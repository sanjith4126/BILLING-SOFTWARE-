using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Dapper;
using GroceryPos.Data;
using GroceryPos.Domain;
using GroceryPos.Hardware;
using GroceryPos.Printing;

namespace GroceryPos.App
{
    /// <summary>
    /// Phase 2/3/5 billing counter. Keyboard-first: scan field keeps focus.
    /// F2 hold, F3 recall, F4 weigh, F5 discount, F9 payment, Del remove, Esc clear.
    /// </summary>
    public class BillingForm : Form
    {
        private readonly AppContext _ctx;
        private TextBox _scan;
        private DataGridView _grid;
        private Label _lblSubtotal, _lblTax, _lblNet, _lblCustomer, _lblScale;
        private ListBox _heldList;
        private Panel _emptyBill;
        private Bill _bill;
        private readonly List<Bill> _held = new List<Bill>();
        private readonly BindingList<BillLineView> _linesView = new BindingList<BillLineView>();
        private readonly WeightBarcodeParser _wbParser = new WeightBarcodeParser();
        private Customer _customer;
        private long? _currentShiftId;

        public BillingForm(AppContext ctx)
        {
            _ctx = ctx;
            Theme.ApplyForm(this);
            Text = "Billing counter";
            Width = 1280; Height = 800;
            MinimumSize = new Size(1024, 640);
            StartPosition = FormStartPosition.CenterScreen;
            WindowState = FormWindowState.Maximized;
            KeyPreview = true;

            // Everything is docked rather than positioned with pixel literals, so
            // the layout survives a different screen size or Windows text scaling.
            var top = BuildScanBar();
            var right = BuildTotalsPanel();
            var held = BuildHeldPanel();
            var centre = BuildLineGrid();
            var keys = BuildKeyHintBar();

            Controls.Add(centre);   // Fill  — added first so it is sized last
            Controls.Add(right);    // Right
            Controls.Add(held);     // Bottom
            Controls.Add(keys);     // Bottom (below held)
            Controls.Add(top);      // Top

            ClearBill(false);
            KeyDown += BillingForm_KeyDown;
            Shown += (s, e) => { _scan.Focus(); EnsureShiftOpen(); };
            UpdateScaleLabel();
        }

        private Panel BuildScanBar()
        {
            var top = new Panel
            {
                Dock = DockStyle.Top,
                Height = 74,
                BackColor = Theme.Surface,
                Padding = new Padding(Theme.Md, Theme.Sm, Theme.Md, Theme.Sm)
            };

            var lblScan = new Label
            {
                Text = "Scan barcode or type an item name",
                Font = Theme.Label,
                ForeColor = Theme.Muted,
                AutoSize = false,
                Left = Theme.Md, Top = 6, Width = 320, Height = 16,
                TextAlign = ContentAlignment.MiddleLeft
            };
            _scan = new TextBox
            {
                Left = Theme.Md, Top = 24,
                Width = 460, Height = 34,
                Font = new Font(Theme.Data.FontFamily, 13f),
                BorderStyle = BorderStyle.FixedSingle
            };
            _scan.KeyDown += Scan_KeyDown;

            _lblCustomer = new Label
            {
                Left = 500, Top = 8, Width = 380, Height = 24,
                Text = "Customer: walk-in",
                Font = Theme.BodyBold,
                TextAlign = ContentAlignment.MiddleLeft
            };
            _lblScale = new Label
            {
                Left = 500, Top = 34, Width = 380, Height = 22,
                Text = "Scale: manual",
                Font = Theme.Data,
                ForeColor = Theme.Muted,
                TextAlign = ContentAlignment.MiddleLeft
            };

            var btnCust = Theme.SecondaryButton("Customer  [Ctrl+K]");
            btnCust.Width = 170;
            btnCust.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnCust.Click += (s, e) => LookupCustomer();

            top.Controls.AddRange(new Control[] { lblScan, _scan, _lblCustomer, _lblScale, btnCust });
            top.Resize += (s, e) =>
            {
                btnCust.Left = top.ClientSize.Width - btnCust.Width - Theme.Md;
                btnCust.Top = 22;
            };
            return top;
        }

        private Panel BuildLineGrid()
        {
            var host = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(Theme.Md, Theme.Sm, Theme.Sm, Theme.Sm),
                BackColor = Theme.Background
            };

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                // Qty, Rate and Disc are typed into directly; the rest stay
                // read-only so a cashier cannot rename an item on the bill.
                ReadOnly = false,
                AutoGenerateColumns = false,
                // EditOnKeystrokeOrF2 would start an edit on ANY key, which
                // swallowed Del before it could remove the line. Editing now
                // starts on F2, a double-click, or typing a digit -- see
                // Grid_KeyDown below.
                EditMode = DataGridViewEditMode.EditOnF2
            };
            Theme.ApplyGrid(_grid);
            var lineNoCol = Theme.NumberColumn("LineNo", "#", 46);
            lineNoCol.ReadOnly = true;
            _grid.Columns.Add(lineNoCol);
            var nameCol = Theme.TextColumn("ItemName", "Item", 300);
            nameCol.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;   // takes the slack
            nameCol.MinimumWidth = 180;
            nameCol.ReadOnly = true;
            _grid.Columns.Add(nameCol);

            var qtyCol = Theme.NumberColumn("Qty", "Qty", 110);
            var rateCol = Theme.NumberColumn("Rate", "Rate", 90);
            var discCol = Theme.NumberColumn("Disc", "Disc", 80);
            foreach (var c in new[] { qtyCol, rateCol, discCol })
            {
                Theme.MarkEditable(c);   // tints them as "you can type here"
                _grid.Columns.Add(c);
            }

            var amt = Theme.NumberColumn("Amount", "Amount", 110);
            amt.DefaultCellStyle.Font = Theme.DataBold;
            amt.ReadOnly = true;         // always worked out, never typed
            _grid.Columns.Add(amt);
            _grid.DataSource = _linesView;

            _grid.CellEndEdit += Grid_CellEndEdit;
            _grid.DataError += (s, e) => { e.ThrowException = false; };
            _grid.KeyDown += Grid_KeyDown;

            _emptyBill = Theme.EmptyState(
                "No items yet.\r\n\r\nScan a barcode, or type part of an item name and press Enter.",
                null, null);
            _linesView.ListChanged += (s, e) => UpdateEmptyBill();

            host.Controls.Add(_grid);
            host.Controls.Add(_emptyBill);
            return host;
        }

        /// <summary>
        /// Applies a quantity, rate or discount typed straight into the bill grid.
        /// Anything invalid is refused with a plain message and the old value is
        /// put back, so a mistyped rate can never reach a customer's bill.
        /// </summary>
        private void Grid_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _bill.Lines.Count) return;

            var line = _bill.Lines[e.RowIndex];
            string col = _grid.Columns[e.ColumnIndex].DataPropertyName;
            string typed = (_grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value ?? "").ToString().Trim();

            try
            {
                if (col == "Qty")
                {
                    if (line.QtyGrams > 0)
                    {
                        // Loose goods are changed by weighing again, not by typing
                        // a quantity, or the weight and the charge stop matching.
                        Theme.Warn("This is a weighed item.\r\n" +
                                   "Use Weigh (F4) to change it, or remove the line and weigh again.");
                        RefreshView();
                        return;
                    }

                    int units;
                    if (!int.TryParse(typed, out units) || units <= 0)
                    {
                        Theme.Warn("Quantity must be a whole number of 1 or more.");
                        RefreshView();
                        return;
                    }
                    line.QtyUnits = units;
                }
                else if (col == "Rate")
                {
                    long rate = Money.ParseRupees(typed).Paise;
                    if (rate <= 0)
                    {
                        Theme.Warn("The rate must be more than zero.");
                        RefreshView();
                        return;
                    }

                    // Rule 6: never sell above the printed MRP.
                    var item = _ctx.Items.FindById(line.ItemId);
                    if (item != null && item.DefaultMrpPaise > 0 && rate > item.DefaultMrpPaise)
                    {
                        Theme.Warn("Rs. " + new Money(rate) + " is above the MRP of Rs. " +
                                   new Money(item.DefaultMrpPaise) + ".\r\n" +
                                   "You cannot sell above the printed price.");
                        RefreshView();
                        return;
                    }
                    line.RatePaise = rate;
                }
                else if (col == "Disc")
                {
                    long disc = Money.ParseRupees(typed).Paise;
                    if (disc < 0)
                    {
                        Theme.Warn("A discount cannot be less than zero.");
                        RefreshView();
                        return;
                    }

                    long lineTotal = line.QtyGrams > 0
                        ? (line.RatePaise * line.QtyGrams + 500L) / 1000L
                        : line.RatePaise * line.QtyUnits;

                    if (disc > lineTotal)
                    {
                        Theme.Warn("The discount is more than the line is worth (Rs. " +
                                   new Money(lineTotal) + ").");
                        RefreshView();
                        return;
                    }
                    line.DiscountPaise = disc;
                }
                else
                {
                    return;   // not an editable column
                }
            }
            catch (Exception)
            {
                Theme.Warn("That is not a number the software can read.\r\n" +
                           "Type it like 45 or 45.50.");
            }

            RefreshView();
            SelectLine(e.RowIndex);
        }

        /// <summary>
        /// Typing a digit on an editable cell starts an edit, the way a
        /// spreadsheet does. Del is deliberately NOT an edit trigger, so it
        /// still removes the whole line.
        /// </summary>
        private void Grid_KeyDown(object sender, KeyEventArgs e)
        {
            if (_grid.IsCurrentCellInEditMode) return;
            if (_grid.CurrentCell == null || _grid.CurrentCell.ReadOnly) return;

            bool digit = (e.KeyCode >= Keys.D0 && e.KeyCode <= Keys.D9)
                      || (e.KeyCode >= Keys.NumPad0 && e.KeyCode <= Keys.NumPad9);

            if (digit)
            {
                _grid.BeginEdit(true);
                // Replace the old figure rather than appending to it, which is
                // what someone correcting a quantity expects.
                var editor = _grid.EditingControl as TextBox;
                if (editor != null) editor.Text = "";
            }
        }

        /// <summary>Keeps the highlight on a line after the grid is rebuilt.</summary>
        private void SelectLine(int index)
        {
            if (index < 0 || index >= _grid.Rows.Count) return;
            try
            {
                _grid.ClearSelection();
                _grid.Rows[index].Selected = true;
                _grid.CurrentCell = _grid.Rows[index].Cells[1];
            }
            catch { /* row went away */ }
        }

        private void UpdateEmptyBill()
        {
            if (_emptyBill == null) return;
            bool empty = _linesView.Count == 0;
            _emptyBill.Visible = empty;
            _grid.Visible = !empty;
            if (empty) _emptyBill.BringToFront();
        }

        private Panel BuildTotalsPanel()
        {
            var right = new Panel
            {
                Dock = DockStyle.Right,
                Width = 360,
                BackColor = Theme.Surface,
                Padding = new Padding(Theme.Md)
            };

            // Actions sit at the bottom, totals fill above — so the NET figure can
            // never be clipped by a short window the way the fixed layout was.
            var actions = new Panel { Dock = DockStyle.Bottom, Height = 250, BackColor = Theme.Surface };

            var btnPay = Theme.PrimaryButton("PAY   [F9]");
            btnPay.Dock = DockStyle.Top;
            btnPay.Height = 56;
            btnPay.Font = new Font(Theme.BodyBold.FontFamily, 13f, FontStyle.Bold);
            btnPay.Click += (s, e) => OpenPayment();

            var gap = new Panel { Dock = DockStyle.Top, Height = Theme.Sm, BackColor = Theme.Surface };

            var rowA = TwoUp(
                Btn("Discount  [F5]", () => ApplyDiscount()),
                Btn("Remove item  [Del]", () => RemoveSelectedLine()));
            var rowB = TwoUp(
                Btn("Hold  [F2]", () => HoldCurrent()),
                Btn("Recall  [F3]", () => RecallHeld()));
            var rowC = TwoUp(
                Btn("Weigh  [F4]", () => WeighSelected()),
                Btn("Clear bill  [Esc]", () => ClearBill(true)));

            var btnCancel = Theme.SecondaryButton("Cancel a past bill  (manager PIN)");
            btnCancel.Dock = DockStyle.Top;
            btnCancel.ForeColor = Theme.Danger;
            btnCancel.FlatAppearance.BorderColor = Theme.Danger;
            btnCancel.Click += (s, e) => CancelBill();

            // Docked children stack in reverse order of addition.
            actions.Controls.Add(btnCancel);
            actions.Controls.Add(rowC);
            actions.Controls.Add(rowB);
            actions.Controls.Add(rowA);
            actions.Controls.Add(gap);
            actions.Controls.Add(btnPay);

            var totals = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface };

            _lblNet = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 54,                       // explicit, so a 20pt face always fits
                Text = "Rs. 0.00",
                Font = Theme.DataLarge,
                ForeColor = Theme.Primary,
                TextAlign = ContentAlignment.MiddleRight,
                AutoSize = false
            };
            var netCaption = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 20,
                Text = "NET PAYABLE",
                Font = Theme.Label,
                ForeColor = Theme.Muted,
                TextAlign = ContentAlignment.MiddleRight
            };
            var rule = new Panel { Dock = DockStyle.Bottom, Height = 2, BackColor = Theme.Primary, Margin = new Padding(0, Theme.Sm, 0, 0) };

            _lblTax = new Label
            {
                Dock = DockStyle.Top,
                Height = 44,
                Text = "Tax: 0.00",
                Font = Theme.Data,
                ForeColor = Theme.Muted,
                TextAlign = ContentAlignment.TopLeft
            };
            _lblSubtotal = new Label
            {
                Dock = DockStyle.Top,
                Height = 26,
                Text = "Subtotal: 0.00",
                Font = Theme.Data,
                ForeColor = Theme.OnSurface,
                TextAlign = ContentAlignment.MiddleLeft
            };
            var title = new Label
            {
                Dock = DockStyle.Top,
                Height = 30,
                Text = "Bill summary",
                Font = Theme.Headline,
                TextAlign = ContentAlignment.MiddleLeft
            };

            // The net block is one unit pinned to the bottom: rule, caption, figure.
            // Building it as its own panel avoids relying on docked stacking order.
            var netBlock = new Panel { Dock = DockStyle.Bottom, Height = 80, BackColor = Theme.Surface };
            rule.Dock = DockStyle.Top;
            netCaption.Dock = DockStyle.Top;
            _lblNet.Dock = DockStyle.Fill;
            netBlock.Controls.Add(_lblNet);
            netBlock.Controls.Add(netCaption);
            netBlock.Controls.Add(rule);

            totals.Controls.Add(_lblTax);
            totals.Controls.Add(_lblSubtotal);
            totals.Controls.Add(title);
            totals.Controls.Add(netBlock);

            right.Controls.Add(totals);
            right.Controls.Add(actions);
            return right;
        }

        private Button Btn(string text, Action onClick)
        {
            var b = Theme.SecondaryButton(text);
            b.Click += (s, e) => onClick();
            return b;
        }

        /// <summary>Two equal buttons side by side, resizing with the panel.</summary>
        private static Panel TwoUp(Button left, Button right)
        {
            var p = new Panel { Dock = DockStyle.Top, Height = Theme.ButtonHeight + Theme.Xs, BackColor = Theme.Surface };
            left.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            right.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            p.Controls.Add(left);
            p.Controls.Add(right);
            EventHandler place = (s, e) =>
            {
                int half = (p.ClientSize.Width - Theme.Xs) / 2;
                left.SetBounds(0, 0, half, Theme.ButtonHeight);
                right.SetBounds(half + Theme.Xs, 0, p.ClientSize.Width - half - Theme.Xs, Theme.ButtonHeight);
            };
            p.Resize += place;
            place(null, EventArgs.Empty);
            return p;
        }

        private Panel BuildHeldPanel()
        {
            var p = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 104,
                BackColor = Theme.Surface,
                Padding = new Padding(Theme.Md, Theme.Sm, Theme.Md, Theme.Sm)
            };
            p.Controls.Add(new Label
            {
                Text = "Bills on hold  -  press F3 to bring one back",
                Dock = DockStyle.Top,
                Height = 20,
                Font = Theme.Label,
                ForeColor = Theme.Muted,
                TextAlign = ContentAlignment.MiddleLeft
            });
            _heldList = new ListBox
            {
                Dock = DockStyle.Fill,
                Font = Theme.Data,
                BorderStyle = BorderStyle.FixedSingle,
                IntegralHeight = false
            };
            p.Controls.Add(_heldList);
            _heldList.BringToFront();
            return p;
        }

        /// <summary>The always-visible key legend, as in the design's footer strip.</summary>
        private Panel BuildKeyHintBar()
        {
            var p = new Panel { Dock = DockStyle.Bottom, Height = 26, BackColor = Theme.Primary };
            p.Controls.Add(new Label
            {
                Text = "  [F2] Hold    [F3] Recall    [F4] Weigh    [F5] Discount    "
                     + "[F9] Pay    [Del] Remove item    [Esc] Clear whole bill",
                Dock = DockStyle.Fill,
                Font = Theme.Data,
                ForeColor = Color.FromArgb(200, 216, 240),
                TextAlign = ContentAlignment.MiddleLeft
            });
            return p;
        }

        private void EnsureShiftOpen()
        {
            int counterId;
            if (!int.TryParse(_ctx.Settings.Get("counter_id", "1"), out counterId)) counterId = 1;
            var open = _ctx.Shifts.OpenShiftFor(counterId);
            if (open != null)
            {
                _currentShiftId = open.Id;
                UpdateShiftLabel(open.Id, false);
                return;
            }

            var ask = MessageBox.Show(
                "No shift is open for counter " + counterId + ".\n\n" +
                "Cash sales must belong to a shift so the day-close cash count works.\n\n" +
                "Open a shift now?",
                "Shift not open", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (ask != DialogResult.Yes)
            {
                UpdateShiftLabel(null, true);
                return;
            }

            string input = Microsoft.VisualBasic.Interaction.InputBox(
                "Opening cash float in the drawer (rupees):", "Open shift", "0");
            if (string.IsNullOrWhiteSpace(input)) { UpdateShiftLabel(null, true); return; }
            long rupees;
            if (!long.TryParse(input.Trim(), out rupees) || rupees < 0)
            {
                MessageBox.Show("Invalid amount. Shift not opened.");
                UpdateShiftLabel(null, true);
                return;
            }
            var shift = _ctx.Shifts.Open(counterId, _ctx.CurrentUser.Id, rupees * 100L);
            _currentShiftId = shift.Id;
            UpdateShiftLabel(shift.Id, false);
        }

        private void UpdateShiftLabel(long? shiftId, bool warn)
        {
            string current = _lblCustomer.Text;
            // Reuse the top area — append shift status to the title bar for visibility.
            Text = "Billing counter" +
                   (shiftId.HasValue ? "  |  Shift #" + shiftId.Value + " OPEN" : "  |  NO SHIFT OPEN") +
                   "  —  F2 hold  F3 recall  F4 weigh  F5 discount  F9 pay  Del remove  Esc clear";
            if (warn)
                MessageBox.Show("Sales will still work, but they will NOT be tied to a shift. " +
                                "Open one from Main Menu → Shift when convenient.",
                                "Shift not open", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void UpdateScaleLabel()
        {
            _lblScale.Text = "Scale: " + (_ctx.WeightSource == null ? "manual" : _ctx.WeightSource.Mode.ToString());
        }

        private void BillingForm_KeyDown(object sender, KeyEventArgs e)
        {
            // While a cell is being typed into, Del and Esc belong to the editor:
            // Del removes a character and Esc cancels the edit. Letting them
            // through here would delete the line, or throw away the whole bill,
            // in the middle of correcting a quantity.
            bool editingCell = _grid != null && _grid.IsCurrentCellInEditMode;

            if (e.KeyCode == Keys.F2) { HoldCurrent(); e.Handled = true; }
            else if (e.KeyCode == Keys.F3) { RecallHeld(); e.Handled = true; }
            else if (e.KeyCode == Keys.F4) { WeighSelected(); e.Handled = true; }
            else if (e.KeyCode == Keys.F5) { ApplyDiscount(); e.Handled = true; }
            else if (e.KeyCode == Keys.F9)
            {
                // Commit whatever is half-typed before totalling the bill.
                if (editingCell) _grid.EndEdit();
                OpenPayment();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Delete && _grid.Focused && !editingCell)
            {
                RemoveSelectedLine(); e.Handled = true;
            }
            else if (e.KeyCode == Keys.Escape && !editingCell)
            {
                ClearBill(true); e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.K) { LookupCustomer(); e.Handled = true; }
        }

        private void Scan_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            string text = _scan.Text.Trim();
            _scan.Clear();
            if (text.Length == 0) return;

            // Weight barcode 20-29 prefix (13 digits, digit)
            if (text.Length == 13 && text[0] == '2' && text.All(char.IsDigit))
            {
                var p = _wbParser.Parse(text);
                if (p != null)
                {
                    var itByCode = _ctx.Items.FindBySku(p.ItemCode) ?? _ctx.Items.FindByBarcode(text);
                    if (itByCode != null)
                    {
                        AddItemLine(itByCode, 0, p.Grams, WeightSource.Label, p.Grams);
                    }
                    else MessageBox.Show("Weight barcode item not found: " + p.ItemCode);
                    _scan.Focus();
                    return;
                }
            }

            // Try exact barcode
            var it = _ctx.Items.FindByBarcode(text) ?? _ctx.Items.FindBySku(text);
            if (it != null)
            {
                AddByItem(it);
                _scan.Focus();
                return;
            }

            // Name search
            var results = _ctx.Items.Search(text, 20);
            if (results.Count == 1) AddByItem(results[0]);
            else if (results.Count == 0) MessageBox.Show("No item matches '" + text + "'");
            else
            {
                using (var pick = new ItemPickerForm(results))
                {
                    if (pick.ShowDialog(this) == DialogResult.OK && pick.Chosen != null)
                        AddByItem(pick.Chosen);
                }
            }
            _scan.Focus();
        }

        private void AddByItem(Item it)
        {
            if (it.SoldBy == SoldBy.Weight)
            {
                int grams = PromptWeight(it);
                if (grams <= 0) return;
                AddItemLine(it, 0, grams, _ctx.WeightSource != null && _ctx.WeightSource.Mode == WeightMode.Serial ? WeightSource.Scale : WeightSource.Manual, grams);
            }
            else
            {
                AddItemLine(it, 1, 0, WeightSource.Na, 0);
            }
        }

        /// <summary>
        /// Prompt for weight. Uses live scale reading (F4) if serial mode has a stable reading;
        /// otherwise opens the manual entry dialog which enforces 100g min and item's rounding.
        /// </summary>
        private int PromptWeight(Item it)
        {
            var src = _ctx.WeightSource;
            if (src != null && src.Mode == WeightMode.Serial && src.Latest.HasValue && src.Latest.Value.Stable)
            {
                int g = src.Latest.Value.Grams - it.TareGrams;
                int rounded = new Grams(g).RoundToStep(it.RoundToGrams > 0 ? it.RoundToGrams : 5).Value;
                if (rounded < it.MinSaleGrams)
                {
                    MessageBox.Show("Weight below minimum sale (" + it.MinSaleGrams + "g)");
                    return 0;
                }
                return rounded;
            }
            using (var m = new ManualWeightDialog(it))
            {
                if (m.ShowDialog(this) == DialogResult.OK) return m.Grams;
                return 0;
            }
        }

        private void AddItemLine(Item it, int units, int grams, WeightSource ws, int rawGrams)
        {
            // Get current selling price from most recent batch, fallback to 0.
            long rate = 0;
            long? batchId = null;
            string batchInfo = null;
            using (var c = _ctx.Db.Open())
            {
                var b = c.QueryFirstOrDefault<BatchPick>(
                    @"SELECT id AS Id, selling_paise AS SellingPaise, mrp_paise AS MrpPaise,
                             batch_code AS BatchCode, expiry_date AS ExpiryDate
                      FROM batches
                      WHERE item_id=@i AND (qty_units>0 OR qty_grams>0)
                      ORDER BY (expiry_date IS NULL) ASC, expiry_date ASC, mrp_paise ASC
                      LIMIT 1",
                    new { i = it.Id });
                if (b != null)
                {
                    rate = b.SellingPaise;
                    batchId = b.Id;
                    batchInfo = b.BatchCode;
                }
            }
            // Fall back to the item's default selling price if no batch is available yet.
            if (rate == 0) rate = it.DefaultSellingPaise;
            if (rate == 0)
            {
                using (var r = new RateEntryDialog(it)) { if (r.ShowDialog(this) == DialogResult.OK) rate = r.Paise; else return; }
            }
            // Scanning the same packet twice should read "2", not print two lines.
            // Only piece-sold goods merge: two separate weighings are genuinely
            // two different weights and must stay on their own lines so the
            // customer can see what each one was.
            if (units > 0 && grams == 0)
            {
                var existing = _bill.Lines.FirstOrDefault(l =>
                    l.ItemId == it.Id &&
                    l.BatchId == batchId &&
                    l.QtyGrams == 0 &&
                    l.RatePaise == rate &&
                    l.DiscountPaise == 0);

                if (existing != null)
                {
                    existing.QtyUnits += units;
                    RefreshView();
                    SelectLine(_bill.Lines.IndexOf(existing));
                    return;
                }
            }

            var line = new BillLine
            {
                LineNo = _bill.Lines.Count + 1,
                ItemId = it.Id,
                BatchId = batchId,
                QtyUnits = units,
                QtyGrams = grams,
                WeightSource = ws,
                RawGrams = rawGrams,
                RatePaise = rate,
                TaxRateBp = it.TaxRateBp,
                HsnCode = it.HsnCode,
                ItemName = it.PrintName ?? it.Name
            };
            _bill.Lines.Add(line);
            RefreshView();
            SelectLine(_bill.Lines.Count - 1);
        }

        private void RefreshView()
        {
            BillCalculator.ComputeBill(_bill);
            _linesView.Clear();
            foreach (var l in _bill.Lines)
                _linesView.Add(new BillLineView
                {
                    LineNo = l.LineNo, ItemName = l.ItemName,
                    Qty = l.QtyGrams > 0 ? new Grams(l.QtyGrams).ToString() : l.QtyUnits.ToString(),
                    Rate = new Money(l.RatePaise).ToString(),
                    Disc = new Money(l.DiscountPaise).ToString(),
                    Amount = new Money(l.AmountPaise).ToString()
                });
            _lblSubtotal.Text = "Subtotal            Rs. " + new Money(_bill.TaxablePaise);
            _lblTax.Text = "CGST + SGST         Rs. " + new Money(_bill.CgstPaise + _bill.SgstPaise)
                           + Environment.NewLine + "Round-off           Rs. " + new Money(_bill.RoundOffPaise);
            _lblNet.Text = "Rs. " + new Money(_bill.NetPaise) + "  ";
            UpdateEmptyBill();
        }

        /// <summary>
        /// Takes the selected line off the bill. If the line holds several of the
        /// same item, ask whether to drop one or the lot -- with merged lines a
        /// silent "remove all" would quietly delete goods the customer is buying.
        /// </summary>
        private void RemoveSelectedLine()
        {
            if (_grid.CurrentRow == null)
            {
                if (_bill.Lines.Count > 0)
                    Theme.Warn("Click the line you want to remove first.");
                return;
            }

            int idx = _grid.CurrentRow.Index;
            if (idx < 0 || idx >= _bill.Lines.Count) return;

            var line = _bill.Lines[idx];

            // A merged line of, say, 5 packets: usually one was scanned by
            // mistake, not all five.
            if (line.QtyUnits > 1)
            {
                using (var ask = new RemoveQuantityDialog(line.ItemName, line.QtyUnits))
                {
                    if (ask.ShowDialog(this) != DialogResult.OK) { _scan.Focus(); return; }

                    if (ask.RemoveAll || ask.Quantity >= line.QtyUnits)
                    {
                        _bill.Lines.RemoveAt(idx);
                    }
                    else
                    {
                        line.QtyUnits -= ask.Quantity;
                    }
                }
            }
            else
            {
                _bill.Lines.RemoveAt(idx);
            }

            for (int i = 0; i < _bill.Lines.Count; i++) _bill.Lines[i].LineNo = i + 1;
            RefreshView();
            _scan.Focus();
        }

        private void ApplyDiscount()
        {
            if (_grid.CurrentRow == null) { MessageBox.Show("Select a line to discount"); return; }
            int idx = _grid.CurrentRow.Index;
            var line = _bill.Lines[idx];
            using (var d = new DiscountDialog(line, _ctx))
            {
                if (d.ShowDialog(this) == DialogResult.OK)
                {
                    line.DiscountPaise = d.DiscountPaise;
                    RefreshView();
                }
            }
            _scan.Focus();
        }

        private void HoldCurrent()
        {
            if (_bill.Lines.Count == 0) return;
            _held.Add(_bill);
            _heldList.Items.Add("Held @ " + DateTime.Now.ToString("HH:mm:ss") + " — " + _bill.Lines.Count + " items, Rs. " + new Money(_bill.NetPaise).ToString());
            ClearBill(false);
            _scan.Focus();
        }

        private void RecallHeld()
        {
            if (_held.Count == 0) return;
            int idx = _heldList.SelectedIndex >= 0 ? _heldList.SelectedIndex : _held.Count - 1;
            var b = _held[idx];
            _held.RemoveAt(idx);
            _heldList.Items.RemoveAt(idx);
            _bill = b;
            RefreshView();
            _scan.Focus();
        }

        private void ClearBill(bool ask)
        {
            if (ask && _bill != null && _bill.Lines.Count > 0)
                if (MessageBox.Show("Clear current bill?", "Confirm", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            _bill = new Bill { Lines = new List<BillLine>(), Payments = new List<Payment>() };
            _customer = null;
            _lblCustomer.Text = "Customer: (walk-in) [Ctrl+K]";
            RefreshView();
            _scan.Focus();
        }

        private void WeighSelected()
        {
            if (_grid.CurrentRow == null) { MessageBox.Show("Select a weight line first"); return; }
            int idx = _grid.CurrentRow.Index;
            var line = _bill.Lines[idx];
            var item = _ctx.Items.FindById(line.ItemId);
            if (item == null || item.SoldBy != SoldBy.Weight) { MessageBox.Show("Not a weight item"); return; }

            int rawGrams = 0;
            bool fromScale = false;
            var src = _ctx.WeightSource;
            if (src != null && src.Mode == WeightMode.Serial)
            {
                var reading = src.Latest;
                if (!reading.HasValue)
                {
                    MessageBox.Show("Scale connected but no reading yet. Put the item on the pan and try again.");
                    return;
                }
                if (!reading.Value.Stable)
                {
                    MessageBox.Show("Scale reading not stable yet. Wait for it to settle and try again.");
                    return;
                }
                rawGrams = reading.Value.Grams;
                fromScale = true;
            }
            else
            {
                rawGrams = PromptWeight(item);
                if (rawGrams <= 0) return;
            }

            int rounded = item.RoundToGrams > 0
                ? new Grams(rawGrams).RoundToStep(item.RoundToGrams).Value
                : rawGrams;
            int minSale = item.MinSaleGrams > 0 ? item.MinSaleGrams : 100;
            if (rounded < minSale)
            {
                MessageBox.Show("Weight " + new Grams(rounded) + " is below the minimum sale weight of "
                                + new Grams(minSale) + " for this item.");
                return;
            }
            line.QtyGrams = rounded;
            line.RawGrams = rawGrams;
            line.WeightSource = fromScale ? WeightSource.Scale : WeightSource.Manual;
            RefreshView();
            _scan.Focus();
        }

        private void LookupCustomer()
        {
            if (_ctx.Customers == null) { MessageBox.Show("Customer repo not initialised"); return; }
            using (var f = new CustomerLookupForm(_ctx))
            {
                if (f.ShowDialog(this) == DialogResult.OK && f.Chosen != null)
                {
                    _customer = f.Chosen;
                    _lblCustomer.Text = "Customer: " + _customer.Name + " (" + _customer.Phone + ")  Balance: " + new Money(_customer.CurrentBalancePaise).ToString() + "  Pts: " + _customer.LoyaltyPoints;
                }
            }
            _scan.Focus();
        }

        private void OpenPayment()
        {
            if (_bill.Lines.Count == 0) { MessageBox.Show("Empty bill"); return; }
            RefreshView();
            using (var pay = new PaymentForm(_ctx, _bill, _customer))
            {
                if (pay.ShowDialog(this) != DialogResult.OK) { _scan.Focus(); return; }

                // Credit limit check
                if (pay.Payments.Any(p => p.Mode == PaymentMode.Khata))
                {
                    if (_customer == null) { MessageBox.Show("Khata requires a customer"); return; }
                    if (!_customer.CreditAllowed) { MessageBox.Show("Credit not enabled for this customer"); return; }
                    long khata = pay.Payments.Where(p => p.Mode == PaymentMode.Khata).Sum(p => p.AmountPaise);
                    long newBal = _customer.CurrentBalancePaise + khata;
                    if (newBal > _customer.CreditLimitPaise)
                    {
                        string reason;
                        long? authoriser = ManagerOverrideDialog.Prompt(_ctx,
                            "Credit limit exceeded.\nBalance: " + new Money(_customer.CurrentBalancePaise) +
                            "\nThis bill: " + new Money(khata) +
                            "\nLimit: " + new Money(_customer.CreditLimitPaise) +
                            "\nShortfall: " + new Money(newBal - _customer.CreditLimitPaise) +
                            "\n\nManager PIN to allow:", out reason);
                        _ctx.CreditLimits.LogEvent(_customer.Id, authoriser.HasValue ? "override_allowed" : "override_refused",
                            _customer.CreditLimitPaise, _customer.CreditLimitPaise, null, khata, _customer.CurrentBalancePaise,
                            reason, authoriser, _ctx.CurrentUser.Id);
                        if (!authoriser.HasValue) { MessageBox.Show("Override refused"); return; }
                    }
                }

                int loyaltyRate;
                if (!int.TryParse(_ctx.Settings.Get("loyalty_points_per_100rupees", "1"), out loyaltyRate)) loyaltyRate = 1;
                int counterId;
                if (!int.TryParse(_ctx.Settings.Get("counter_id", "1"), out counterId)) counterId = 1;

                long prevBal = _customer == null ? 0 : _customer.CurrentBalancePaise;
                _ctx.Bills.Save(_bill, pay.Payments, _ctx.CurrentUser.Id, counterId, _customer, _currentShiftId, loyaltyRate);

                // Refresh customer for updated balance
                if (_customer != null) _customer = _ctx.Customers.FindById(_customer.Id);

                // Print
                TryPrint(_bill, prevBal);

                MessageBox.Show("Bill INV-" + _bill.BillNo + " saved. Net Rs. " + new Money(_bill.NetPaise));
                ClearBill(false);
            }
        }

        private void TryPrint(Bill b, long prevBalPaise)
        {
            try
            {
                var store = new ReceiptFormatter.StoreInfo
                {
                    Name = _ctx.Settings.Get("store_name", "GROCERY STORE"),
                    Address1 = _ctx.Settings.Get("store_address_1", ""),
                    Address2 = _ctx.Settings.Get("store_address_2", ""),
                    Phone = _ctx.Settings.Get("store_phone", ""),
                    Gstin = _ctx.Settings.Get("store_gstin", ""),
                    Footer = _ctx.Settings.Get("store_footer", ""),
                    TitleNoGst = _ctx.Settings.Get("receipt_title_no_gst", "CASH BILL"),
                    CounterId = b.CounterId
                };
                var names = new Dictionary<long, string>();
                foreach (var l in b.Lines) names[l.ItemId] = l.ItemName ?? "";
                var fmt = new ReceiptFormatter();
                Money? prev = _customer != null && b.IsCreditSale ? (Money?)new Money(prevBalPaise) : null;
                Money? cur = _customer != null && b.IsCreditSale ? (Money?)new Money(_customer.CurrentBalancePaise) : null;

                ReceiptFormatter.LoyaltyBlock loyalty = null;
                if (_customer != null)
                {
                    int loyaltyRate;
                    int.TryParse(_ctx.Settings.Get("loyalty_points_per_100rupees", "1"), out loyaltyRate);
                    long pointsEarned = (b.NetPaise / 10000L) * loyaltyRate;
                    loyalty = new ReceiptFormatter.LoyaltyBlock
                    {
                        CustomerName = _customer.Name,
                        CustomerPhone = _customer.Phone,
                        PointsEarnedThisBill = pointsEarned,
                        PointsBalance = _customer.LoyaltyPoints
                    };
                }
                var richLines = fmt.FormatRich(store, b, _ctx.CurrentUser.Name, names, false, prev, cur, loyalty);
                var payments = b.Payments != null && b.Payments.Any(p => p.Mode == PaymentMode.Cash);
                int drawerPin;
                int.TryParse(_ctx.Settings.Get("drawer_pin", "0"), out drawerPin);
                bool drawerEnabled = _ctx.Settings.Get("drawer_enabled", "0") == "1";
                bool kick = drawerEnabled && payments;
                var bytes = EscPos.Build(richLines, cut: true, drawerKick: kick, drawerPin: drawerPin);
                string queue = _ctx.Settings.Get("printer_name", "");
                if (string.IsNullOrWhiteSpace(queue))
                {
                    // No printer configured: show preview
                    using (var pf = new Form { Text = "Receipt preview (no printer configured)", Width = 500, Height = 600 })
                    {
                        var previewText = string.Join(Environment.NewLine, richLines.Select(rl => rl.Text));
                        pf.Controls.Add(new TextBox { Multiline = true, Dock = DockStyle.Fill, Font = new Font("Consolas", 9),
                            ScrollBars = ScrollBars.Vertical, Text = previewText, ReadOnly = true });
                        pf.ShowDialog();
                    }
                    return;
                }
                _ctx.Printer.Print(queue, bytes);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Print failed (bill saved): " + ex.Message);
            }
        }

        private void CancelBill()
        {
            string billNoStr = Prompt("Bill number to cancel:");
            if (string.IsNullOrEmpty(billNoStr)) return;
            long billNo;
            if (!long.TryParse(billNoStr, out billNo)) { MessageBox.Show("Invalid bill number"); return; }
            var b = _ctx.Bills.FindByBillNo(billNo);
            if (b == null) { MessageBox.Show("Bill not found"); return; }
            if (b.Status == BillStatus.Cancelled) { MessageBox.Show("Already cancelled"); return; }
            string reason;
            long? auth = ManagerOverrideDialog.Prompt(_ctx, "Cancel INV-" + billNo + "\nNet Rs. " + new Money(b.NetPaise) + "\nManager or owner PIN:", out reason);
            if (!auth.HasValue) return;
            if (string.IsNullOrWhiteSpace(reason)) { MessageBox.Show("Reason required"); return; }
            try
            {
                _ctx.Bills.Cancel(b.Id, auth.Value, reason);
                MessageBox.Show("Bill INV-" + billNo + " cancelled");
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private static string Prompt(string caption)
        {
            using (var f = new Form { Text = caption, Width = 320, Height = 150, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false })
            {
                var t = new TextBox { Left = 12, Top = 12, Width = 280 };
                var ok = new Button { Text = "OK", Left = 130, Top = 60, Width = 80, DialogResult = DialogResult.OK };
                var cancel = new Button { Text = "Cancel", Left = 212, Top = 60, Width = 80, DialogResult = DialogResult.Cancel };
                f.Controls.AddRange(new Control[] { t, ok, cancel });
                f.AcceptButton = ok; f.CancelButton = cancel;
                return f.ShowDialog() == DialogResult.OK ? t.Text.Trim() : null;
            }
        }
    }

    public class BillLineView
    {
        public int LineNo { get; set; }
        public string ItemName { get; set; }
        public string Qty { get; set; }
        public string Rate { get; set; }
        public string Disc { get; set; }
        public string Amount { get; set; }
    }

    public class ItemPickerForm : Form
    {
        public Item Chosen { get; private set; }
        public ItemPickerForm(IList<Item> items)
        {
            Text = "Pick item"; Width = 500; Height = 400; StartPosition = FormStartPosition.CenterParent;
            var lb = new ListBox { Dock = DockStyle.Fill };
            foreach (var it in items) lb.Items.Add(it.Name + "  [" + it.Sku + "]");
            var ok = new Button { Text = "Select", Dock = DockStyle.Bottom, Height = 36 };
            ok.Click += (s, e) => { if (lb.SelectedIndex >= 0) { Chosen = items[lb.SelectedIndex]; DialogResult = DialogResult.OK; Close(); } };
            lb.DoubleClick += (s, e) => { if (lb.SelectedIndex >= 0) { Chosen = items[lb.SelectedIndex]; DialogResult = DialogResult.OK; Close(); } };
            Controls.Add(lb); Controls.Add(ok);
            Theme.Retrofit(this);
        }
    }

    public class RateEntryDialog : Form
    {
        public long Paise;
        public RateEntryDialog(Item it)
        {
            Text = "Enter rate for " + it.Name; Width = 320; Height = 160; StartPosition = FormStartPosition.CenterParent;
            var l = new Label { Text = "Rate (rupees" + (it.SoldBy == SoldBy.Weight ? " per kg" : "") + "):", Left = 12, Top = 12, Width = 280 };
            var t = new TextBox { Left = 12, Top = 40, Width = 280 };
            var ok = new Button { Text = "OK", Left = 130, Top = 80, Width = 80 };
            ok.Click += (s, e) => { try { Paise = Money.ParseRupees(t.Text).Paise; DialogResult = DialogResult.OK; Close(); } catch (Exception ex) { MessageBox.Show(ex.Message); } };
            AcceptButton = ok;
            Controls.AddRange(new Control[] { l, t, ok });
            Theme.Retrofit(this);
        }
    }

    public class DiscountDialog : Form
    {
        public long DiscountPaise;
        public DiscountDialog(BillLine line, AppContext ctx)
        {
            Text = "Discount"; Width = 340; Height = 200; StartPosition = FormStartPosition.CenterParent;
            long baseAmt = line.QtyGrams > 0
                ? (line.RatePaise * (long)line.QtyGrams + 500L) / 1000L
                : line.RatePaise * (long)line.QtyUnits;
            var l = new Label { Text = "Line base: " + new Money(baseAmt), Left = 12, Top = 12, Width = 300 };
            var t = new TextBox { Left = 12, Top = 40, Width = 300, Text = new Money(line.DiscountPaise).ToString() };
            int cap;
            if (!int.TryParse(ctx.Settings.Get("discount_cap_percent", "5"), out cap)) cap = 5;
            var l2 = new Label { Text = "Cap: " + cap + "%  (above requires manager PIN)", Left = 12, Top = 66, Width = 300, ForeColor = Color.Gray };
            var ok = new Button { Text = "OK", Left = 150, Top = 100, Width = 80 };
            ok.Click += (s, e) => {
                try
                {
                    long amt = Money.ParseRupees(t.Text).Paise;
                    if (amt < 0 || amt > baseAmt) { MessageBox.Show("Invalid discount"); return; }
                    long capAmt = baseAmt * cap / 100L;
                    if (amt > capAmt)
                    {
                        string reason;
                        long? auth = ManagerOverrideDialog.Prompt(ctx, "Discount " + new Money(amt) + " exceeds cap " + new Money(capAmt) + "\nManager PIN:", out reason);
                        if (!auth.HasValue) return;
                        ctx.Audit.Write(auth.Value, "discount_override", "bill_line", null, null, new { amt = amt, cap = capAmt, reason });
                    }
                    DiscountPaise = amt;
                    DialogResult = DialogResult.OK;
                    Close();
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
            };
            AcceptButton = ok;
            Controls.AddRange(new Control[] { l, t, l2, ok });
            Theme.Retrofit(this);
        }
    }

    public class PaymentForm : Form
    {
        public List<Payment> Payments = new List<Payment>();
        private readonly Bill _bill;
        private readonly Customer _cust;
        private TextBox _cash, _upi, _upiRef, _card, _cardRef, _khata;
        private Label _change;

        public PaymentForm(AppContext ctx, Bill bill, Customer cust)
        {
            _bill = bill; _cust = cust;
            Text = "Payment — Net Rs. " + new Money(bill.NetPaise);
            Width = 420; Height = 380; StartPosition = FormStartPosition.CenterParent;
            int y = 12;
            AddRow("Cash", out _cash, ref y, "0");
            AddRow("UPI", out _upi, ref y, "0"); _upiRef = AddRef(ref y);
            AddRow("Card", out _card, ref y, "0"); _cardRef = AddRef(ref y);
            AddRow("Khata (credit)", out _khata, ref y, "0");
            _change = new Label { Left = 12, Top = y, Width = 380, Text = "Change: 0.00", Font = new Font("Segoe UI", 11, FontStyle.Bold) };
            Controls.Add(_change); y += 30;
            var ok = new Button { Text = "Confirm", Left = 200, Top = y, Width = 100, DialogResult = DialogResult.None };
            ok.Click += (s, e) => Confirm();
            var cancel = new Button { Text = "Cancel", Left = 305, Top = y, Width = 80, DialogResult = DialogResult.Cancel };
            Controls.Add(ok); Controls.Add(cancel);
            AcceptButton = ok; CancelButton = cancel;
            _cash.TextChanged += (s, e) => Recalc(); _upi.TextChanged += (s, e) => Recalc();
            _card.TextChanged += (s, e) => Recalc(); _khata.TextChanged += (s, e) => Recalc();
            _cash.Text = new Money(bill.NetPaise).ToString();
            _cash.SelectAll(); _cash.Focus();
            Theme.Retrofit(this);
        }

        private void AddRow(string label, out TextBox tb, ref int y, string def)
        {
            var l = new Label { Text = label, Left = 12, Top = y + 4, Width = 120 };
            tb = new TextBox { Left = 140, Top = y, Width = 120, Text = def };
            Controls.Add(l); Controls.Add(tb); y += 30;
        }

        private TextBox AddRef(ref int y)
        {
            var l = new Label { Text = "   Ref:", Left = 12, Top = y + 4, Width = 60 };
            var tb = new TextBox { Left = 140, Top = y, Width = 240 };
            Controls.Add(l); Controls.Add(tb); y += 26;
            return tb;
        }

        private void Recalc()
        {
            long total = Try(_cash) + Try(_upi) + Try(_card) + Try(_khata);
            long diff = total - _bill.NetPaise;
            _change.Text = diff >= 0 ? "Change: " + new Money(diff) : "Short: " + new Money(-diff);
        }

        private long Try(TextBox t)
        {
            try { return string.IsNullOrWhiteSpace(t.Text) ? 0 : Money.ParseRupees(t.Text).Paise; } catch { return 0; }
        }

        private void Confirm()
        {
            long cash = Try(_cash), upi = Try(_upi), card = Try(_card), khata = Try(_khata);
            long nonCash = upi + card + khata;
            // Cash may overpay (change); non-cash modes must be exact for their portion.
            long needFromCash = _bill.NetPaise - nonCash;
            if (needFromCash < 0)
            {
                MessageBox.Show("Non-cash payments exceed bill total"); return;
            }
            if (cash < needFromCash)
            {
                MessageBox.Show("Cash short by " + new Money(needFromCash - cash)); return;
            }
            long cashApplied = needFromCash; // change = cash - cashApplied
            Payments.Clear();
            if (cashApplied > 0) Payments.Add(new Payment { Mode = PaymentMode.Cash, AmountPaise = cashApplied });
            if (upi > 0) Payments.Add(new Payment { Mode = PaymentMode.Upi, AmountPaise = upi, Reference = _upiRef.Text });
            if (card > 0) Payments.Add(new Payment { Mode = PaymentMode.Card, AmountPaise = card, Reference = _cardRef.Text });
            if (khata > 0) Payments.Add(new Payment { Mode = PaymentMode.Khata, AmountPaise = khata });
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    public class ManagerOverrideDialog : Form
    {
        private readonly AppContext _ctx;
        private TextBox _user, _pin, _reason;
        private long? _authoriser;
        public string ReasonText { get; private set; }

        public static long? Prompt(AppContext ctx, string prompt, out string reason)
        {
            using (var f = new ManagerOverrideDialog(ctx, prompt))
            {
                if (f.ShowDialog() == DialogResult.OK) { reason = f.ReasonText; return f._authoriser; }
                reason = f.ReasonText; return null;
            }
        }

        public ManagerOverrideDialog(AppContext ctx, string prompt)
        {
            _ctx = ctx;
            Text = "Manager authorisation";
            Width = 420; Height = 300; StartPosition = FormStartPosition.CenterParent;
            var l = new Label { Text = prompt, Left = 12, Top = 12, Width = 380, Height = 100 };
            var l1 = new Label { Text = "User:", Left = 12, Top = 120, Width = 60 };
            _user = new TextBox { Left = 80, Top = 118, Width = 200 };
            var l2 = new Label { Text = "PIN:", Left = 12, Top = 150, Width = 60 };
            _pin = new TextBox { Left = 80, Top = 148, Width = 200, UseSystemPasswordChar = true };
            var l3 = new Label { Text = "Reason:", Left = 12, Top = 180, Width = 60 };
            _reason = new TextBox { Left = 80, Top = 178, Width = 300 };
            var ok = new Button { Text = "Authorise", Left = 200, Top = 220, Width = 100 };
            ok.Click += (s, e) => Try();
            var cancel = new Button { Text = "Refuse", Left = 305, Top = 220, Width = 80, DialogResult = DialogResult.Cancel };
            AcceptButton = ok; CancelButton = cancel;
            Controls.AddRange(new Control[] { l, l1, _user, l2, _pin, l3, _reason, ok, cancel });
            Theme.Retrofit(this);
        }

        private void Try()
        {
            ReasonText = _reason.Text.Trim();
            var u = _ctx.Users.FindByName(_user.Text.Trim());
            if (u == null || !_ctx.Users.VerifyPin(u, _pin.Text)) { MessageBox.Show("Bad credentials"); return; }
            if (u.Role != UserRole.Manager && u.Role != UserRole.Owner) { MessageBox.Show("Not authorised"); return; }
            _authoriser = u.Id;
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    internal class BatchPick
    {
        public long Id { get; set; }
        public long SellingPaise { get; set; }
        public long MrpPaise { get; set; }
        public string BatchCode { get; set; }
        public string ExpiryDate { get; set; }
    }

    public class CustomerLookupForm : Form
    {
        public Customer Chosen { get; private set; }
        private readonly AppContext _ctx;
        private TextBox _search;
        private ListBox _list;
        private IList<Customer> _current;

        public CustomerLookupForm(AppContext ctx)
        {
            _ctx = ctx;
            Text = "Customer lookup"; Width = 560; Height = 460;
            StartPosition = FormStartPosition.CenterParent;
            _search = new TextBox { Left = 12, Top = 12, Width = 520 };
            _list = new ListBox { Left = 12, Top = 44, Width = 520, Height = 320 };
            var ok = new Button { Text = "Select (Enter)", Left = 12, Top = 372, Width = 140, DialogResult = DialogResult.None };
            var newBtn = new Button { Text = "New customer", Left = 160, Top = 372, Width = 140 };
            var cancel = new Button { Text = "Cancel", Left = 440, Top = 372, Width = 92, DialogResult = DialogResult.Cancel };
            ok.Click += (s, e) => Pick();
            newBtn.Click += (s, e) => CreateNew();
            _list.DoubleClick += (s, e) => Pick();
            _search.TextChanged += (s, e) => Reload();
            AcceptButton = ok; CancelButton = cancel;
            Controls.AddRange(new Control[] { _search, _list, ok, newBtn, cancel });
            Reload();
            _search.Focus();
            Theme.Retrofit(this);
        }

        private void Reload()
        {
            _current = _ctx.Customers.Search(_search.Text);
            _list.Items.Clear();
            foreach (var c in _current)
                _list.Items.Add(c.Name + "  " + (c.Phone ?? "") + "   bal " + new Money(c.CurrentBalancePaise) + (c.CreditAllowed ? "  [credit]" : ""));
        }

        private void Pick()
        {
            if (_list.SelectedIndex < 0 || _current == null || _list.SelectedIndex >= _current.Count) return;
            Chosen = _current[_list.SelectedIndex];
            DialogResult = DialogResult.OK;
            Close();
        }

        private void CreateNew()
        {
            using (var f = new NewCustomerForm(_ctx))
            {
                if (f.ShowDialog(this) == DialogResult.OK && f.Created != null)
                {
                    Chosen = f.Created;
                    DialogResult = DialogResult.OK;
                    Close();
                }
            }
        }
    }

    /// <summary>
    /// Asked when removing a line that holds more than one of the same item:
    /// take one off, or drop the whole line.
    /// </summary>
    public class RemoveQuantityDialog : Form
    {
        private readonly NumericUpDown _qty;
        public int Quantity { get { return (int)_qty.Value; } }
        public bool RemoveAll { get; private set; }

        public RemoveQuantityDialog(string itemName, int onBill)
        {
            Theme.ApplyForm(this);
            Text = "Remove from bill";
            Width = 430; Height = 260;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;

            var body = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface, Padding = new Padding(Theme.Lg) };

            var what = new Label
            {
                Text = itemName,
                Font = Theme.BodyBold,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft
            };
            what.SetBounds(Theme.Lg, 12, 360, 22);

            var howMany = new Label
            {
                Text = "There are " + onBill + " on the bill. How many do you want to take off?",
                Font = Theme.Body,
                ForeColor = Theme.Muted,
                AutoSize = false,
                TextAlign = ContentAlignment.TopLeft
            };
            howMany.SetBounds(Theme.Lg, 38, 360, 36);

            _qty = new NumericUpDown
            {
                Minimum = 1,
                Maximum = onBill,
                Value = 1,
                Font = new Font(Theme.Data.FontFamily, 14f),
                TextAlign = HorizontalAlignment.Center
            };
            _qty.SetBounds(Theme.Lg, 80, 110, 34);

            var takeOff = Theme.PrimaryButton("Take these off");
            takeOff.SetBounds(Theme.Lg, 128, 170, 40);
            takeOff.Click += (s, e) => { RemoveAll = false; DialogResult = DialogResult.OK; Close(); };

            var all = Theme.SecondaryButton("Remove the whole line");
            all.SetBounds(Theme.Lg + 182, 128, 178, 40);
            all.Click += (s, e) => { RemoveAll = true; DialogResult = DialogResult.OK; Close(); };

            body.Controls.AddRange(new Control[] { what, howMany, _qty, takeOff, all });

            var footer = new Panel { Dock = DockStyle.Bottom, Height = 52, Padding = new Padding(Theme.Md, Theme.Sm, Theme.Md, Theme.Sm) };
            var cancel = Theme.SecondaryButton("Cancel");
            cancel.Width = 110; cancel.Dock = DockStyle.Right;
            cancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            footer.Controls.Add(cancel);

            Controls.Add(body);
            Controls.Add(footer);
            AcceptButton = takeOff;
            CancelButton = cancel;
            Shown += (s, e) => { _qty.Focus(); _qty.Select(0, _qty.Text.Length); };
        }
    }

    public class NewCustomerForm : Form
    {
        public new Customer Created { get; private set; }
        public NewCustomerForm(AppContext ctx)
        {
            Text = "New customer"; Width = 400; Height = 260; StartPosition = FormStartPosition.CenterParent;
            var lName = new Label { Text = "Name:", Left = 12, Top = 12, Width = 80 };
            var tName = new TextBox { Left = 100, Top = 10, Width = 260 };
            var lPhone = new Label { Text = "Phone:", Left = 12, Top = 42, Width = 80 };
            var tPhone = new TextBox { Left = 100, Top = 40, Width = 260 };
            var lAddr = new Label { Text = "Address:", Left = 12, Top = 72, Width = 80 };
            var tAddr = new TextBox { Left = 100, Top = 70, Width = 260 };
            var cCredit = new CheckBox { Text = "Credit allowed", Left = 100, Top = 100, Width = 260 };
            var lLimit = new Label { Text = "Credit limit (Rs.):", Left = 12, Top = 130, Width = 120 };
            var tLimit = new TextBox { Left = 140, Top = 128, Width = 120, Text = "0" };
            var ok = new Button { Text = "Save", Left = 200, Top = 170, Width = 80 };
            var cancel = new Button { Text = "Cancel", Left = 285, Top = 170, Width = 80, DialogResult = DialogResult.Cancel };
            ok.Click += (s, e) =>
            {
                try
                {
                    long limit = 0;
                    if (!string.IsNullOrWhiteSpace(tLimit.Text)) limit = Money.ParseRupees(tLimit.Text).Paise;
                    var cust = new Customer
                    {
                        Name = tName.Text.Trim(),
                        Phone = tPhone.Text.Trim(),
                        Address = tAddr.Text.Trim(),
                        CreditAllowed = cCredit.Checked,
                        CreditLimitPaise = limit,
                        IsActive = true
                    };
                    long id = ctx.Customers.Create(cust, ctx.CurrentUser.Id);
                    Created = ctx.Customers.FindById(id);
                    DialogResult = DialogResult.OK;
                    Close();
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
            };
            AcceptButton = ok; CancelButton = cancel;
            Controls.AddRange(new Control[] { lName, tName, lPhone, tPhone, lAddr, tAddr, cCredit, lLimit, tLimit, ok, cancel });
            Theme.Retrofit(this);
        }
    }

    public class ManualWeightDialog : Form
    {
        public int Grams;
        public ManualWeightDialog(Item it)
        {
            Text = "Manual weight — " + it.Name;
            Width = 340; Height = 200; StartPosition = FormStartPosition.CenterParent;
            var l1 = new Label { Text = "Weight in grams (min " + it.MinSaleGrams + "g, round to " + it.RoundToGrams + "g):", Left = 12, Top = 12, Width = 320 };
            var t = new TextBox { Left = 12, Top = 40, Width = 300 };
            var lErr = new Label { Left = 12, Top = 70, Width = 300, ForeColor = Color.Red };
            var ok = new Button { Text = "OK", Left = 150, Top = 100, Width = 80 };
            ok.Click += (s, e) => {
                int g;
                if (!int.TryParse(t.Text, out g)) { lErr.Text = "Enter an integer"; return; }
                int min = it.MinSaleGrams > 0 ? it.MinSaleGrams : 100;
                int step = it.RoundToGrams > 0 ? it.RoundToGrams : 5;
                if (g < min) { lErr.Text = "Below minimum " + min + "g"; return; }
                Grams = new Grams(g).RoundToStep(step).Value;
                DialogResult = DialogResult.OK;
                Close();
            };
            AcceptButton = ok;
            Controls.AddRange(new Control[] { l1, t, lErr, ok });
            t.Focus();
            Theme.Retrofit(this);
        }
    }
}
