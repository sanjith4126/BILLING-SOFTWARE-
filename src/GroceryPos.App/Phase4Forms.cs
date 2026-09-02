using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Dapper;
using GroceryPos.Domain;

namespace GroceryPos.App
{
    // -----------------------------------------------------------------------------------
    // Shared helpers for the stock screens
    // -----------------------------------------------------------------------------------
    internal static class StockUi
    {
        /// <summary>
        /// A batch shown to a shop owner, not a developer. The old screens put
        /// "12 | Tomato | B-01 | u=0 g=5000" into a dropdown; this reads plainly.
        /// </summary>
        public static string DescribeBatch(string itemName, string batchCode, long units, long grams)
        {
            string qty;
            if (units > 0 && grams > 0) qty = units + " pc + " + FormatKg(grams);
            else if (grams > 0) qty = FormatKg(grams);
            else qty = units + " pc";

            string batch = string.IsNullOrWhiteSpace(batchCode) ? "" : "  [batch " + batchCode + "]";
            return itemName + batch + "  -  in stock: " + qty;
        }

        public static string FormatKg(long grams)
        {
            return (grams / 1000m).ToString("0.000", CultureInfo.InvariantCulture) + " kg";
        }
    }

    // -----------------------------------------------------------------------------------
    // Purchase entry — record a supplier invoice and take goods into stock
    // -----------------------------------------------------------------------------------
    public class PurchaseEntryForm : Form
    {
        private readonly AppContext _ctx;
        private ComboBox _supplier, _paymentMode;
        private TextBox _invoiceNo, _freight, _discount;
        private DateTimePicker _invoiceDate, _dueDate;
        private CheckBox _hasDueDate;
        private DataGridView _grid;
        private Label _lblSubtotal, _lblTotal;
        private Panel _emptyState, _banner;
        private readonly BindingList<PurchaseLineRow> _rows = new BindingList<PurchaseLineRow>();
        private IList<Item> _items;
        private List<ItemChoice> _itemChoices = new List<ItemChoice>();

        public PurchaseEntryForm(AppContext ctx)
        {
            _ctx = ctx;
            Theme.ApplyForm(this);
            Text = "Purchase entry - goods received from a supplier";
            Width = 1240; Height = 740;
            MinimumSize = new Size(1000, 600);
            StartPosition = FormStartPosition.CenterScreen;
            KeyPreview = true;

            // Docked children are laid out in reverse order of addition, so build
            // bottom-up and add in the order: Fill last. Getting this wrong is what
            // hid the grid header behind the toolbar before.
            var header = Theme.Header("Purchase entry", "Record a supplier bill. Saving adds these goods to your stock.");
            _banner = Theme.Banner();
            var invoicePanel = BuildInvoicePanel();
            var footer = BuildFooter();
            var body = BuildGrid();

            Controls.Add(body);          // Fill  — added first, sized last
            Controls.Add(footer);        // Bottom
            Controls.Add(invoicePanel);  // Top
            Controls.Add(_banner);       // Top (above the invoice fields)
            Controls.Add(header);        // Top (topmost)

            Load += (s, e) => { LoadRefs(); AddLine(); };
            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.F2) { AddLine(); e.Handled = true; }
                else if (e.KeyCode == Keys.F12) { Save(); e.Handled = true; }
                else if (e.KeyCode == Keys.Escape) { TryClose(); e.Handled = true; }
            };
        }

        // ---- Layout ---------------------------------------------------------
        private Panel BuildInvoicePanel()
        {
            var card = new Panel
            {
                Dock = DockStyle.Top,
                Height = 122,
                BackColor = Theme.Surface,
                Padding = new Padding(Theme.Md, Theme.Sm, Theme.Md, Theme.Sm)
            };

            // A flow layout keeps fields aligned no matter the DPI or font size,
            // which absolute Left/Top coordinates could never do.
            var row1 = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 54, WrapContents = false, AutoScroll = false };
            var row2 = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 54, WrapContents = false, AutoScroll = false };

            _supplier = Theme.DropDown(280);
            var addSupplier = Theme.SecondaryButton("+ New supplier");
            addSupplier.Width = 120;
            addSupplier.Click += (s, e) => NewSupplier();

            _invoiceNo = Theme.TextField(160);
            _invoiceDate = new DateTimePicker { Width = 130, Format = DateTimePickerFormat.Short, Font = Theme.Body };
            _paymentMode = Theme.DropDown(120);
            _paymentMode.Items.AddRange(new object[] { "credit", "cash", "upi", "card" });
            _paymentMode.SelectedIndex = 0;

            _freight = Theme.NumberField(100); _freight.Text = "0";
            _discount = Theme.NumberField(100); _discount.Text = "0";
            _freight.TextChanged += (s, e) => Recalculate();
            _discount.TextChanged += (s, e) => Recalculate();

            _hasDueDate = new CheckBox { Text = "Payment due on", AutoSize = true, Font = Theme.Body };
            _dueDate = new DateTimePicker { Width = 130, Format = DateTimePickerFormat.Short, Enabled = false, Font = Theme.Body };
            _hasDueDate.CheckedChanged += (s, e) => _dueDate.Enabled = _hasDueDate.Checked;

            row1.Controls.Add(Field("Supplier", _supplier));
            row1.Controls.Add(Spacer(addSupplier, 22));
            row1.Controls.Add(Field("Invoice number", _invoiceNo));
            row1.Controls.Add(Field("Invoice date", _invoiceDate));
            row1.Controls.Add(Field("Payment mode", _paymentMode));

            row2.Controls.Add(Field("Freight (Rs.)", _freight));
            row2.Controls.Add(Field("Invoice discount (Rs.)", _discount));
            row2.Controls.Add(Spacer(_hasDueDate, 24));
            row2.Controls.Add(Spacer(_dueDate, 22));

            card.Controls.Add(row2);
            card.Controls.Add(row1);
            return card;
        }

        /// <summary>Label above field, as a single flow unit.</summary>
        private static Panel Field(string caption, Control input)
        {
            var p = new Panel
            {
                Width = input.Width + Theme.Md,
                Height = 50,
                Margin = new Padding(0, 0, Theme.Sm, 0)
            };
            var l = Theme.FieldLabel(caption);
            l.SetBounds(0, 0, input.Width, 18);
            input.SetBounds(0, 20, input.Width, input.Height);
            p.Controls.Add(l);
            p.Controls.Add(input);
            return p;
        }

        /// <summary>Aligns a bare control to the baseline of the fields beside it.</summary>
        private static Panel Spacer(Control c, int topOffset)
        {
            var p = new Panel { Width = c.Width + Theme.Sm, Height = 50, Margin = new Padding(0, 0, Theme.Sm, 0) };
            c.Top = topOffset;
            c.Left = 0;
            p.Controls.Add(c);
            return p;
        }

        private Control BuildGrid()
        {
            var host = new Panel { Dock = DockStyle.Fill, Padding = new Padding(Theme.Md, Theme.Sm, Theme.Md, Theme.Sm), BackColor = Theme.Background };

            var bar = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = Theme.Background };
            bar.Controls.Add(new Label
            {
                Text = "Items received",
                Font = Theme.BodyBold,
                AutoSize = false,
                Dock = DockStyle.Left,
                Width = 200,
                TextAlign = ContentAlignment.MiddleLeft
            });
            var addLine = Theme.SecondaryButton("Add line  [F2]");
            addLine.Width = 140; addLine.Dock = DockStyle.Right;
            addLine.Click += (s, e) => AddLine();
            var delLine = Theme.SecondaryButton("Remove line");
            delLine.Width = 120; delLine.Dock = DockStyle.Right;
            delLine.Margin = new Padding(0, 0, Theme.Sm, 0);
            delLine.Click += (s, e) => RemoveLine();
            bar.Controls.Add(addLine);
            bar.Controls.Add(delLine);

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,   // rows come from the Add line button only
                AllowUserToDeleteRows = false,
                EditMode = DataGridViewEditMode.EditOnEnter
            };
            Theme.ApplyGrid(_grid);

            var itemCol = new DataGridViewComboBoxColumn
            {
                DataPropertyName = "ItemId",
                HeaderText = "Item",
                Width = 260,
                DisplayMember = "Display",
                ValueMember = "Id",
                DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton,
                FlatStyle = FlatStyle.Flat,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            _grid.Columns.Add(itemCol);
            _grid.Columns.Add(Theme.TextColumn("BatchCode", "Batch", 90));
            var expiryCol = Theme.TextColumn("ExpiryDate", "Expiry", 110);
            expiryCol.ToolTipText = "Expiry date as 2026-12-31. Leave blank if the item does not expire.";
            _grid.Columns.Add(expiryCol);
            _grid.Columns.Add(Theme.NumberColumn("QtyUnits", "Pieces", 80));
            _grid.Columns.Add(Theme.NumberColumn("QtyGrams", "Grams", 85));
            _grid.Columns.Add(Theme.NumberColumn("FreeUnits", "Free pc", 80));
            _grid.Columns.Add(Theme.NumberColumn("FreeGrams", "Free g", 80));
            _grid.Columns.Add(Theme.NumberColumn("CostRs", "Cost", 95));
            _grid.Columns.Add(Theme.NumberColumn("MrpRs", "MRP", 90));

            var valueCol = Theme.NumberColumn("ValueRs", "Line value", 110);
            valueCol.ReadOnly = true;                       // computed, never typed
            valueCol.DefaultCellStyle.BackColor = Theme.RowAlt;
            valueCol.DefaultCellStyle.Font = Theme.DataBold;
            _grid.Columns.Add(valueCol);

            _grid.DataSource = _rows;

            // The crash that produced "DataGridViewComboBoxCell value is not valid":
            // a row's ItemId of 0 matched no entry in the list. We now seed the list
            // with a real 0 = "(choose item)" row, and still swallow any stray error
            // rather than throwing a .NET dialog at the user.
            _grid.DataError += (s, e) => { e.ThrowException = false; };

            _grid.CellValueChanged += Grid_CellValueChanged;
            // Combo edits only commit on focus loss by default, which makes the
            // auto-fill feel broken. Commit as soon as the selection changes.
            _grid.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (_grid.IsCurrentCellDirty && _grid.CurrentCell is DataGridViewComboBoxCell)
                    _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };

            _emptyState = Theme.EmptyState(
                "No items on this bill yet.\r\nAdd a line for each product the supplier delivered.",
                "Add the first line  [F2]", AddLine);

            host.Controls.Add(_grid);
            host.Controls.Add(_emptyState);
            host.Controls.Add(bar);
            _emptyState.BringToFront();
            return host;
        }

        private Panel BuildFooter()
        {
            var p = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 76,
                BackColor = Theme.Surface,
                Padding = new Padding(Theme.Md, Theme.Sm, Theme.Md, Theme.Sm)
            };

            _lblSubtotal = new Label
            {
                Text = "Goods value: Rs. 0.00",
                Font = Theme.Data,
                ForeColor = Theme.Muted,
                AutoSize = false,
                Left = Theme.Md, Top = 8, Width = 420, Height = 22,
                TextAlign = ContentAlignment.MiddleLeft
            };
            _lblTotal = new Label
            {
                Text = "Invoice total: Rs. 0.00",
                Font = new Font(Theme.DataBold.FontFamily, 14f, FontStyle.Bold),
                ForeColor = Theme.Primary,
                AutoSize = false,
                Left = Theme.Md, Top = 32, Width = 420, Height = 30,
                TextAlign = ContentAlignment.MiddleLeft
            };

            var save = Theme.PrimaryButton("Save purchase  [F12]");
            save.Width = 210; save.Height = 44;
            save.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            save.Click += (s, e) => Save();

            var cancel = Theme.SecondaryButton("Cancel  [Esc]");
            cancel.Width = 130; cancel.Height = 44;
            cancel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            cancel.Click += (s, e) => TryClose();

            p.Controls.Add(_lblSubtotal);
            p.Controls.Add(_lblTotal);
            p.Controls.Add(save);
            p.Controls.Add(cancel);

            EventHandler place = (s, e) =>
            {
                save.Left = p.ClientSize.Width - save.Width - Theme.Md;
                save.Top = 14;
                cancel.Left = save.Left - cancel.Width - Theme.Sm;
                cancel.Top = 14;
            };
            p.Resize += place;
            place(null, EventArgs.Empty);
            return p;
        }

        // ---- Data -----------------------------------------------------------
        private void LoadRefs()
        {
            var suppliers = _ctx.Suppliers.All();
            _supplier.DisplayMember = "Name";
            _supplier.ValueMember = "Id";
            _supplier.DataSource = suppliers;

            _items = _ctx.Items.Search("");
            RebuildItemChoices();

            // Guidance appears as a strip on the screen rather than a popup the
            // user has to dismiss before they can even see the form.
            if (suppliers.Count == 0 && _items.Count == 0)
                Theme.ShowBanner(_banner,
                    "Before recording a purchase you need at least one supplier and one item. " +
                    "Use \"+ New supplier\" here, and add products in Item master.");
            else if (suppliers.Count == 0)
                Theme.ShowBanner(_banner,
                    "No suppliers yet. Use \"+ New supplier\" to add the shop or distributor this delivery came from.");
            else if (_items.Count == 0)
                Theme.ShowBanner(_banner,
                    "No products yet. Add them in Item master before recording a purchase.");
            else
                Theme.HideBanner(_banner);
        }

        private void RebuildItemChoices()
        {
            // Entry 0 is the placeholder every new row starts on. Without it the
            // grid throws on any row whose item has not been picked yet.
            _itemChoices = new List<ItemChoice> { new ItemChoice { Id = 0, Display = "(choose item)" } };
            foreach (var i in _items)
                _itemChoices.Add(new ItemChoice { Id = i.Id, Display = i.Name + "  (" + i.Sku + ")" });

            var col = (DataGridViewComboBoxColumn)_grid.Columns[0];
            col.DataSource = null;
            col.DataSource = _itemChoices;
            col.DisplayMember = "Display";
            col.ValueMember = "Id";
        }

        private void NewSupplier()
        {
            using (var f = new SupplierEditForm())
            {
                if (f.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    long id = _ctx.Suppliers.Create(f.Result);
                    var all = _ctx.Suppliers.All();
                    _supplier.DataSource = all;
                    var match = all.FirstOrDefault(s => s.Id == id);
                    if (match != null) _supplier.SelectedItem = match;
                }
                catch (Exception ex) { Theme.Error(ex.Message); }
            }
        }

        private void AddLine()
        {
            _rows.Add(new PurchaseLineRow());
            UpdateEmptyState();
            if (_grid.Rows.Count > 0)
            {
                var r = _grid.Rows[_grid.Rows.Count - 1];
                _grid.CurrentCell = r.Cells[0];
                _grid.Focus();
            }
        }

        private void RemoveLine()
        {
            if (_grid.CurrentRow == null) return;
            int idx = _grid.CurrentRow.Index;
            if (idx < 0 || idx >= _rows.Count) return;
            _rows.RemoveAt(idx);
            UpdateEmptyState();
            Recalculate();
        }

        private void UpdateEmptyState()
        {
            bool empty = _rows.Count == 0;
            _emptyState.Visible = empty;
            _grid.Visible = !empty;
            if (empty) _emptyState.BringToFront();
        }

        private void Grid_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _rows.Count) return;
            var row = _rows[e.RowIndex];

            // Picking an item pre-fills its usual cost and MRP so the user only
            // types what actually differs on this invoice.
            if (e.ColumnIndex == 0 && row.ItemId > 0)
            {
                var item = _items.FirstOrDefault(i => i.Id == row.ItemId);
                if (item != null)
                {
                    if (IsZero(row.CostRs) && item.DefaultCostPaise > 0)
                        row.CostRs = new Money(item.DefaultCostPaise).ToString();
                    if (IsZero(row.MrpRs) && item.DefaultMrpPaise > 0)
                        row.MrpRs = new Money(item.DefaultMrpPaise).ToString();
                }
            }

            RecalculateRow(row);
            _grid.InvalidateRow(e.RowIndex);
            Recalculate();
        }

        private static bool IsZero(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return true;
            decimal d;
            return decimal.TryParse(s.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out d) && d == 0m;
        }

        /// <summary>
        /// Line value = cost x quantity. Free goods add stock but no cost, which is
        /// why they are excluded here — it is what makes the margin come out right.
        /// </summary>
        private static void RecalculateRow(PurchaseLineRow r)
        {
            long cost = SafePaise(r.CostRs);
            decimal qty = r.QtyUnits > 0 ? r.QtyUnits : (r.QtyGrams / 1000m);
            long value = (long)Math.Round(cost * qty, MidpointRounding.AwayFromZero);
            r.ValueRs = new Money(value).ToString();
        }

        private static long SafePaise(string s)
        {
            try { return Money.ParseRupees(string.IsNullOrWhiteSpace(s) ? "0" : s).Paise; }
            catch { return 0; }
        }

        private void Recalculate()
        {
            long goods = _rows.Where(r => r.ItemId > 0).Sum(r => SafePaise(r.ValueRs));
            long freight = SafePaise(_freight.Text);
            long discount = SafePaise(_discount.Text);
            long total = goods + freight - discount;

            _lblSubtotal.Text = "Goods value: Rs. " + new Money(goods) +
                                "    + Freight: Rs. " + new Money(freight) +
                                "    - Discount: Rs. " + new Money(discount);
            _lblTotal.Text = "Invoice total: Rs. " + new Money(total);
            _lblTotal.ForeColor = total < 0 ? Theme.Danger : Theme.Primary;
        }

        // ---- Save -----------------------------------------------------------
        private void Save()
        {
            try
            {
                _grid.EndEdit();

                var sup = _supplier.SelectedItem as Supplier;
                if (sup == null)
                {
                    Theme.Warn("Choose which supplier this bill is from.");
                    _supplier.Focus();
                    return;
                }
                if (string.IsNullOrWhiteSpace(_invoiceNo.Text))
                {
                    Theme.Warn("Enter the supplier's invoice number.\r\n\r\n" +
                               "This is how the same bill is prevented from being entered twice.");
                    _invoiceNo.Focus();
                    return;
                }

                var p = new Purchase
                {
                    SupplierId = sup.Id,
                    InvoiceNo = _invoiceNo.Text.Trim(),
                    InvoiceDate = _invoiceDate.Value.Date,
                    FreightPaise = SafePaise(_freight.Text),
                    DiscountPaise = SafePaise(_discount.Text),
                    PaymentMode = _paymentMode.Text,
                    DueDate = _hasDueDate.Checked ? _dueDate.Value.Date : (DateTime?)null
                };

                long goods = 0;
                int lineNo = 0;
                foreach (var r in _rows)
                {
                    lineNo++;
                    if (r.ItemId == 0) continue;   // an untouched blank line is fine, just skip it

                    if (r.QtyUnits <= 0 && r.QtyGrams <= 0)
                    {
                        Theme.Warn("Line " + lineNo + ": enter how many pieces or grams were received.");
                        return;
                    }

                    DateTime? expiry = null;
                    if (!string.IsNullOrWhiteSpace(r.ExpiryDate))
                    {
                        DateTime parsed;
                        if (!DateTime.TryParse(r.ExpiryDate.Trim(), CultureInfo.InvariantCulture,
                                DateTimeStyles.None, out parsed))
                        {
                            Theme.Warn("Line " + lineNo + ": the expiry date \"" + r.ExpiryDate +
                                       "\" is not a valid date.\r\n\r\nUse the form 2026-12-31, or leave it blank.");
                            return;
                        }
                        expiry = parsed.Date;
                    }

                    long cost = SafePaise(r.CostRs);
                    long mrp = SafePaise(r.MrpRs);
                    if (mrp > 0 && cost > mrp)
                    {
                        if (!Theme.Confirm(
                            "Line " + lineNo + ": the cost (Rs. " + new Money(cost) +
                            ") is higher than the MRP (Rs. " + new Money(mrp) + ").\r\n\r\n" +
                            "That would mean selling at a loss. Save anyway?", "Check the price"))
                            return;
                    }

                    RecalculateRow(r);
                    long val = SafePaise(r.ValueRs);
                    goods += val;

                    p.Lines.Add(new PurchaseLine
                    {
                        ItemId = r.ItemId,
                        BatchCode = string.IsNullOrWhiteSpace(r.BatchCode) ? null : r.BatchCode.Trim(),
                        ExpiryDate = expiry,
                        QtyUnits = r.QtyUnits,
                        QtyGrams = r.QtyGrams,
                        FreeUnits = r.FreeUnits,
                        FreeGrams = r.FreeGrams,
                        CostPaise = cost,
                        MrpPaise = mrp,
                        ValuePaise = val
                    });
                }

                if (p.Lines.Count == 0)
                {
                    Theme.Warn("Add at least one item before saving this bill.");
                    return;
                }

                p.GoodsPaise = goods;
                p.TotalPaise = goods + p.FreightPaise - p.DiscountPaise;

                long id = _ctx.Purchases.Save(p, _ctx.CurrentUser.Id);
                Theme.Info(
                    "Purchase saved.\r\n\r\n" +
                    "Invoice: " + p.InvoiceNo + "\r\n" +
                    "Items: " + p.Lines.Count + "\r\n" +
                    "Total: Rs. " + new Money(p.TotalPaise) + "\r\n\r\n" +
                    "These goods are now in your stock.",
                    "Saved");
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                // The repository turns the UNIQUE(supplier_id, invoice_no) violation
                // into a "Duplicate supplier invoice" message; match either form.
                if (ex.Message.IndexOf("Duplicate supplier invoice", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    ex.Message.IndexOf("UNIQUE", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Theme.Warn("This invoice number has already been entered for this supplier.\r\n\r\n" +
                               "Check the invoice number, or look at the existing entry instead of adding it twice.");
                    return;
                }
                Theme.Error(ex.Message);
            }
        }

        private void TryClose()
        {
            bool hasWork = _rows.Any(r => r.ItemId > 0) || !string.IsNullOrWhiteSpace(_invoiceNo.Text);
            if (hasWork && !Theme.Confirm("Close without saving this purchase?", "Discard entry"))
                return;
            Close();
        }

        // ---- Row models ------------------------------------------------------
        public class ItemChoice
        {
            public long Id { get; set; }
            public string Display { get; set; }
        }

        public class PurchaseLineRow
        {
            public long ItemId { get; set; }
            public string BatchCode { get; set; }
            public string ExpiryDate { get; set; }
            public int QtyUnits { get; set; }
            public int QtyGrams { get; set; }
            public int FreeUnits { get; set; }
            public int FreeGrams { get; set; }
            public string CostRs { get; set; }
            public string MrpRs { get; set; }
            public string ValueRs { get; set; }

            public PurchaseLineRow()
            {
                CostRs = "0.00";
                MrpRs = "0.00";
                ValueRs = "0.00";
            }
        }
    }

    // -----------------------------------------------------------------------------------
    // Add a supplier — previously there was no way to create one at all
    // -----------------------------------------------------------------------------------
    public class SupplierEditForm : Form
    {
        private readonly TextBox _name, _phone, _gstin, _address, _terms;
        public Supplier Result { get; private set; }

        public SupplierEditForm()
        {
            Theme.ApplyForm(this);
            Text = "New supplier";
            Width = 460; Height = 350;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;

            var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(Theme.Lg), BackColor = Theme.Surface };
            int y = 0;
            _name = AddField(body, "Supplier name", ref y);
            _phone = AddField(body, "Phone", ref y);
            _gstin = AddField(body, "GSTIN (optional)", ref y);
            _address = AddField(body, "Address (optional)", ref y);
            _terms = AddField(body, "Credit days", ref y);
            _terms.Text = "0";

            var footer = new Panel { Dock = DockStyle.Bottom, Height = 56, Padding = new Padding(Theme.Md, Theme.Sm, Theme.Md, Theme.Sm) };
            var ok = Theme.PrimaryButton("Save supplier");
            ok.Width = 140; ok.Dock = DockStyle.Right;
            ok.Click += (s, e) => Ok();
            var cancel = Theme.SecondaryButton("Cancel");
            cancel.Width = 100; cancel.Dock = DockStyle.Right;
            cancel.Margin = new Padding(0, 0, Theme.Sm, 0);
            cancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            footer.Controls.Add(cancel);
            footer.Controls.Add(ok);

            Controls.Add(body);
            Controls.Add(footer);
            AcceptButton = ok;
            CancelButton = cancel;
        }

        private static TextBox AddField(Panel host, string caption, ref int y)
        {
            var l = Theme.FieldLabel(caption);
            l.SetBounds(Theme.Lg, y, 380, 18);
            var t = Theme.TextField(380);
            t.SetBounds(Theme.Lg, y + 19, 380, Theme.FieldHeight);
            host.Controls.Add(l);
            host.Controls.Add(t);
            y += 54;
            return t;
        }

        private void Ok()
        {
            if (string.IsNullOrWhiteSpace(_name.Text))
            {
                Theme.Warn("Enter the supplier's name.");
                _name.Focus();
                return;
            }
            int days;
            if (!int.TryParse(string.IsNullOrWhiteSpace(_terms.Text) ? "0" : _terms.Text.Trim(), out days) || days < 0)
            {
                Theme.Warn("Credit days must be a whole number, such as 0 or 30.");
                _terms.Focus();
                return;
            }
            Result = new Supplier
            {
                Name = _name.Text.Trim(),
                Phone = _phone.Text.Trim(),
                Gstin = _gstin.Text.Trim(),
                Address = _address.Text.Trim(),
                PaymentTermsDays = days
            };
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    // -----------------------------------------------------------------------------------
    // Purchase return
    // -----------------------------------------------------------------------------------
    public class PurchaseReturnForm : Form
    {
        private readonly AppContext _ctx;
        private ComboBox _batch;
        private TextBox _units, _grams, _reason;
        private Label _available;
        private List<BatchChoice> _batchRows = new List<BatchChoice>();

        public PurchaseReturnForm(AppContext ctx)
        {
            _ctx = ctx;
            Theme.ApplyForm(this);
            Text = "Return goods to supplier";
            Width = 720; Height = 380;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;

            var header = Theme.Header("Return to supplier", "Send damaged or expired goods back. Stock is reduced.");
            var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(Theme.Lg), BackColor = Theme.Surface };

            int y = Theme.Sm;
            body.Controls.Add(Cap("Which batch is going back?", Theme.Lg, y));
            _batch = Theme.DropDown(620);
            _batch.SetBounds(Theme.Lg, y + 19, 620, Theme.FieldHeight);
            _batch.SelectedIndexChanged += (s, e) => ShowAvailable();
            body.Controls.Add(_batch);
            y += 56;

            _available = new Label
            {
                Font = Theme.Data, ForeColor = Theme.Muted, AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft
            };
            _available.SetBounds(Theme.Lg, y, 620, 20);
            body.Controls.Add(_available);
            y += 28;

            body.Controls.Add(Cap("Pieces to return", Theme.Lg, y));
            _units = Theme.NumberField(120); _units.Text = "0";
            _units.SetBounds(Theme.Lg, y + 19, 120, Theme.FieldHeight);
            body.Controls.Add(_units);

            body.Controls.Add(Cap("Grams to return", Theme.Lg + 160, y));
            _grams = Theme.NumberField(120); _grams.Text = "0";
            _grams.SetBounds(Theme.Lg + 160, y + 19, 120, Theme.FieldHeight);
            body.Controls.Add(_grams);
            y += 56;

            body.Controls.Add(Cap("Reason for the return", Theme.Lg, y));
            _reason = Theme.TextField(620);
            _reason.SetBounds(Theme.Lg, y + 19, 620, Theme.FieldHeight);
            body.Controls.Add(_reason);

            var footer = new Panel { Dock = DockStyle.Bottom, Height = 60, Padding = new Padding(Theme.Md, Theme.Sm, Theme.Md, Theme.Sm) };
            var save = Theme.PrimaryButton("Record return");
            save.Width = 160; save.Dock = DockStyle.Right;
            save.Click += (s, e) => Save();
            var cancel = Theme.SecondaryButton("Cancel");
            cancel.Width = 100; cancel.Dock = DockStyle.Right;
            cancel.Click += (s, e) => Close();
            // Dock=Right stacks outward-in: add cancel first, then the primary
            // action, so "do it" ends up in the far-right corner.
            footer.Controls.Add(cancel);
            footer.Controls.Add(save);

            Controls.Add(body);
            Controls.Add(footer);
            Controls.Add(header);
            CancelButton = cancel;

            Load += (s, e) => LoadBatches();
        }

        private static Label Cap(string text, int x, int y)
        {
            var l = Theme.FieldLabel(text);
            l.SetBounds(x, y, 300, 18);
            return l;
        }

        private void LoadBatches()
        {
            _batchRows = BatchChoice.Load(_ctx, true);
            _batch.DisplayMember = "Display";
            _batch.DataSource = _batchRows;
            if (_batchRows.Count == 0)
            {
                _available.Text = "There is no stock to return. Record a purchase first.";
                _available.ForeColor = Theme.Danger;
            }
            else ShowAvailable();
        }

        private void ShowAvailable()
        {
            var b = _batch.SelectedItem as BatchChoice;
            _available.Text = b == null ? "" :
                "Available: " + b.Units + " pc, " + StockUi.FormatKg(b.Grams);
        }

        private void Save()
        {
            try
            {
                var b = _batch.SelectedItem as BatchChoice;
                if (b == null) { Theme.Warn("Choose the batch being returned."); return; }
                if (string.IsNullOrWhiteSpace(_reason.Text))
                {
                    Theme.Warn("Enter why these goods are going back.\r\n\r\n" +
                               "This is recorded so you can explain the stock change later.");
                    _reason.Focus();
                    return;
                }

                int u, g;
                if (!TryQty(_units.Text, out u)) { Theme.Warn("Pieces must be a whole number, 0 or more."); return; }
                if (!TryQty(_grams.Text, out g)) { Theme.Warn("Grams must be a whole number, 0 or more."); return; }
                if (u == 0 && g == 0) { Theme.Warn("Enter how much is being returned."); return; }
                if (u > b.Units || g > b.Grams)
                {
                    Theme.Warn("You cannot return more than you have.\r\n\r\n" +
                               "This batch holds " + b.Units + " pc and " + StockUi.FormatKg(b.Grams) + ".");
                    return;
                }

                _ctx.StockLedger.RecordReturnToSupplier(b.ItemId, b.Id, u, g, _reason.Text.Trim(), _ctx.CurrentUser.Id);
                Theme.Info("Return recorded and stock reduced.", "Done");
                Close();
            }
            catch (Exception ex) { Theme.Error(ex.Message); }
        }

        internal static bool TryQty(string s, out int v)
        {
            v = 0;
            if (string.IsNullOrWhiteSpace(s)) return true;
            return int.TryParse(s.Trim(), out v) && v >= 0;
        }
    }

    /// <summary>A batch as offered in a dropdown, described in plain words.</summary>
    internal class BatchChoice
    {
        public long Id { get; set; }
        public long ItemId { get; set; }
        public string ItemName { get; set; }
        public string BatchCode { get; set; }
        public long Units { get; set; }
        public long Grams { get; set; }
        public string Display { get; set; }

        public static List<BatchChoice> Load(AppContext ctx, bool inStockOnly)
        {
            using (var c = ctx.Db.Open())
            {
                string where = inStockOnly ? "WHERE b.qty_units>0 OR b.qty_grams>0" : "";
                var rows = c.Query<dynamic>(@"SELECT b.id AS Id, b.item_id AS ItemId, i.name AS ItemName,
                    b.batch_code AS BatchCode, b.qty_units AS QtyUnits, b.qty_grams AS QtyGrams
                    FROM batches b JOIN items i ON i.id=b.item_id " + where + " ORDER BY i.name").ToList();

                var list = new List<BatchChoice>();
                foreach (var r in rows)
                {
                    var bc = new BatchChoice
                    {
                        Id = (long)r.Id,
                        ItemId = (long)r.ItemId,
                        ItemName = (string)r.ItemName,
                        BatchCode = r.BatchCode == null ? "" : (string)r.BatchCode,
                        Units = (long)r.QtyUnits,
                        Grams = (long)r.QtyGrams
                    };
                    bc.Display = StockUi.DescribeBatch(bc.ItemName, bc.BatchCode, bc.Units, bc.Grams);
                    list.Add(bc);
                }
                return list;
            }
        }
    }

    // -----------------------------------------------------------------------------------
    // Stock summary
    // -----------------------------------------------------------------------------------
    public class StockSummaryForm : Form
    {
        private readonly AppContext _ctx;
        private DataGridView _grid;
        private Panel _cValue, _cSkus, _cReorder, _cExpiry, _emptyState;
        private TextBox _search;
        private List<StockRow> _all = new List<StockRow>();

        public StockSummaryForm(AppContext ctx)
        {
            _ctx = ctx;
            Theme.ApplyForm(this);
            Text = "Stock and inventory";
            Width = 1180; Height = 720;
            MinimumSize = new Size(900, 560);
            StartPosition = FormStartPosition.CenterScreen;

            var header = Theme.Header("Stock and inventory", "What you have on hand, batch by batch.");

            var cards = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 96,
                ColumnCount = 4,
                RowCount = 1,
                Padding = new Padding(Theme.Md, Theme.Sm, Theme.Md, Theme.Sm),
                BackColor = Theme.Background
            };
            for (int i = 0; i < 4; i++) cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            _cValue = Theme.MetricCard("Stock value", "Rs. 0.00", Theme.Primary);
            _cSkus = Theme.MetricCard("Active items", "0", Theme.Primary);
            _cReorder = Theme.MetricCard("Below reorder", "0", Theme.Warning);
            _cExpiry = Theme.MetricCard("Expiring in 30 days", "0", Theme.Danger);
            foreach (var c in new[] { _cValue, _cSkus, _cReorder, _cExpiry })
            {
                c.Dock = DockStyle.Fill;
                c.Margin = new Padding(0, 0, Theme.Sm, 0);
                cards.Controls.Add(c);
            }

            var searchBar = new Panel { Dock = DockStyle.Top, Height = 46, Padding = new Padding(Theme.Md, 0, Theme.Md, Theme.Sm), BackColor = Theme.Background };
            var lbl = new Label { Text = "Search", Font = Theme.Body, AutoSize = false, Dock = DockStyle.Left, Width = 60, TextAlign = ContentAlignment.MiddleLeft };
            _search = Theme.TextField(320);
            _search.Dock = DockStyle.Left;
            _search.TextChanged += (s, e) => ApplyFilter();
            searchBar.Controls.Add(_search);
            searchBar.Controls.Add(lbl);

            var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(Theme.Md, 0, Theme.Md, Theme.Md), BackColor = Theme.Background };
            _grid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AutoGenerateColumns = false, AllowUserToAddRows = false };
            Theme.ApplyGrid(_grid);
            _grid.Columns.Add(Theme.TextColumn("Item", "Item", 280));
            _grid.Columns.Add(Theme.TextColumn("Batch", "Batch", 110));
            _grid.Columns.Add(Theme.TextColumn("Expiry", "Expiry", 110));
            _grid.Columns.Add(Theme.NumberColumn("Mrp", "MRP", 100));
            _grid.Columns.Add(Theme.NumberColumn("Units", "Pieces", 90));
            _grid.Columns.Add(Theme.NumberColumn("Weight", "Weight", 120));
            _grid.Columns.Add(Theme.TextColumn("Status", "Status", 150));
            _grid.CellFormatting += Grid_CellFormatting;

            _emptyState = Theme.EmptyState(
                "You have no stock recorded yet.\r\nRecord a purchase to bring goods in.",
                null, null);

            body.Controls.Add(_grid);
            body.Controls.Add(_emptyState);

            Controls.Add(body);
            Controls.Add(searchBar);
            Controls.Add(cards);
            Controls.Add(header);

            Load += (s, e) => Reload();
        }

        private void Grid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _grid.Rows.Count) return;
            var row = _grid.Rows[e.RowIndex].DataBoundItem as StockRow;
            if (row == null) return;
            if (_grid.Columns[e.ColumnIndex].DataPropertyName == "Status")
            {
                e.CellStyle.ForeColor = row.Expired ? Theme.Danger
                    : row.NearExpiry ? Theme.Warning
                    : Theme.Muted;
                e.CellStyle.Font = row.Expired || row.NearExpiry ? Theme.BodyBold : Theme.Body;
            }
        }

        private void Reload()
        {
            using (var c = _ctx.Db.Open())
            {
                long stockValue = c.ExecuteScalar<long>(
                    @"SELECT COALESCE(SUM(cost_paise * qty_units + cost_paise * qty_grams / 1000), 0) FROM batches");
                long activeSku = c.ExecuteScalar<long>("SELECT COUNT(*) FROM items WHERE is_active=1");
                long belowReorder = c.ExecuteScalar<long>(@"
                    SELECT COUNT(*) FROM items i WHERE i.is_active=1 AND i.reorder_level > 0 AND
                    (SELECT COALESCE(SUM(qty_units + qty_grams/1000),0) FROM batches b WHERE b.item_id=i.id) < i.reorder_level");
                string cutoff = DateTime.Today.AddDays(30).ToString("yyyy-MM-dd");
                long expiring = c.ExecuteScalar<long>(
                    @"SELECT COUNT(*) FROM batches WHERE expiry_date IS NOT NULL AND expiry_date <= @d
                      AND (qty_units>0 OR qty_grams>0)", new { d = cutoff });

                Theme.SetMetric(_cValue, "Rs. " + new Money(stockValue));
                Theme.SetMetric(_cSkus, activeSku.ToString());
                Theme.SetMetric(_cReorder, belowReorder.ToString());
                Theme.SetMetric(_cExpiry, expiring.ToString());

                var rows = c.Query<dynamic>(@"SELECT b.id, i.name AS ItemName, b.batch_code AS BatchCode,
                    b.expiry_date AS ExpiryDate, b.mrp_paise AS MrpPaise, b.qty_units AS QtyUnits,
                    b.qty_grams AS QtyGrams
                    FROM batches b JOIN items i ON i.id=b.item_id
                    ORDER BY i.name, b.expiry_date").ToList();

                _all = new List<StockRow>();
                foreach (var r in rows)
                {
                    string exp = r.ExpiryDate == null ? "" : (string)r.ExpiryDate;
                    DateTime expDate;
                    bool hasExp = DateTime.TryParse(exp, CultureInfo.InvariantCulture, DateTimeStyles.None, out expDate);
                    long units = (long)r.QtyUnits, grams = (long)r.QtyGrams;

                    var sr = new StockRow
                    {
                        Item = (string)r.ItemName,
                        Batch = r.BatchCode == null ? "" : (string)r.BatchCode,
                        Expiry = exp,
                        Mrp = new Money((long)r.MrpPaise).ToString(),
                        Units = units == 0 ? "-" : units.ToString(),
                        Weight = grams == 0 ? "-" : StockUi.FormatKg(grams),
                        Expired = hasExp && expDate.Date < DateTime.Today,
                        NearExpiry = hasExp && expDate.Date >= DateTime.Today && expDate.Date <= DateTime.Today.AddDays(30)
                    };
                    sr.Status = units == 0 && grams == 0 ? "Out of stock"
                        : sr.Expired ? "EXPIRED"
                        : sr.NearExpiry ? "Expires in " + (expDate.Date - DateTime.Today).Days + " days"
                        : "In stock";
                    _all.Add(sr);
                }
            }
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            string q = (_search.Text ?? "").Trim();
            var view = string.IsNullOrEmpty(q)
                ? _all
                : _all.Where(r => (r.Item ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0
                               || (r.Batch ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            _grid.DataSource = new BindingList<StockRow>(view);
            bool empty = view.Count == 0;
            _emptyState.Visible = empty;
            _grid.Visible = !empty;
            if (empty) _emptyState.BringToFront();
        }

        public class StockRow
        {
            public string Item { get; set; }
            public string Batch { get; set; }
            public string Expiry { get; set; }
            public string Mrp { get; set; }
            public string Units { get; set; }
            public string Weight { get; set; }
            public string Status { get; set; }
            public bool Expired { get; set; }
            public bool NearExpiry { get; set; }
        }
    }

    // -----------------------------------------------------------------------------------
    // Stock take — count what is on the shelf against what the system expects
    // -----------------------------------------------------------------------------------
    public class StockTakeForm : Form
    {
        private readonly AppContext _ctx;
        private DataGridView _grid;
        private Panel _emptyState;
        private Label _summary;
        private readonly BindingList<StockTakeRow> _rows = new BindingList<StockTakeRow>();

        public StockTakeForm(AppContext ctx)
        {
            _ctx = ctx;
            Theme.ApplyForm(this);
            Text = "Stock take - count your shelves";
            Width = 1180; Height = 720;
            MinimumSize = new Size(900, 560);
            StartPosition = FormStartPosition.CenterScreen;

            var header = Theme.Header("Stock take",
                "Type what you actually counted. Only the lines you change are adjusted.");

            var bar = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = Theme.Background, Padding = new Padding(Theme.Md, Theme.Sm, Theme.Md, 0) };
            _summary = new Label
            {
                Dock = DockStyle.Fill,
                Font = Theme.Data,
                ForeColor = Theme.Muted,
                TextAlign = ContentAlignment.MiddleLeft
            };
            bar.Controls.Add(_summary);

            var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(Theme.Md, 0, Theme.Md, Theme.Sm), BackColor = Theme.Background };

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,      // this is the bug that showed a phantom blank row
                AllowUserToDeleteRows = false,
                EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2
            };
            Theme.ApplyGrid(_grid);

            AddReadOnly(Theme.TextColumn("Item", "Item", 260));
            AddReadOnly(Theme.TextColumn("Batch", "Batch", 100));
            AddReadOnly(Theme.TextColumn("Expiry", "Expiry", 100));
            AddReadOnly(Theme.NumberColumn("ExpectedUnits", "Was pc", 90));
            AddReadOnly(Theme.NumberColumn("ExpectedGrams", "Was g", 90));

            var cu = Theme.NumberColumn("CountedUnits", "Counted pc", 110);
            var cg = Theme.NumberColumn("CountedGrams", "Counted g", 110);
            foreach (var c in new[] { cu, cg })
            {
                Theme.MarkEditable(c);   // tints the column as "you type here"
                _grid.Columns.Add(c);
            }

            var variance = Theme.NumberColumn("Variance", "Difference", 150);
            variance.ReadOnly = true;
            _grid.Columns.Add(variance);

            _grid.DataSource = _rows;
            _grid.CellValueChanged += (s, e) =>
            {
                if (e.RowIndex >= 0) { _grid.InvalidateRow(e.RowIndex); UpdateSummary(); }
            };
            _grid.CellFormatting += Grid_CellFormatting;
            // Reject anything that is not a whole number, with a plain message.
            _grid.CellValidating += Grid_CellValidating;

            _emptyState = Theme.EmptyState(
                "There is no stock to count yet.\r\n\r\n" +
                "Stock take lists the batches you already have. Record a purchase first,\r\n" +
                "then come back here to count them.",
                null, null);

            var footer = new Panel { Dock = DockStyle.Bottom, Height = 64, BackColor = Theme.Surface, Padding = new Padding(Theme.Md, Theme.Sm, Theme.Md, Theme.Sm) };
            var save = Theme.PrimaryButton("Save the count");
            save.Width = 200; save.Height = 44; save.Dock = DockStyle.Right;
            save.Click += (s, e) => Save();
            var reset = Theme.SecondaryButton("Start again");
            reset.Width = 130; reset.Height = 44; reset.Dock = DockStyle.Right;
            reset.Click += (s, e) => { if (Theme.Confirm("Discard the counts you have typed?", "Start again")) Reload(); };
            var hint = new Label
            {
                Text = "Nothing changes until you press Save. Lines you do not touch are left alone.",
                Dock = DockStyle.Left, Width = 520, Font = Theme.Body, ForeColor = Theme.Muted,
                TextAlign = ContentAlignment.MiddleLeft
            };
            // Docked Right stacks outward-in, so add the primary action LAST
            // to keep it in the far-right corner where the eye lands.
            footer.Controls.Add(hint);
            footer.Controls.Add(reset);
            footer.Controls.Add(save);

            body.Controls.Add(_grid);
            body.Controls.Add(_emptyState);

            Controls.Add(body);
            Controls.Add(footer);
            Controls.Add(bar);
            Controls.Add(header);

            Load += (s, e) => Reload();
        }

        private void AddReadOnly(DataGridViewTextBoxColumn c)
        {
            c.ReadOnly = true;
            c.DefaultCellStyle.ForeColor = Theme.Muted;
            _grid.Columns.Add(c);
        }

        private void Grid_CellValidating(object sender, DataGridViewCellValidatingEventArgs e)
        {
            string name = _grid.Columns[e.ColumnIndex].DataPropertyName;
            if (name != "CountedUnits" && name != "CountedGrams") return;

            string s = (e.FormattedValue ?? "").ToString().Trim();
            int v;
            if (s.Length == 0 || (int.TryParse(s, out v) && v >= 0)) return;

            Theme.Warn("Enter a whole number of " + (name == "CountedUnits" ? "pieces" : "grams") +
                       ", such as 0 or 12.");
            e.Cancel = true;
        }

        private void Grid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _grid.Rows.Count) return;
            if (_grid.Columns[e.ColumnIndex].DataPropertyName != "Variance") return;
            var row = _grid.Rows[e.RowIndex].DataBoundItem as StockTakeRow;
            if (row == null) return;

            if (row.DiffUnits == 0 && row.DiffGrams == 0)
            {
                e.CellStyle.ForeColor = Theme.Muted;
                e.CellStyle.Font = Theme.Data;
            }
            else
            {
                bool shortfall = row.DiffUnits < 0 || row.DiffGrams < 0;
                e.CellStyle.ForeColor = shortfall ? Theme.Danger : Theme.Success;
                e.CellStyle.Font = Theme.DataBold;
            }
        }

        private void Reload()
        {
            _rows.Clear();
            using (var c = _ctx.Db.Open())
            {
                var rows = c.Query<dynamic>(@"SELECT b.id AS Id, b.item_id AS ItemId, i.name AS ItemName,
                    b.batch_code AS BatchCode, b.expiry_date AS ExpiryDate,
                    b.qty_units AS QtyUnits, b.qty_grams AS QtyGrams
                    FROM batches b JOIN items i ON i.id=b.item_id ORDER BY i.name").ToList();

                foreach (var r in rows)
                {
                    _rows.Add(new StockTakeRow
                    {
                        BatchId = (long)r.Id,
                        ItemId = (long)r.ItemId,
                        Item = (string)r.ItemName,
                        Batch = r.BatchCode == null ? "" : (string)r.BatchCode,
                        Expiry = r.ExpiryDate == null ? "" : (string)r.ExpiryDate,
                        ExpectedUnits = (int)(long)r.QtyUnits,
                        ExpectedGrams = (int)(long)r.QtyGrams,
                        CountedUnits = (int)(long)r.QtyUnits,
                        CountedGrams = (int)(long)r.QtyGrams
                    });
                }
            }

            bool empty = _rows.Count == 0;
            _emptyState.Visible = empty;
            _grid.Visible = !empty;
            if (empty) _emptyState.BringToFront();
            UpdateSummary();
        }

        private void UpdateSummary()
        {
            int diffs = _rows.Count(r => r.DiffUnits != 0 || r.DiffGrams != 0);
            _summary.Text = _rows.Count == 0
                ? ""
                : _rows.Count + " batches listed    |    " + diffs + " with a difference";
            _summary.ForeColor = diffs > 0 ? Theme.Danger : Theme.Muted;
        }

        private void Save()
        {
            _grid.EndEdit();
            var changed = _rows.Where(r => r.DiffUnits != 0 || r.DiffGrams != 0).ToList();
            if (changed.Count == 0)
            {
                Theme.Info("Every count matches what the system expected.\r\n\r\nNothing needed changing.",
                           "All correct");
                return;
            }

            string preview = string.Join("\r\n", changed.Take(10).Select(r =>
                "  " + r.Item + ": " + r.Variance));
            if (changed.Count > 10) preview += "\r\n  ... and " + (changed.Count - 10) + " more";

            if (!Theme.Confirm(
                changed.Count + " batch(es) do not match the counted amount:\r\n\r\n" + preview +
                "\r\n\r\nAdjust your stock to match what you counted?", "Confirm stock take"))
                return;

            int wrote = 0;
            try
            {
                foreach (var r in changed)
                {
                    _ctx.StockLedger.RecordStockTake(r.ItemId, r.BatchId, r.DiffUnits, r.DiffGrams,
                        "Stock take " + DateTime.Today.ToString("yyyy-MM-dd"), _ctx.CurrentUser.Id);
                    wrote++;
                }
            }
            catch (Exception ex)
            {
                Theme.Error("Saved " + wrote + " of " + changed.Count + " adjustments, then hit a problem:\r\n\r\n"
                            + ex.Message);
                Reload();
                return;
            }

            Theme.Info("Stock take saved. " + wrote + " batch(es) adjusted.", "Done");
            Reload();
        }

        public class StockTakeRow
        {
            public long BatchId { get; set; }
            public long ItemId { get; set; }
            public string Item { get; set; }
            public string Batch { get; set; }
            public string Expiry { get; set; }
            public int ExpectedUnits { get; set; }
            public int ExpectedGrams { get; set; }
            public int CountedUnits { get; set; }
            public int CountedGrams { get; set; }

            public int DiffUnits { get { return CountedUnits - ExpectedUnits; } }
            public int DiffGrams { get { return CountedGrams - ExpectedGrams; } }

            /// <summary>Plain-words difference, e.g. "2 pc short" or "0.150 kg extra".</summary>
            public string Variance
            {
                get
                {
                    var parts = new List<string>();
                    if (DiffUnits != 0)
                        parts.Add(Math.Abs(DiffUnits) + " pc " + (DiffUnits < 0 ? "short" : "extra"));
                    if (DiffGrams != 0)
                        parts.Add(StockUi.FormatKg(Math.Abs(DiffGrams)) + " " + (DiffGrams < 0 ? "short" : "extra"));
                    return parts.Count == 0 ? "matches" : string.Join(", ", parts);
                }
            }
        }
    }

    // -----------------------------------------------------------------------------------
    // Damage / wastage
    // -----------------------------------------------------------------------------------
    public class WastageForm : Form
    {
        private readonly AppContext _ctx;
        private ComboBox _batch, _kind;
        private TextBox _units, _grams, _reason;
        private Label _available;
        private List<BatchChoice> _batchRows = new List<BatchChoice>();

        public WastageForm(AppContext ctx)
        {
            _ctx = ctx;
            Theme.ApplyForm(this);
            Text = "Damage and wastage";
            Width = 720; Height = 420;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;

            var header = Theme.Header("Damage and wastage",
                "Write off goods that spoiled or broke. Stock is reduced and the reason is recorded.");
            var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(Theme.Lg), BackColor = Theme.Surface };

            int y = Theme.Sm;
            body.Controls.Add(Cap("What happened?", Theme.Lg, y));
            _kind = Theme.DropDown(200);
            _kind.Items.AddRange(new object[] { "Spoiled / wasted", "Damaged / broken" });
            _kind.SelectedIndex = 0;
            _kind.SetBounds(Theme.Lg, y + 19, 200, Theme.FieldHeight);
            body.Controls.Add(_kind);
            y += 56;

            body.Controls.Add(Cap("Which batch?", Theme.Lg, y));
            _batch = Theme.DropDown(620);
            _batch.SetBounds(Theme.Lg, y + 19, 620, Theme.FieldHeight);
            _batch.SelectedIndexChanged += (s, e) => ShowAvailable();
            body.Controls.Add(_batch);
            y += 56;

            _available = new Label { Font = Theme.Data, ForeColor = Theme.Muted, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft };
            _available.SetBounds(Theme.Lg, y, 620, 20);
            body.Controls.Add(_available);
            y += 28;

            body.Controls.Add(Cap("Pieces lost", Theme.Lg, y));
            _units = Theme.NumberField(120); _units.Text = "0";
            _units.SetBounds(Theme.Lg, y + 19, 120, Theme.FieldHeight);
            body.Controls.Add(_units);

            body.Controls.Add(Cap("Grams lost", Theme.Lg + 160, y));
            _grams = Theme.NumberField(120); _grams.Text = "0";
            _grams.SetBounds(Theme.Lg + 160, y + 19, 120, Theme.FieldHeight);
            body.Controls.Add(_grams);
            y += 56;

            body.Controls.Add(Cap("Reason", Theme.Lg, y));
            _reason = Theme.TextField(620);
            _reason.SetBounds(Theme.Lg, y + 19, 620, Theme.FieldHeight);
            body.Controls.Add(_reason);

            var footer = new Panel { Dock = DockStyle.Bottom, Height = 60, Padding = new Padding(Theme.Md, Theme.Sm, Theme.Md, Theme.Sm) };
            var save = Theme.PrimaryButton("Record the loss");
            save.Width = 170; save.Dock = DockStyle.Right;
            save.Click += (s, e) => Save();
            var cancel = Theme.SecondaryButton("Cancel");
            cancel.Width = 100; cancel.Dock = DockStyle.Right;
            cancel.Click += (s, e) => Close();
            // Dock=Right stacks outward-in: add cancel first, then the primary
            // action, so "do it" ends up in the far-right corner.
            footer.Controls.Add(cancel);
            footer.Controls.Add(save);

            Controls.Add(body);
            Controls.Add(footer);
            Controls.Add(header);
            CancelButton = cancel;

            Load += (s, e) => LoadBatches();
        }

        private static Label Cap(string text, int x, int y)
        {
            var l = Theme.FieldLabel(text);
            l.SetBounds(x, y, 300, 18);
            return l;
        }

        private void LoadBatches()
        {
            _batchRows = BatchChoice.Load(_ctx, true);
            _batch.DisplayMember = "Display";
            _batch.DataSource = _batchRows;
            if (_batchRows.Count == 0)
            {
                _available.Text = "There is no stock to write off. Record a purchase first.";
                _available.ForeColor = Theme.Danger;
            }
            else ShowAvailable();
        }

        private void ShowAvailable()
        {
            var b = _batch.SelectedItem as BatchChoice;
            _available.Text = b == null ? "" : "Available: " + b.Units + " pc, " + StockUi.FormatKg(b.Grams);
        }

        private void Save()
        {
            try
            {
                var b = _batch.SelectedItem as BatchChoice;
                if (b == null) { Theme.Warn("Choose the batch."); return; }
                if (string.IsNullOrWhiteSpace(_reason.Text))
                {
                    Theme.Warn("Enter what happened.\r\n\r\nThis is recorded so the stock change can be explained later.");
                    _reason.Focus();
                    return;
                }

                int u, g;
                if (!PurchaseReturnForm.TryQty(_units.Text, out u)) { Theme.Warn("Pieces must be a whole number, 0 or more."); return; }
                if (!PurchaseReturnForm.TryQty(_grams.Text, out g)) { Theme.Warn("Grams must be a whole number, 0 or more."); return; }
                if (u == 0 && g == 0) { Theme.Warn("Enter how much was lost."); return; }
                if (u > b.Units || g > b.Grams)
                {
                    Theme.Warn("You cannot write off more than you have.\r\n\r\n" +
                               "This batch holds " + b.Units + " pc and " + StockUi.FormatKg(b.Grams) + ".");
                    return;
                }

                bool damage = _kind.SelectedIndex == 1;
                if (!Theme.Confirm(
                    "Write off " + (u > 0 ? u + " pc " : "") + (g > 0 ? StockUi.FormatKg(g) : "") +
                    " of " + b.ItemName + "?\r\n\r\nThis cannot be undone, only corrected with another entry.",
                    "Confirm write-off"))
                    return;

                if (damage) _ctx.StockLedger.RecordDamage(b.ItemId, b.Id, u, g, _reason.Text.Trim(), _ctx.CurrentUser.Id);
                else _ctx.StockLedger.RecordWastage(b.ItemId, b.Id, u, g, _reason.Text.Trim(), _ctx.CurrentUser.Id);

                Theme.Info("Recorded and stock reduced.", "Done");
                Close();
            }
            catch (Exception ex) { Theme.Error(ex.Message); }
        }
    }

    // -----------------------------------------------------------------------------------
    // Unit conversion — a sack becomes loose stock
    // -----------------------------------------------------------------------------------
    public class UnitConversionForm : Form
    {
        private readonly AppContext _ctx;
        private ComboBox _sourceBatch, _targetBatch;
        private TextBox _unitsRemoved, _gramsAdded;
        private Label _available;
        private List<BatchChoice> _batchRows = new List<BatchChoice>();

        public UnitConversionForm(AppContext ctx)
        {
            _ctx = ctx;
            Theme.ApplyForm(this);
            Text = "Open a sack into loose stock";
            Width = 760; Height = 420;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;

            var header = Theme.Header("Open a sack into loose stock",
                "Example: one 50 kg rice bag becomes 50,000 g of loose rice.");
            var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(Theme.Lg), BackColor = Theme.Surface };

            int y = Theme.Sm;
            body.Controls.Add(Cap("Take from (the sealed bag)", Theme.Lg, y));
            _sourceBatch = Theme.DropDown(660);
            _sourceBatch.SetBounds(Theme.Lg, y + 19, 660, Theme.FieldHeight);
            _sourceBatch.SelectedIndexChanged += (s, e) => ShowAvailable();
            body.Controls.Add(_sourceBatch);
            y += 56;

            _available = new Label { Font = Theme.Data, ForeColor = Theme.Muted, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft };
            _available.SetBounds(Theme.Lg, y, 660, 20);
            body.Controls.Add(_available);
            y += 28;

            body.Controls.Add(Cap("Add to (the loose batch of the same item)", Theme.Lg, y));
            _targetBatch = Theme.DropDown(660);
            _targetBatch.SetBounds(Theme.Lg, y + 19, 660, Theme.FieldHeight);
            body.Controls.Add(_targetBatch);
            y += 56;

            body.Controls.Add(Cap("Bags opened", Theme.Lg, y));
            _unitsRemoved = Theme.NumberField(120); _unitsRemoved.Text = "1";
            _unitsRemoved.SetBounds(Theme.Lg, y + 19, 120, Theme.FieldHeight);
            body.Controls.Add(_unitsRemoved);

            body.Controls.Add(Cap("Grams gained", Theme.Lg + 170, y));
            _gramsAdded = Theme.NumberField(140); _gramsAdded.Text = "50000";
            _gramsAdded.SetBounds(Theme.Lg + 170, y + 19, 140, Theme.FieldHeight);
            body.Controls.Add(_gramsAdded);

            var footer = new Panel { Dock = DockStyle.Bottom, Height = 60, Padding = new Padding(Theme.Md, Theme.Sm, Theme.Md, Theme.Sm) };
            var save = Theme.PrimaryButton("Convert");
            save.Width = 140; save.Dock = DockStyle.Right;
            save.Click += (s, e) => Save();
            var cancel = Theme.SecondaryButton("Cancel");
            cancel.Width = 100; cancel.Dock = DockStyle.Right;
            cancel.Click += (s, e) => Close();
            // Dock=Right stacks outward-in: add cancel first, then the primary
            // action, so "do it" ends up in the far-right corner.
            footer.Controls.Add(cancel);
            footer.Controls.Add(save);

            Controls.Add(body);
            Controls.Add(footer);
            Controls.Add(header);
            CancelButton = cancel;

            Load += (s, e) => LoadBatches();
        }

        private static Label Cap(string text, int x, int y)
        {
            var l = Theme.FieldLabel(text);
            l.SetBounds(x, y, 340, 18);
            return l;
        }

        private void LoadBatches()
        {
            _batchRows = BatchChoice.Load(_ctx, false);
            _sourceBatch.DisplayMember = "Display";
            _targetBatch.DisplayMember = "Display";
            // Separate list instances: one shared BindingList would move both
            // selections together.
            _sourceBatch.DataSource = new List<BatchChoice>(_batchRows);
            _targetBatch.DataSource = new List<BatchChoice>(_batchRows);
            if (_batchRows.Count == 0)
            {
                _available.Text = "There are no batches yet. Record a purchase first.";
                _available.ForeColor = Theme.Danger;
            }
            else ShowAvailable();
        }

        private void ShowAvailable()
        {
            var b = _sourceBatch.SelectedItem as BatchChoice;
            _available.Text = b == null ? "" : "Available in that batch: " + b.Units + " pc, " + StockUi.FormatKg(b.Grams);
        }

        private void Save()
        {
            try
            {
                var src = _sourceBatch.SelectedItem as BatchChoice;
                var tgt = _targetBatch.SelectedItem as BatchChoice;
                if (src == null || tgt == null) { Theme.Warn("Choose both the bag and the loose batch."); return; }
                if (src.Id == tgt.Id) { Theme.Warn("The two batches must be different."); return; }
                if (src.ItemId != tgt.ItemId)
                {
                    Theme.Warn("Both batches must be the same product.\r\n\r\n" +
                               "You chose \"" + src.ItemName + "\" and \"" + tgt.ItemName + "\".");
                    return;
                }

                int u, g;
                if (!PurchaseReturnForm.TryQty(_unitsRemoved.Text, out u) || u <= 0) { Theme.Warn("Bags opened must be 1 or more."); return; }
                if (!PurchaseReturnForm.TryQty(_gramsAdded.Text, out g) || g <= 0) { Theme.Warn("Grams gained must be more than 0."); return; }
                if (u > src.Units)
                {
                    Theme.Warn("That batch only has " + src.Units + " bag(s).");
                    return;
                }

                _ctx.StockLedger.RecordConversion(src.ItemId, src.Id, tgt.Id, u, g, _ctx.CurrentUser.Id);
                Theme.Info(u + " bag(s) opened into " + StockUi.FormatKg(g) + " of loose stock.", "Converted");
                Close();
            }
            catch (Exception ex) { Theme.Error(ex.Message); }
        }
    }

    // -----------------------------------------------------------------------------------
    // Near expiry / reorder reports
    // -----------------------------------------------------------------------------------
    public class NearExpiryReportForm : Form
    {
        public NearExpiryReportForm(AppContext ctx)
        {
            Theme.ApplyForm(this);
            Text = "Goods expiring soon";
            Width = 1000; Height = 600;
            MinimumSize = new Size(760, 460);
            StartPosition = FormStartPosition.CenterScreen;

            var header = Theme.Header("Expiring within 30 days", "Sell these first, or return them to the supplier.");
            var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(Theme.Md), BackColor = Theme.Background };

            var grid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AutoGenerateColumns = false, AllowUserToAddRows = false };
            Theme.ApplyGrid(grid);
            grid.Columns.Add(Theme.TextColumn("Item", "Item", 280));
            grid.Columns.Add(Theme.TextColumn("Batch", "Batch", 110));
            grid.Columns.Add(Theme.TextColumn("Expiry", "Expiry", 110));
            grid.Columns.Add(Theme.TextColumn("DaysLeft", "Days left", 100));
            grid.Columns.Add(Theme.NumberColumn("Units", "Pieces", 90));
            grid.Columns.Add(Theme.NumberColumn("Weight", "Weight", 120));
            grid.Columns.Add(Theme.NumberColumn("Mrp", "MRP", 100));

            var empty = Theme.EmptyState("Nothing is expiring in the next 30 days.", null, null);
            body.Controls.Add(grid);
            body.Controls.Add(empty);

            Controls.Add(body);
            Controls.Add(header);

            Load += (s, e) =>
            {
                var list = new List<ExpiryRow>();
                using (var c = ctx.Db.Open())
                {
                    string cutoff = DateTime.Today.AddDays(30).ToString("yyyy-MM-dd");
                    var rows = c.Query<dynamic>(@"SELECT i.name AS Item, b.batch_code AS Batch,
                        b.expiry_date AS Expiry, b.qty_units AS Units, b.qty_grams AS Grams,
                        b.mrp_paise AS MrpPaise
                        FROM batches b JOIN items i ON i.id=b.item_id
                        WHERE b.expiry_date IS NOT NULL AND b.expiry_date <= @d
                        AND (b.qty_units>0 OR b.qty_grams>0) ORDER BY b.expiry_date", new { d = cutoff }).ToList();

                    foreach (var r in rows)
                    {
                        string exp = (string)r.Expiry;
                        DateTime d;
                        int days = DateTime.TryParse(exp, CultureInfo.InvariantCulture, DateTimeStyles.None, out d)
                            ? (d.Date - DateTime.Today).Days : 0;
                        long units = (long)r.Units, grams = (long)r.Grams;
                        list.Add(new ExpiryRow
                        {
                            Item = (string)r.Item,
                            Batch = r.Batch == null ? "" : (string)r.Batch,
                            Expiry = exp,
                            DaysLeft = days < 0 ? "EXPIRED" : days.ToString(),
                            Expired = days < 0,
                            Units = units == 0 ? "-" : units.ToString(),
                            Weight = grams == 0 ? "-" : StockUi.FormatKg(grams),
                            Mrp = new Money((long)r.MrpPaise).ToString()
                        });
                    }
                }
                grid.DataSource = new BindingList<ExpiryRow>(list);
                empty.Visible = list.Count == 0;
                grid.Visible = list.Count > 0;
                if (list.Count == 0) empty.BringToFront();
            };

            grid.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.RowIndex >= grid.Rows.Count) return;
                var row = grid.Rows[e.RowIndex].DataBoundItem as ExpiryRow;
                if (row != null && row.Expired)
                {
                    e.CellStyle.ForeColor = Theme.Danger;
                    e.CellStyle.Font = Theme.BodyBold;
                }
            };
        }

        private class ExpiryRow
        {
            public string Item { get; set; }
            public string Batch { get; set; }
            public string Expiry { get; set; }
            public string DaysLeft { get; set; }
            public string Units { get; set; }
            public string Weight { get; set; }
            public string Mrp { get; set; }
            public bool Expired { get; set; }
        }
    }

    public class ReorderReportForm : Form
    {
        public ReorderReportForm(AppContext ctx)
        {
            Theme.ApplyForm(this);
            Text = "Items to reorder";
            Width = 900; Height = 600;
            MinimumSize = new Size(700, 440);
            StartPosition = FormStartPosition.CenterScreen;

            var header = Theme.Header("Items to reorder", "Stock has fallen below the level you set for these items.");
            var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(Theme.Md), BackColor = Theme.Background };

            var grid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AutoGenerateColumns = false, AllowUserToAddRows = false };
            Theme.ApplyGrid(grid);
            grid.Columns.Add(Theme.TextColumn("Item", "Item", 320));
            grid.Columns.Add(Theme.TextColumn("Sku", "Code", 140));
            grid.Columns.Add(Theme.NumberColumn("OnHand", "On hand", 110));
            grid.Columns.Add(Theme.NumberColumn("Reorder", "Reorder at", 110));
            grid.Columns.Add(Theme.NumberColumn("Shortfall", "Short by", 110));

            var empty = Theme.EmptyState(
                "Nothing needs reordering right now.\r\n\r\n" +
                "Tip: set a reorder level on an item in Item master to see it here.", null, null);
            body.Controls.Add(grid);
            body.Controls.Add(empty);

            Controls.Add(body);
            Controls.Add(header);

            Load += (s, e) =>
            {
                var list = new List<ReorderRow>();
                using (var c = ctx.Db.Open())
                {
                    var rows = c.Query<dynamic>(@"SELECT i.name AS Item, i.sku AS Sku, i.reorder_level AS Reorder,
                        COALESCE((SELECT SUM(qty_units + qty_grams/1000) FROM batches b WHERE b.item_id=i.id),0) AS OnHand
                        FROM items i WHERE i.is_active=1 AND i.reorder_level > 0 ORDER BY i.name").ToList();

                    foreach (var r in rows)
                    {
                        long onHand = (long)r.OnHand, reorder = (long)r.Reorder;
                        if (onHand >= reorder) continue;
                        list.Add(new ReorderRow
                        {
                            Item = (string)r.Item,
                            Sku = (string)r.Sku,
                            OnHand = onHand,
                            Reorder = reorder,
                            Shortfall = reorder - onHand
                        });
                    }
                }
                grid.DataSource = new BindingList<ReorderRow>(list);
                empty.Visible = list.Count == 0;
                grid.Visible = list.Count > 0;
                if (list.Count == 0) empty.BringToFront();
            };
        }

        private class ReorderRow
        {
            public string Item { get; set; }
            public string Sku { get; set; }
            public long OnHand { get; set; }
            public long Reorder { get; set; }
            public long Shortfall { get; set; }
        }
    }
}
