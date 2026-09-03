using System;
using System.Drawing;
using System.Windows.Forms;
using GroceryPos.Domain;

namespace GroceryPos.App
{
    /// <summary>
    /// The home screen. Tiles are grouped under plain-language section headings
    /// so the owner can find a task by what it is for, not by a number.
    /// </summary>
    public class MainMenuForm : Form
    {
        private readonly AppContext _ctx;
        private FlowLayoutPanel _flow;

        public MainMenuForm(AppContext ctx)
        {
            _ctx = ctx;
            Theme.ApplyForm(this);
            Text = "Grocery POS - " + ctx.CurrentUser.Name + " (" + ctx.CurrentUser.Role + ")";
            Width = 1280; Height = 800;
            MinimumSize = new Size(1000, 640);
            StartPosition = FormStartPosition.CenterScreen;
            WindowState = FormWindowState.Maximized;

            var storeName = ctx.Settings != null ? ctx.Settings.Get("store_name", "GROCERY STORE") : "GROCERY STORE";
            var header = Theme.Header(storeName, "Signed in: " + ctx.CurrentUser.Name + "  (" + ctx.CurrentUser.Role + ")");

            var signOut = Theme.SecondaryButton("Sign out");
            signOut.Width = 110;
            signOut.BackColor = Theme.Primary;
            signOut.ForeColor = Color.White;
            signOut.FlatAppearance.BorderColor = Color.FromArgb(90, 115, 150);
            signOut.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            signOut.Click += (s, e) => Close();
            header.Controls.Add(signOut);
            header.Resize += (s, e) =>
            {
                signOut.Left = header.ClientSize.Width - signOut.Width - Theme.Md;
                signOut.Top = (header.Height - signOut.Height) / 2;
            };

            _flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(Theme.Lg, Theme.Md, Theme.Lg, Theme.Lg),
                BackColor = Theme.Background,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false
            };

            bool isOwner = ctx.CurrentUser.Role == UserRole.Owner;
            bool isManager = isOwner || ctx.CurrentUser.Role == UserRole.Manager;

            Section("Billing");
            Tiles(
                Tile("Billing counter", "Scan, weigh, take payment", Theme.Primary,
                     () => new BillingForm(_ctx).ShowDialog()),
                Tile("Shift and day close", "Count the cash, close the day", Color.FromArgb(25, 105, 70),
                     () => new ShiftForm(_ctx).ShowDialog()));

            Section("Stock");
            Tiles(
                Tile("Stock summary", "What is on hand right now", Color.FromArgb(45, 90, 145),
                     () => new StockSummaryForm(_ctx).ShowDialog()),
                Tile("Stock take", "Count shelves, fix differences", Color.FromArgb(45, 90, 145),
                     () => new StockTakeForm(_ctx).ShowDialog()),
                Tile("Damage and wastage", "Write off spoiled goods", Color.FromArgb(150, 60, 60),
                     () => new WastageForm(_ctx).ShowDialog()),
                Tile("Open a sack", "Turn a bag into loose stock", Color.FromArgb(45, 90, 145),
                     () => new UnitConversionForm(_ctx).ShowDialog()),
                Tile("Expiring soon", "Goods near their expiry date", Color.FromArgb(155, 100, 35),
                     () => new NearExpiryReportForm(_ctx).ShowDialog()),
                Tile("Items to reorder", "What is running low", Color.FromArgb(155, 100, 35),
                     () => new ReorderReportForm(_ctx).ShowDialog()));

            Section("Buying");
            Tiles(
                Tile("Purchase entry", "Record a supplier bill", Color.FromArgb(85, 60, 135),
                     () => new PurchaseEntryForm(_ctx).ShowDialog()),
                Tile("Return to supplier", "Send goods back", Color.FromArgb(85, 60, 135),
                     () => new PurchaseReturnForm(_ctx).ShowDialog()));

            Section("Customers and credit");
            Tiles(
                Tile("Customer credit (kadan)", "Who owes what, and why", Color.FromArgb(180, 85, 120),
                     () => new CustomerLedgerForm(_ctx).ShowDialog()),
                Tile("Money owed by age", "Chase the oldest debts", Color.FromArgb(180, 85, 120),
                     () => new AgeingReportForm(_ctx).ShowDialog()),
                OwnerTile(isOwner, "Opening balances", "Carry in the old kadan book", Color.FromArgb(180, 85, 120),
                     () => new OpeningBalanceImportForm(_ctx).ShowDialog()));

            Section("Products and reports");
            Tiles(
                Tile("Item master", "Add and edit your products", Color.FromArgb(200, 120, 35),
                     () => new ItemMasterForm(_ctx).ShowDialog()),
                Tile("Reports and GST", "Sales, tax and exports", Color.FromArgb(80, 80, 80),
                     () => new ReportsMenuForm(_ctx).ShowDialog()));

            Section("Setup");
            Tiles(
                Tile("Scale and weight", "Set up the weighing scale", Color.FromArgb(45, 90, 145),
                     () => new ScaleSetupForm(_ctx).ShowDialog()),
                Tile("Settings", "Store name, printer, counter", Color.FromArgb(80, 80, 80),
                     () => new SettingsForm(_ctx).ShowDialog()),
                OwnerTile(isManager, "Staff accounts", "Who can sign in, and as what", Color.FromArgb(80, 80, 80),
                     () => new UsersForm(_ctx).ShowDialog()));

            Controls.Add(_flow);
            Controls.Add(header);

            // The shop's screen is narrower than this laptop's. Fixed-width rows
            // pushed the panel wider than the window, which turned the vertical
            // scrollbar into a horizontal one and left the lower tiles
            // unreachable. Resize the rows with the window instead.
            _flow.Resize += (s, e) => FitRowsToWidth();
            Shown += (s, e) => FitRowsToWidth();

            // A wheel over a tile would otherwise do nothing, because the tile
            // has focus rather than the scrolling panel.
            HookWheel(_flow);
        }

        /// <summary>Keeps every row as wide as the visible area, less the padding.</summary>
        private void FitRowsToWidth()
        {
            int usable = _flow.ClientSize.Width - _flow.Padding.Horizontal;
            if (usable < 300) usable = 300;
            foreach (Control c in _flow.Controls)
                c.Width = usable;
        }

        /// <summary>Sends the mouse wheel to the scrolling panel from any child.</summary>
        private void HookWheel(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                c.MouseWheel += (s, e) =>
                {
                    var native = (HandledMouseEventArgs)e;
                    native.Handled = true;
                    int delta = -Math.Sign(e.Delta) * 60;
                    var v = _flow.VerticalScroll;
                    v.Value = Math.Max(v.Minimum, Math.Min(v.Maximum, v.Value + delta));
                    _flow.PerformLayout();
                };
                c.MouseEnter += (s, e) => { if (!_flow.Focused) _flow.Focus(); };
                if (c.HasChildren) HookWheel(c);
            }
        }

        // ---- Layout helpers -------------------------------------------------
        private void Section(string title)
        {
            var l = new Label
            {
                Text = title,
                Font = Theme.Headline,
                ForeColor = Theme.OnSurface,
                AutoSize = false,
                Width = 1100,
                Height = 34,
                TextAlign = ContentAlignment.BottomLeft,
                Margin = new Padding(0, Theme.Md, 0, Theme.Xs)
            };
            _flow.Controls.Add(l);

            var rule = new Panel { Width = 1100, Height = 1, BackColor = Theme.Outline, Margin = new Padding(0, 0, 0, Theme.Sm) };
            _flow.Controls.Add(rule);
        }

        /// <summary>Lays a row of tiles out left to right, wrapping as needed.</summary>
        private void Tiles(params Panel[] tiles)
        {
            var row = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = true,
                Width = 1100,
                Margin = new Padding(0, 0, 0, Theme.Sm),
                FlowDirection = FlowDirection.LeftToRight
            };
            foreach (var t in tiles)
                if (t != null) row.Controls.Add(t);
            _flow.Controls.Add(row);
        }

        /// <summary>A tile that is simply absent when the user's role may not use it.</summary>
        private Panel OwnerTile(bool allowed, string title, string subtitle, Color color, Action onClick)
        {
            return allowed ? Tile(title, subtitle, color, onClick) : null;
        }

        private Panel Tile(string title, string subtitle, Color color, Action onClick)
        {
            var tile = new Panel
            {
                Width = 280,
                Height = 84,
                BackColor = color,
                Margin = new Padding(0, 0, Theme.Sm, Theme.Sm),
                Cursor = Cursors.Hand
            };

            var titleLbl = new Label
            {
                Text = title,
                ForeColor = Color.White,
                Font = Theme.BodyBold,
                AutoSize = false,
                TextAlign = ContentAlignment.BottomLeft,
                Left = Theme.Md, Top = Theme.Md, Width = 248, Height = 26,
                BackColor = Color.Transparent
            };
            var subLbl = new Label
            {
                Text = subtitle,
                ForeColor = Color.FromArgb(225, 232, 242),
                Font = Theme.Body,
                AutoSize = false,
                TextAlign = ContentAlignment.TopLeft,
                Left = Theme.Md, Top = Theme.Md + 26, Width = 248, Height = 34,
                BackColor = Color.Transparent
            };
            tile.Controls.Add(titleLbl);
            tile.Controls.Add(subLbl);

            EventHandler click = (s, e) =>
            {
                try { onClick(); }
                catch (Exception ex)
                {
                    Theme.Error("This screen could not be opened.\r\n\r\nDetails: " + ex.Message);
                }
            };
            Action<Color> paint = c =>
            {
                tile.BackColor = c;
            };

            foreach (Control c in new Control[] { tile, titleLbl, subLbl })
            {
                c.Click += click;
                c.MouseEnter += (s, e) => paint(Lighten(color, 22));
                c.MouseLeave += (s, e) => paint(color);
            }
            return tile;
        }

        private static Color Lighten(Color c, int amount)
        {
            return Color.FromArgb(
                Math.Min(255, c.R + amount),
                Math.Min(255, c.G + amount),
                Math.Min(255, c.B + amount));
        }
    }
}
