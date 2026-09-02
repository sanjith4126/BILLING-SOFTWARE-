using System;
using System.Drawing;
using System.Windows.Forms;

namespace GroceryPos.App
{
    public class MainMenuForm : Form
    {
        private readonly AppContext _ctx;

        public MainMenuForm(AppContext ctx)
        {
            _ctx = ctx;
            Text = "Grocery POS — " + ctx.CurrentUser.Name + " (" + ctx.CurrentUser.Role + ")";
            Width = 1000; Height = 720;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(245, 247, 250);
            WindowState = FormWindowState.Maximized;

            var header = new Panel { Dock = DockStyle.Top, Height = 70, BackColor = Color.FromArgb(30, 60, 100) };
            var storeName = ctx.Settings != null ? ctx.Settings.Get("store_name", "GROCERY STORE") : "GROCERY STORE";
            header.Controls.Add(new Label
            {
                Text = storeName,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 18F, FontStyle.Bold),
                Left = 20, Top = 12, Width = 600, Height = 30
            });
            header.Controls.Add(new Label
            {
                Text = "Signed in: " + ctx.CurrentUser.Name + "  (" + ctx.CurrentUser.Role + ")",
                ForeColor = Color.WhiteSmoke,
                Font = new Font("Segoe UI", 10F),
                Left = 20, Top = 42, Width = 600, Height = 22
            });
            Controls.Add(header);

            // Center a 3-column grid of tiles regardless of window size.
            var outer = new Panel { Dock = DockStyle.Fill };
            var grid = new TableLayoutPanel
            {
                ColumnCount = 3,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
                Anchor = AnchorStyles.None,
                Padding = new Padding(10)
            };
            for (int i = 0; i < 3; i++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300));

            AddTile(grid, "1", "Billing counter", Color.FromArgb(30, 130, 76), () => new BillingForm(_ctx).ShowDialog());
            AddTile(grid, "2", "Scale & weight setup", Color.FromArgb(60, 100, 160), () => { new ScaleSetupForm(_ctx).ShowDialog(); });
            AddTile(grid, "4", "Item master", Color.FromArgb(220, 130, 40), () => new ItemMasterForm(_ctx).ShowDialog());

            AddTile(grid, "5a", "Stock summary", Color.FromArgb(60, 100, 160), () => new StockSummaryForm(_ctx).ShowDialog());
            AddTile(grid, "5b", "Stock take", Color.FromArgb(60, 100, 160), () => new StockTakeForm(_ctx).ShowDialog());
            AddTile(grid, "5c", "Damage / wastage", Color.FromArgb(160, 60, 60), () => new WastageForm(_ctx).ShowDialog());

            AddTile(grid, "5d", "Unit conversion", Color.FromArgb(60, 100, 160), () => new UnitConversionForm(_ctx).ShowDialog());
            AddTile(grid, "5e", "Near-expiry report", Color.FromArgb(160, 100, 40), () => new NearExpiryReportForm(_ctx).ShowDialog());
            AddTile(grid, "5f", "Reorder report", Color.FromArgb(160, 100, 40), () => new ReorderReportForm(_ctx).ShowDialog());

            AddTile(grid, "6a", "Purchase entry", Color.FromArgb(90, 60, 140), () => new PurchaseEntryForm(_ctx).ShowDialog());
            AddTile(grid, "6b", "Purchase return", Color.FromArgb(90, 60, 140), () => new PurchaseReturnForm(_ctx).ShowDialog());
            AddTile(grid, "7a", "Customer khata", Color.FromArgb(200, 90, 130), () => new CustomerLedgerForm(_ctx).ShowDialog());

            AddTile(grid, "7b", "Opening balance", Color.FromArgb(200, 90, 130), () => new OpeningBalanceImportForm(_ctx).ShowDialog());
            AddTile(grid, "7c", "Ageing report", Color.FromArgb(200, 90, 130), () => new AgeingReportForm(_ctx).ShowDialog());
            AddTile(grid, "8", "Shift / day close", Color.FromArgb(30, 130, 76), () => new ShiftForm(_ctx).ShowDialog());

            AddTile(grid, "9", "Reports & GST", Color.FromArgb(90, 90, 90), () => new ReportsMenuForm(_ctx).ShowDialog());
            AddTile(grid, "", "Settings", Color.FromArgb(90, 90, 90), () => new SettingsForm(_ctx).ShowDialog());
            AddTile(grid, "", "Users (staff)", Color.FromArgb(90, 90, 90), () => new UsersForm(_ctx).ShowDialog());

            AddTile(grid, "", "Sign out", Color.FromArgb(120, 40, 40), () => Close());

            outer.Controls.Add(grid);
            outer.Resize += (s, e) => CenterGrid(outer, grid);
            grid.Resize += (s, e) => CenterGrid(outer, grid);
            CenterGrid(outer, grid);
            Controls.Add(outer);
        }

        private void CenterGrid(Panel outer, TableLayoutPanel grid)
        {
            grid.Left = Math.Max(0, (outer.ClientSize.Width - grid.Width) / 2);
            grid.Top = Math.Max(0, (outer.ClientSize.Height - grid.Height) / 2);
        }

        private void AddTile(TableLayoutPanel grid, string number, string label, Color color, Action onClick)
        {
            var tile = new Panel
            {
                Width = 280,
                Height = 90,
                BackColor = color,
                Margin = new Padding(6),
                Cursor = Cursors.Hand
            };

            var numLbl = new Label
            {
                Text = number,
                ForeColor = Color.FromArgb(255, 255, 255),
                Font = new Font("Segoe UI", 20F, FontStyle.Bold),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Left = 6, Top = 6, Width = 50, Height = 78,
                BackColor = Color.FromArgb(0, 0, 0, 0)
            };
            var textLbl = new Label
            {
                Text = label,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Left = 64, Top = 6, Width = 210, Height = 78,
                BackColor = Color.FromArgb(0, 0, 0, 0)
            };
            tile.Controls.Add(numLbl);
            tile.Controls.Add(textLbl);

            EventHandler click = (s, e) => onClick();
            tile.Click += click;
            numLbl.Click += click;
            textLbl.Click += click;

            tile.MouseEnter += (s, e) => tile.BackColor = Lighten(color, 15);
            tile.MouseLeave += (s, e) => tile.BackColor = color;
            numLbl.MouseEnter += (s, e) => tile.BackColor = Lighten(color, 15);
            numLbl.MouseLeave += (s, e) => tile.BackColor = color;
            textLbl.MouseEnter += (s, e) => tile.BackColor = Lighten(color, 15);
            textLbl.MouseLeave += (s, e) => tile.BackColor = color;

            grid.Controls.Add(tile);
        }

        private static Color Lighten(Color c, int amount)
        {
            int r = Math.Min(255, c.R + amount);
            int g = Math.Min(255, c.G + amount);
            int b = Math.Min(255, c.B + amount);
            return Color.FromArgb(r, g, b);
        }
    }
}
