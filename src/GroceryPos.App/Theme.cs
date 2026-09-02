using System;
using System.Drawing;
using System.Windows.Forms;

namespace GroceryPos.App
{
    /// <summary>
    /// Shared visual language for every form, translated from the
    /// "Bharat POS Terminal" design system in stitch_kirana_pos_desktop_system/.
    ///
    /// Rules this encodes:
    ///  - Navy primary, near-white surfaces, semantic red/green/amber.
    ///  - Inter (falls back to Segoe UI) for labels, a monospace face for all numbers.
    ///  - 4px spacing grid, 32px table rows, right-aligned numeric columns.
    ///  - Sharp edges, 1px outlines, no gradients or shadows.
    ///
    /// Everything here is plain WinForms so it still renders on Windows 7.
    /// </summary>
    public static class Theme
    {
        // ---- Colours -------------------------------------------------------
        public static readonly Color Primary = Color.FromArgb(0, 31, 63);        // #001F3F navy
        public static readonly Color PrimaryHover = Color.FromArgb(20, 55, 95);
        public static readonly Color OnPrimary = Color.White;

        public static readonly Color Surface = Color.White;                       // work areas
        public static readonly Color Background = Color.FromArgb(248, 249, 250);  // #F8F9FA
        public static readonly Color RowAlt = Color.FromArgb(248, 249, 250);      // zebra stripe
        public static readonly Color Outline = Color.FromArgb(222, 226, 230);     // #DEE2E6
        public static readonly Color OnSurface = Color.FromArgb(25, 28, 29);      // near-black text
        public static readonly Color Muted = Color.FromArgb(67, 71, 78);          // secondary text

        public static readonly Color Danger = Color.FromArgb(220, 53, 69);        // #DC3545
        public static readonly Color Success = Color.FromArgb(25, 135, 84);
        public static readonly Color Warning = Color.FromArgb(180, 105, 20);
        public static readonly Color SelectionBack = Color.FromArgb(212, 227, 255);
        public static readonly Color SelectionFore = Color.FromArgb(0, 28, 58);

        // ---- Spacing (4px grid) -------------------------------------------
        public const int Xs = 4;
        public const int Sm = 8;
        public const int Md = 16;
        public const int Lg = 24;
        public const int RowHeight = 32;
        public const int FieldHeight = 30;
        public const int ButtonHeight = 34;

        // ---- Fonts ---------------------------------------------------------
        // Inter and JetBrains Mono are unlikely to be installed on the shop PC,
        // so resolve to them only if present and fall back to stock Windows faces.
        private static readonly string UiFace = PickFont("Inter", "Segoe UI", "Tahoma");
        private static readonly string MonoFace = PickFont("JetBrains Mono", "Consolas", "Courier New");

        public static Font Body { get { return new Font(UiFace, 9.75f, FontStyle.Regular); } }
        public static Font BodyBold { get { return new Font(UiFace, 9.75f, FontStyle.Bold); } }
        public static Font Label { get { return new Font(UiFace, 8.25f, FontStyle.Bold); } }
        public static Font Headline { get { return new Font(UiFace, 13f, FontStyle.Bold); } }
        public static Font Display { get { return new Font(UiFace, 18f, FontStyle.Bold); } }

        /// <summary>Monospace, for every number the user reads or compares.</summary>
        public static Font Data { get { return new Font(MonoFace, 9.75f, FontStyle.Regular); } }
        public static Font DataBold { get { return new Font(MonoFace, 9.75f, FontStyle.Bold); } }
        public static Font DataLarge { get { return new Font(MonoFace, 20f, FontStyle.Bold); } }

        private static string PickFont(params string[] candidates)
        {
            foreach (var name in candidates)
            {
                try
                {
                    using (var f = new Font(name, 9f))
                    {
                        // WinForms silently substitutes a default face for a missing
                        // font, so compare what we actually got back.
                        if (string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase))
                            return name;
                    }
                }
                catch { /* font construction failed; try the next candidate */ }
            }
            return "Segoe UI";
        }

        // ---- Form ----------------------------------------------------------
        /// <summary>Baseline look for any form. Call first in the constructor.</summary>
        public static void ApplyForm(Form f)
        {
            f.BackColor = Background;
            f.ForeColor = OnSurface;

            // Changing a form's Font while AutoScaleMode is Font or Dpi makes
            // WinForms re-scale the whole form against the new font metrics, which
            // silently shrinks it and clips the content. Layout here is done with
            // docking and explicit sizes, so opt out of automatic rescaling and
            // let the app manifest handle real DPI awareness instead.
            f.AutoScaleMode = AutoScaleMode.None;
            f.Font = Body;
            f.StartPosition = FormStartPosition.CenterParent;
        }

        /// <summary>Navy title strip across the top of a screen.</summary>
        public static Panel Header(string title, string subtitle)
        {
            var p = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = Primary };
            var t = new Label
            {
                Text = title,
                ForeColor = OnPrimary,
                Font = Headline,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Left = Md,
                Top = string.IsNullOrEmpty(subtitle) ? 0 : 6,
                Width = 700,
                Height = string.IsNullOrEmpty(subtitle) ? 56 : 28
            };
            p.Controls.Add(t);
            if (!string.IsNullOrEmpty(subtitle))
            {
                p.Controls.Add(new Label
                {
                    Text = subtitle,
                    ForeColor = Color.FromArgb(175, 200, 240),
                    Font = Body,
                    AutoSize = false,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Left = Md,
                    Top = 30,
                    Width = 700,
                    Height = 20
                });
            }
            return p;
        }

        // ---- Buttons -------------------------------------------------------
        public static Button PrimaryButton(string text)
        {
            var b = BaseButton(text);
            b.BackColor = Primary;
            b.ForeColor = OnPrimary;
            b.Font = BodyBold;
            b.FlatAppearance.MouseOverBackColor = PrimaryHover;
            return b;
        }

        public static Button SecondaryButton(string text)
        {
            var b = BaseButton(text);
            b.BackColor = Surface;
            b.ForeColor = Primary;
            b.FlatAppearance.BorderColor = Primary;
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(232, 238, 247);
            return b;
        }

        public static Button DangerButton(string text)
        {
            var b = BaseButton(text);
            b.BackColor = Danger;
            b.ForeColor = Color.White;
            b.Font = BodyBold;
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(190, 40, 55);
            return b;
        }

        private static Button BaseButton(string text)
        {
            var b = new Button
            {
                Text = text,
                Height = ButtonHeight,
                FlatStyle = FlatStyle.Flat,
                Font = Body,
                UseVisualStyleBackColor = false,
                Cursor = Cursors.Hand,
                Padding = new Padding(Sm, 0, Sm, 0)
            };
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = Outline;
            return b;
        }

        // ---- Form fields ---------------------------------------------------
        /// <summary>A 12px bold caption that sits directly above its input.</summary>
        public static Label FieldLabel(string text)
        {
            return new Label
            {
                Text = text,
                Font = Label,
                ForeColor = Muted,
                AutoSize = false,
                Height = 18,
                TextAlign = ContentAlignment.BottomLeft,
                Margin = new Padding(0, 0, 0, 2)
            };
        }

        public static TextBox TextField(int width)
        {
            return new TextBox
            {
                Width = width,
                Height = FieldHeight,
                Font = Body,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Surface
            };
        }

        /// <summary>A text field for numbers: monospace and right-aligned.</summary>
        public static TextBox NumberField(int width)
        {
            var t = TextField(width);
            t.Font = Data;
            t.TextAlign = HorizontalAlignment.Right;
            return t;
        }

        public static ComboBox DropDown(int width)
        {
            return new ComboBox
            {
                Width = width,
                Font = Body,
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                BackColor = Surface
            };
        }

        // ---- Data tables ---------------------------------------------------
        /// <summary>
        /// Applies the table styling from the design system: navy sticky header,
        /// 32px zebra rows, and a selection colour that stays readable.
        /// </summary>
        public static void ApplyGrid(DataGridView g)
        {
            g.BackgroundColor = Surface;
            g.BorderStyle = BorderStyle.FixedSingle;
            g.GridColor = Outline;
            g.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            g.RowHeadersVisible = false;
            g.AllowUserToResizeRows = false;
            g.AllowUserToOrderColumns = false;
            g.EnableHeadersVisualStyles = false;   // required or the navy header is ignored
            g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            g.MultiSelect = false;
            g.Font = Body;

            g.ColumnHeadersDefaultCellStyle.BackColor = Primary;
            g.ColumnHeadersDefaultCellStyle.ForeColor = OnPrimary;
            g.ColumnHeadersDefaultCellStyle.Font = BodyBold;
            g.ColumnHeadersDefaultCellStyle.SelectionBackColor = Primary;
            g.ColumnHeadersDefaultCellStyle.SelectionForeColor = OnPrimary;
            g.ColumnHeadersDefaultCellStyle.Padding = new Padding(Sm, 0, Sm, 0);
            g.ColumnHeadersHeight = 34;
            g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

            g.DefaultCellStyle.BackColor = Surface;
            g.DefaultCellStyle.ForeColor = OnSurface;
            g.DefaultCellStyle.SelectionBackColor = SelectionBack;
            g.DefaultCellStyle.SelectionForeColor = SelectionFore;
            g.DefaultCellStyle.Padding = new Padding(Sm, 0, Sm, 0);
            // Zebra striping is applied per row in CellFormatting rather than via
            // AlternatingRowsDefaultCellStyle, because that style outranks a
            // column's own BackColor and would wipe out editable-column tinting.
            g.CellFormatting += ZebraStripe;

            g.RowTemplate.Height = RowHeight;
            g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;

            // A grid that throws a raw .NET dialog at a cashier is unusable.
            // Swallow the noise and show nothing; callers that care handle it.
            g.DataError += (s, e) => { e.ThrowException = false; };
        }

        private static void ZebraStripe(object sender, DataGridViewCellFormattingEventArgs e)
        {
            var g = sender as DataGridView;
            if (g == null || e.RowIndex < 0 || e.RowIndex >= g.Rows.Count) return;
            if (e.ColumnIndex < 0 || e.ColumnIndex >= g.Columns.Count) return;

            // Leave a column that set its own colour alone.
            var colStyle = g.Columns[e.ColumnIndex].DefaultCellStyle;
            if (colStyle != null && !colStyle.BackColor.IsEmpty && colStyle.BackColor != Surface)
                return;

            e.CellStyle.BackColor = (e.RowIndex % 2 == 1) ? RowAlt : Surface;
        }

        /// <summary>
        /// Tints a column to say "you type in here". Sets the alternating-row
        /// style as well, because the zebra stripe otherwise wins on every second
        /// row and the column comes out looking patchy.
        /// </summary>
        public static void MarkEditable(DataGridViewColumn col)
        {
            var tint = Color.FromArgb(255, 250, 224);
            col.DefaultCellStyle.BackColor = tint;
            col.DefaultCellStyle.Font = DataBold;
            col.DefaultCellStyle.SelectionBackColor = SelectionBack;
            col.DefaultCellStyle.SelectionForeColor = SelectionFore;
        }

        /// <summary>Right-aligned monospace column, for money, weights and counts.</summary>
        public static DataGridViewTextBoxColumn NumberColumn(string prop, string header, int width)
        {
            var c = TextColumn(prop, header, width);
            c.DefaultCellStyle.Font = Data;
            c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            c.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
            return c;
        }

        public static DataGridViewTextBoxColumn TextColumn(string prop, string header, int width)
        {
            return new DataGridViewTextBoxColumn
            {
                DataPropertyName = prop,
                HeaderText = header,
                Width = width,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
        }

        // ---- Cards and panels ----------------------------------------------
        /// <summary>White panel with a 1px outline — "Level 1" in the design system.</summary>
        public static Panel Card()
        {
            return new Panel
            {
                BackColor = Surface,
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(Md)
            };
        }

        /// <summary>A metric tile: big number over a small caption.</summary>
        public static Panel MetricCard(string caption, string value, Color valueColor)
        {
            var p = Card();
            p.Padding = new Padding(Sm + Xs, Sm, Sm + Xs, Sm);
            var cap = new Label
            {
                Text = caption.ToUpperInvariant(),
                Dock = DockStyle.Top,
                Height = 18,
                Font = Label,
                ForeColor = Muted,
                TextAlign = ContentAlignment.MiddleLeft
            };
            var val = new Label
            {
                Text = value,
                Dock = DockStyle.Fill,
                Font = new Font(MonoFace, 15f, FontStyle.Bold),
                ForeColor = valueColor,
                TextAlign = ContentAlignment.MiddleLeft,
                Name = "value"
            };
            p.Controls.Add(val);
            p.Controls.Add(cap);
            return p;
        }

        public static void SetMetric(Panel card, string value)
        {
            var l = card.Controls["value"] as Label;
            if (l != null) l.Text = value;
        }

        // ---- Empty state ---------------------------------------------------
        /// <summary>
        /// Shown instead of a bare empty grid. A blank table with no explanation is
        /// the single most confusing thing for a non-technical shop owner.
        /// </summary>
        public static Panel EmptyState(string message, string actionText, Action onAction)
        {
            var host = new Panel { Dock = DockStyle.Fill, BackColor = Surface, Visible = false };
            var inner = new Panel { Width = 460, Height = actionText == null ? 70 : 116, BackColor = Surface };

            inner.Controls.Add(new Label
            {
                Text = message,
                Dock = DockStyle.Top,
                Height = 62,
                Font = Body,
                ForeColor = Muted,
                TextAlign = ContentAlignment.MiddleCenter
            });

            if (actionText != null && onAction != null)
            {
                var b = PrimaryButton(actionText);
                b.Width = 240;
                b.Left = (460 - 240) / 2;
                b.Top = 70;
                b.Click += (s, e) => onAction();
                inner.Controls.Add(b);
            }

            host.Controls.Add(inner);
            EventHandler center = (s, e) =>
            {
                inner.Left = Math.Max(0, (host.ClientSize.Width - inner.Width) / 2);
                inner.Top = Math.Max(0, (host.ClientSize.Height - inner.Height) / 2);
            };
            host.Resize += center;
            host.VisibleChanged += center;
            return host;
        }

        // ---- Inline banner --------------------------------------------------
        /// <summary>
        /// A coloured strip across the top of a screen, for guidance that should
        /// not interrupt: "add a supplier first", "nothing to count yet". A modal
        /// popup on form load makes the user dismiss a box before they can even
        /// look at the screen, so notices like these belong here instead.
        /// </summary>
        public static Panel Banner()
        {
            var p = new Panel
            {
                Dock = DockStyle.Top,
                Height = 0,
                Visible = false,
                Padding = new Padding(Md, Sm, Md, Sm),
                BackColor = Color.FromArgb(255, 248, 225)
            };
            var text = new Label
            {
                Name = "text",
                Dock = DockStyle.Fill,
                Font = Body,
                ForeColor = Color.FromArgb(110, 75, 10),
                TextAlign = ContentAlignment.MiddleLeft
            };
            p.Controls.Add(text);
            return p;
        }

        public static void ShowBanner(Panel banner, string message)
        {
            var l = banner.Controls["text"] as Label;
            if (l == null) return;
            l.Text = message;
            int lines = message.Split('\n').Length;
            banner.Height = Math.Max(38, 20 * lines + Md);
            banner.Visible = true;
        }

        public static void HideBanner(Panel banner)
        {
            banner.Visible = false;
            banner.Height = 0;
        }

        // ---- Feedback ------------------------------------------------------
        public static void Info(string message, string title)
        {
            MessageBox.Show(message, title ?? "Grocery POS",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public static void Warn(string message)
        {
            MessageBox.Show(message, "Please check",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        public static void Error(string message)
        {
            MessageBox.Show(message, "Could not continue",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        public static bool Confirm(string message, string title)
        {
            return MessageBox.Show(message, title ?? "Please confirm",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        }

        // ---- Retro-fitting existing forms -----------------------------------
        /// <summary>
        /// Walks a form built with plain WinForms controls and gives it the shared
        /// look: themed grids, buttons and fonts. Screens whose layout has already
        /// been rebuilt by hand call the specific helpers instead; this exists so
        /// the remaining forms are consistent without rewriting every one of them.
        ///
        /// Layout is left untouched — only colours, fonts and grid styling change.
        /// </summary>
        public static void Retrofit(Form f)
        {
            f.BackColor = Background;
            f.ForeColor = OnSurface;
            f.AutoScaleMode = AutoScaleMode.None;   // see the note in ApplyForm
            RetrofitChildren(f);
        }

        private static void RetrofitChildren(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                var grid = c as DataGridView;
                if (grid != null)
                {
                    bool wasReadOnly = grid.ReadOnly;
                    ApplyGrid(grid);
                    grid.ReadOnly = wasReadOnly;
                    // A read-only grid should not offer a phantom "new row".
                    if (wasReadOnly) grid.AllowUserToAddRows = false;
                    RightAlignNumericColumns(grid);
                    continue;
                }

                var button = c as Button;
                if (button != null)
                {
                    StyleExistingButton(button);
                    continue;
                }

                var box = c as TextBox;
                if (box != null)
                {
                    box.BorderStyle = BorderStyle.FixedSingle;
                    if (box.Font.SizeInPoints < 8f) box.Font = Body;
                    continue;
                }

                var list = c as ListBox;
                if (list != null)
                {
                    list.BorderStyle = BorderStyle.FixedSingle;
                    list.Font = Data;
                    continue;
                }

                var panel = c as Panel;
                if (panel != null && panel.BorderStyle == BorderStyle.FixedSingle)
                    panel.BackColor = Surface;

                if (c.HasChildren) RetrofitChildren(c);
            }
        }

        private static void StyleExistingButton(Button b)
        {
            // Anything already carrying a deliberate colour is left alone.
            if (b.BackColor != SystemColors.Control && b.BackColor != Color.Empty) return;

            b.FlatStyle = FlatStyle.Flat;
            b.UseVisualStyleBackColor = false;
            b.BackColor = Surface;
            b.ForeColor = Primary;
            b.Cursor = Cursors.Hand;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = Outline;
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(232, 238, 247);
            if (b.Height < ButtonHeight) b.Height = ButtonHeight;
        }

        /// <summary>
        /// Money, quantity and count columns read far better right-aligned in a
        /// monospace face, so digits line up down the column.
        /// </summary>
        private static void RightAlignNumericColumns(DataGridView g)
        {
            foreach (DataGridViewColumn col in g.Columns)
            {
                string name = ((col.DataPropertyName ?? "") + " " + (col.HeaderText ?? "")).ToLowerInvariant();
                bool numeric =
                    name.Contains("amount") || name.Contains("paise") || name.Contains("rs") ||
                    name.Contains("qty") || name.Contains("units") || name.Contains("grams") ||
                    name.Contains("debit") || name.Contains("credit") || name.Contains("balance") ||
                    name.Contains("rate") || name.Contains("cost") || name.Contains("mrp") ||
                    name.Contains("total") || name.Contains("count") || name.Contains("value") ||
                    name.Contains("price") || name.Contains("disc") || name.Contains("weight");

                if (!numeric) continue;
                col.DefaultCellStyle.Font = Data;
                col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
            }
        }
    }
}
