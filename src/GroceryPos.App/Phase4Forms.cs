using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using Dapper;
using GroceryPos.Domain;

namespace GroceryPos.App
{
    // -----------------------------------------------------------------------------------
    // Purchase entry
    // -----------------------------------------------------------------------------------
    public class PurchaseEntryForm : Form
    {
        private readonly AppContext _ctx;
        private ComboBox _supplier;
        private TextBox _invoiceNo, _invoiceDate, _freight, _discount, _paymentMode, _dueDate;
        private DataGridView _grid;
        private BindingList<PurchaseLineRow> _rows = new BindingList<PurchaseLineRow>();
        private IList<Supplier> _suppliers;
        private IList<Item> _items;

        public PurchaseEntryForm(AppContext ctx)
        {
            _ctx = ctx;
            Text = "Purchase entry";
            Width = 1000; Height = 620;
            StartPosition = FormStartPosition.CenterParent;

            var top = new Panel { Dock = DockStyle.Top, Height = 100 };
            top.Controls.Add(new Label { Text = "Supplier", Left = 8, Top = 12, Width = 60 });
            _supplier = new ComboBox { Left = 70, Top = 8, Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
            top.Controls.Add(_supplier);

            top.Controls.Add(new Label { Text = "Invoice#", Left = 300, Top = 12, Width = 60 });
            _invoiceNo = new TextBox { Left = 365, Top = 8, Width = 120 };
            top.Controls.Add(_invoiceNo);

            top.Controls.Add(new Label { Text = "Date", Left = 500, Top = 12, Width = 40 });
            _invoiceDate = new TextBox { Left = 545, Top = 8, Width = 100, Text = DateTime.Today.ToString("yyyy-MM-dd") };
            top.Controls.Add(_invoiceDate);

            top.Controls.Add(new Label { Text = "Freight (Rs)", Left = 8, Top = 42, Width = 80 });
            _freight = new TextBox { Left = 90, Top = 38, Width = 80, Text = "0" };
            top.Controls.Add(_freight);
            top.Controls.Add(new Label { Text = "Discount (Rs)", Left = 180, Top = 42, Width = 90 });
            _discount = new TextBox { Left = 275, Top = 38, Width = 80, Text = "0" };
            top.Controls.Add(_discount);
            top.Controls.Add(new Label { Text = "Pay mode", Left = 365, Top = 42, Width = 70 });
            _paymentMode = new TextBox { Left = 435, Top = 38, Width = 80, Text = "credit" };
            top.Controls.Add(_paymentMode);
            top.Controls.Add(new Label { Text = "Due date", Left = 525, Top = 42, Width = 60 });
            _dueDate = new TextBox { Left = 590, Top = 38, Width = 100 };
            top.Controls.Add(_dueDate);

            var addLine = new Button { Text = "Add line", Left = 8, Top = 68, Width = 100 };
            addLine.Click += (s, e) => _rows.Add(new PurchaseLineRow());
            top.Controls.Add(addLine);
            var save = new Button { Text = "Save purchase", Left = 120, Top = 68, Width = 140 };
            save.Click += (s, e) => Save();
            top.Controls.Add(save);
            Controls.Add(top);

            _grid = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false, RowHeadersVisible = false };
            _grid.Columns.Add(NewCombo("ItemId", "Item", 240));
            _grid.Columns.Add(NewText("BatchCode", "Batch", 100));
            _grid.Columns.Add(NewText("ExpiryDate", "Expiry", 90));
            _grid.Columns.Add(NewText("QtyUnits", "Qty units", 70));
            _grid.Columns.Add(NewText("QtyGrams", "Qty grams", 80));
            _grid.Columns.Add(NewText("FreeUnits", "Free units", 70));
            _grid.Columns.Add(NewText("FreeGrams", "Free grams", 80));
            _grid.Columns.Add(NewText("CostRs", "Cost/unit (Rs)", 100));
            _grid.Columns.Add(NewText("MrpRs", "MRP (Rs)", 90));
            _grid.Columns.Add(NewText("ValueRs", "Value (Rs)", 90));
            _grid.DataSource = _rows;
            Controls.Add(_grid);

            Load += (s, e) => LoadRefs();
        }

        private DataGridViewTextBoxColumn NewText(string prop, string header, int w)
        { return new DataGridViewTextBoxColumn { DataPropertyName = prop, HeaderText = header, Width = w }; }

        private DataGridViewComboBoxColumn NewCombo(string prop, string header, int w)
        { return new DataGridViewComboBoxColumn { DataPropertyName = prop, HeaderText = header, Width = w, DisplayMember = "Display", ValueMember = "Id" }; }

        private void LoadRefs()
        {
            _suppliers = _ctx.Suppliers.All();
            _supplier.DisplayMember = "Name"; _supplier.ValueMember = "Id";
            _supplier.DataSource = _suppliers;

            _items = _ctx.Items.Search("");
            var displayItems = _items.Select(i => new { Id = i.Id, Display = i.Name + " (" + i.Sku + ")" }).ToList();
            ((DataGridViewComboBoxColumn)_grid.Columns[0]).DataSource = displayItems;
        }

        private void Save()
        {
            try
            {
                if (_supplier.SelectedItem == null) { MessageBox.Show("Pick a supplier"); return; }
                var sup = (Supplier)_supplier.SelectedItem;
                if (string.IsNullOrWhiteSpace(_invoiceNo.Text)) { MessageBox.Show("Invoice # required"); return; }
                DateTime invDate = DateTime.Parse(_invoiceDate.Text);
                long freight = Money.ParseRupees(_freight.Text).Paise;
                long discount = Money.ParseRupees(_discount.Text).Paise;
                DateTime? due = string.IsNullOrWhiteSpace(_dueDate.Text) ? (DateTime?)null : DateTime.Parse(_dueDate.Text);

                var p = new Purchase
                {
                    SupplierId = sup.Id,
                    InvoiceNo = _invoiceNo.Text.Trim(),
                    InvoiceDate = invDate,
                    FreightPaise = freight,
                    DiscountPaise = discount,
                    PaymentMode = _paymentMode.Text,
                    DueDate = due,
                };
                long goods = 0;
                foreach (var r in _rows)
                {
                    if (r.ItemId == 0) continue;
                    long cost = Money.ParseRupees(r.CostRs ?? "0").Paise;
                    long mrp = Money.ParseRupees(r.MrpRs ?? "0").Paise;
                    long val = Money.ParseRupees(r.ValueRs ?? "0").Paise;
                    goods += val;
                    p.Lines.Add(new PurchaseLine
                    {
                        ItemId = r.ItemId,
                        BatchCode = r.BatchCode,
                        ExpiryDate = string.IsNullOrWhiteSpace(r.ExpiryDate) ? (DateTime?)null : DateTime.Parse(r.ExpiryDate),
                        QtyUnits = r.QtyUnits, QtyGrams = r.QtyGrams,
                        FreeUnits = r.FreeUnits, FreeGrams = r.FreeGrams,
                        CostPaise = cost, MrpPaise = mrp, ValuePaise = val
                    });
                }
                if (p.Lines.Count == 0) { MessageBox.Show("Add at least one line"); return; }
                p.GoodsPaise = goods;
                p.TotalPaise = goods + freight - discount;
                long id = _ctx.Purchases.Save(p, _ctx.CurrentUser.Id);
                MessageBox.Show("Purchase saved (id=" + id + ", total=Rs. " + new Money(p.TotalPaise) + ")", "Saved");
                Close();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Error"); }
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
            public string CostRs { get; set; } = "0";
            public string MrpRs { get; set; } = "0";
            public string ValueRs { get; set; } = "0";
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
        private IList<dynamic> _batchRows;

        public PurchaseReturnForm(AppContext ctx)
        {
            _ctx = ctx;
            Text = "Purchase return / return to supplier";
            Width = 640; Height = 260;
            StartPosition = FormStartPosition.CenterParent;

            int y = 12;
            Controls.Add(new Label { Text = "Batch", Left = 10, Top = y + 3, Width = 60 });
            _batch = new ComboBox { Left = 80, Top = y, Width = 520, DropDownStyle = ComboBoxStyle.DropDownList };
            Controls.Add(_batch);
            y += 34;
            Controls.Add(new Label { Text = "Units", Left = 10, Top = y + 3, Width = 60 });
            _units = new TextBox { Left = 80, Top = y, Width = 80, Text = "0" }; Controls.Add(_units);
            Controls.Add(new Label { Text = "Grams", Left = 180, Top = y + 3, Width = 60 });
            _grams = new TextBox { Left = 240, Top = y, Width = 80, Text = "0" }; Controls.Add(_grams);
            y += 34;
            Controls.Add(new Label { Text = "Reason", Left = 10, Top = y + 3, Width = 60 });
            _reason = new TextBox { Left = 80, Top = y, Width = 520 }; Controls.Add(_reason);
            y += 40;
            var save = new Button { Text = "Return to supplier", Left = 80, Top = y, Width = 160 };
            save.Click += (s, e) => Save();
            Controls.Add(save);

            Load += (s, e) => LoadBatches();
        }

        private void LoadBatches()
        {
            using (var c = _ctx.Db.Open())
            {
                _batchRows = c.Query<dynamic>(@"SELECT b.id AS Id, i.name AS ItemName, b.batch_code AS BatchCode,
                    b.qty_units AS QtyUnits, b.qty_grams AS QtyGrams, b.item_id AS ItemId
                    FROM batches b JOIN items i ON i.id=b.item_id
                    WHERE b.qty_units>0 OR b.qty_grams>0 ORDER BY i.name").ToList();
            }
            foreach (var r in _batchRows)
                _batch.Items.Add((long)r.Id + " | " + (string)r.ItemName + " | " + (string)r.BatchCode + " | u=" + (long)r.QtyUnits + " g=" + (long)r.QtyGrams);
        }

        private void Save()
        {
            try
            {
                if (_batch.SelectedIndex < 0) { MessageBox.Show("Pick a batch"); return; }
                var row = _batchRows[_batch.SelectedIndex];
                if (string.IsNullOrWhiteSpace(_reason.Text)) { MessageBox.Show("Reason required"); return; }
                _ctx.StockLedger.RecordReturnToSupplier((long)row.ItemId, (long)row.Id, int.Parse(_units.Text), int.Parse(_grams.Text), _reason.Text, _ctx.CurrentUser.Id);
                MessageBox.Show("Return recorded");
                Close();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }
    }

    // -----------------------------------------------------------------------------------
    // Stock summary
    // -----------------------------------------------------------------------------------
    public class StockSummaryForm : Form
    {
        private readonly AppContext _ctx;
        private DataGridView _grid;
        private Label _cards;

        public StockSummaryForm(AppContext ctx)
        {
            _ctx = ctx;
            Text = "Stock & inventory";
            Width = 1000; Height = 620;
            StartPosition = FormStartPosition.CenterParent;

            _cards = new Label { Dock = DockStyle.Top, Height = 60, Font = new System.Drawing.Font("Segoe UI", 10F) };
            Controls.Add(_cards);
            _grid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, RowHeadersVisible = false };
            Controls.Add(_grid);
            Load += (s, e) => Reload();
        }

        private void Reload()
        {
            using (var c = _ctx.Db.Open())
            {
                long stockValue = c.ExecuteScalar<long>(@"SELECT COALESCE(SUM(cost_paise * qty_units + cost_paise * qty_grams / 1000), 0) FROM batches");
                long activeSku = c.ExecuteScalar<long>("SELECT COUNT(*) FROM items WHERE is_active=1");
                long belowReorder = c.ExecuteScalar<long>(@"
                    SELECT COUNT(*) FROM items i WHERE i.is_active=1 AND
                    (SELECT COALESCE(SUM(qty_units + qty_grams/1000),0) FROM batches b WHERE b.item_id=i.id) < i.reorder_level");
                string cutoff = DateTime.Today.AddDays(30).ToString("yyyy-MM-dd");
                long expiring = c.ExecuteScalar<long>(@"SELECT COUNT(*) FROM batches WHERE expiry_date IS NOT NULL AND expiry_date <= @d AND (qty_units>0 OR qty_grams>0)", new { d = cutoff });
                _cards.Text = "Stock value: Rs. " + new Money(stockValue) +
                    "   |   Active SKUs: " + activeSku +
                    "   |   Below reorder: " + belowReorder +
                    "   |   Expiring 30d: " + expiring;

                var rows = c.Query<dynamic>(@"SELECT b.id, i.name AS ItemName, b.batch_code AS BatchCode,
                    b.expiry_date AS ExpiryDate, b.mrp_paise AS MrpPaise, b.qty_units AS QtyUnits, b.qty_grams AS QtyGrams
                    FROM batches b JOIN items i ON i.id=b.item_id ORDER BY i.name, b.expiry_date").ToList();
                var display = rows.Select(r => new
                {
                    Id = (long)r.id,
                    Item = (string)r.ItemName,
                    Batch = (string)r.BatchCode,
                    Expiry = r.ExpiryDate == null ? "" : (string)r.ExpiryDate,
                    MRP = new Money((long)r.MrpPaise).ToString(),
                    Units = (long)r.QtyUnits,
                    Grams = (long)r.QtyGrams
                }).ToList();
                _grid.DataSource = display;
            }
        }
    }

    // -----------------------------------------------------------------------------------
    // Stock take
    // -----------------------------------------------------------------------------------
    public class StockTakeForm : Form
    {
        private readonly AppContext _ctx;
        private DataGridView _grid;
        private BindingList<StockTakeRow> _rows = new BindingList<StockTakeRow>();

        public StockTakeForm(AppContext ctx)
        {
            _ctx = ctx;
            Text = "Stock take";
            Width = 900; Height = 620;
            StartPosition = FormStartPosition.CenterParent;

            _grid = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false, RowHeadersVisible = false };
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Item", HeaderText = "Item", Width = 240, ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Batch", HeaderText = "Batch", Width = 100, ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ExpectedUnits", HeaderText = "Expected units", Width = 100, ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ExpectedGrams", HeaderText = "Expected grams", Width = 100, ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "CountedUnits", HeaderText = "Counted units", Width = 100 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "CountedGrams", HeaderText = "Counted grams", Width = 100 });
            _grid.DataSource = _rows;

            var save = new Button { Text = "Commit stock take (writes ledger)", Dock = DockStyle.Bottom, Height = 40 };
            save.Click += (s, e) => Save();
            Controls.Add(_grid);
            Controls.Add(save);
            Load += (s, e) => Reload();
        }

        private void Reload()
        {
            _rows.Clear();
            using (var c = _ctx.Db.Open())
            {
                var rows = c.Query<dynamic>(@"SELECT b.id AS Id, b.item_id AS ItemId, i.name AS ItemName,
                    b.batch_code AS BatchCode, b.qty_units AS QtyUnits, b.qty_grams AS QtyGrams
                    FROM batches b JOIN items i ON i.id=b.item_id ORDER BY i.name").ToList();
                foreach (var r in rows)
                {
                    _rows.Add(new StockTakeRow
                    {
                        BatchId = (long)r.Id, ItemId = (long)r.ItemId, Item = (string)r.ItemName,
                        Batch = (string)r.BatchCode,
                        ExpectedUnits = (int)(long)r.QtyUnits, ExpectedGrams = (int)(long)r.QtyGrams,
                        CountedUnits = (int)(long)r.QtyUnits, CountedGrams = (int)(long)r.QtyGrams
                    });
                }
            }
        }

        private void Save()
        {
            int wrote = 0;
            foreach (var r in _rows)
            {
                int du = r.CountedUnits - r.ExpectedUnits;
                int dg = r.CountedGrams - r.ExpectedGrams;
                if (du == 0 && dg == 0) continue;
                _ctx.StockLedger.RecordStockTake(r.ItemId, r.BatchId, du, dg, "stock take " + DateTime.Today.ToString("yyyy-MM-dd"), _ctx.CurrentUser.Id);
                wrote++;
            }
            MessageBox.Show("Stock take committed: " + wrote + " adjustments");
            Reload();
        }

        public class StockTakeRow
        {
            public long BatchId { get; set; }
            public long ItemId { get; set; }
            public string Item { get; set; }
            public string Batch { get; set; }
            public int ExpectedUnits { get; set; }
            public int ExpectedGrams { get; set; }
            public int CountedUnits { get; set; }
            public int CountedGrams { get; set; }
        }
    }

    // -----------------------------------------------------------------------------------
    // Wastage / damage
    // -----------------------------------------------------------------------------------
    public class WastageForm : Form
    {
        private readonly AppContext _ctx;
        private ComboBox _batch, _kind;
        private TextBox _units, _grams, _reason;
        private IList<dynamic> _batchRows;

        public WastageForm(AppContext ctx)
        {
            _ctx = ctx;
            Text = "Damage / wastage";
            Width = 640; Height = 260;
            StartPosition = FormStartPosition.CenterParent;

            int y = 12;
            Controls.Add(new Label { Text = "Type", Left = 10, Top = y + 3, Width = 60 });
            _kind = new ComboBox { Left = 80, Top = y, Width = 140, DropDownStyle = ComboBoxStyle.DropDownList };
            _kind.Items.AddRange(new object[] { "wastage", "damage" });
            _kind.SelectedIndex = 0;
            Controls.Add(_kind);
            y += 34;
            Controls.Add(new Label { Text = "Batch", Left = 10, Top = y + 3, Width = 60 });
            _batch = new ComboBox { Left = 80, Top = y, Width = 520, DropDownStyle = ComboBoxStyle.DropDownList };
            Controls.Add(_batch);
            y += 34;
            Controls.Add(new Label { Text = "Units", Left = 10, Top = y + 3, Width = 60 });
            _units = new TextBox { Left = 80, Top = y, Width = 80, Text = "0" }; Controls.Add(_units);
            Controls.Add(new Label { Text = "Grams", Left = 180, Top = y + 3, Width = 60 });
            _grams = new TextBox { Left = 240, Top = y, Width = 80, Text = "0" }; Controls.Add(_grams);
            y += 34;
            Controls.Add(new Label { Text = "Reason", Left = 10, Top = y + 3, Width = 60 });
            _reason = new TextBox { Left = 80, Top = y, Width = 520 }; Controls.Add(_reason);
            y += 40;
            var save = new Button { Text = "Record", Left = 80, Top = y, Width = 120 };
            save.Click += (s, e) => Save();
            Controls.Add(save);

            Load += (s, e) => LoadBatches();
        }

        private void LoadBatches()
        {
            using (var c = _ctx.Db.Open())
            {
                _batchRows = c.Query<dynamic>(@"SELECT b.id AS Id, i.name AS ItemName, b.batch_code AS BatchCode,
                    b.qty_units AS QtyUnits, b.qty_grams AS QtyGrams, b.item_id AS ItemId
                    FROM batches b JOIN items i ON i.id=b.item_id WHERE b.qty_units>0 OR b.qty_grams>0 ORDER BY i.name").ToList();
            }
            foreach (var r in _batchRows)
                _batch.Items.Add((long)r.Id + " | " + (string)r.ItemName + " | " + (string)r.BatchCode + " | u=" + (long)r.QtyUnits + " g=" + (long)r.QtyGrams);
        }

        private void Save()
        {
            try
            {
                if (_batch.SelectedIndex < 0) { MessageBox.Show("Pick a batch"); return; }
                var row = _batchRows[_batch.SelectedIndex];
                if (string.IsNullOrWhiteSpace(_reason.Text)) { MessageBox.Show("Reason required"); return; }
                int u = int.Parse(_units.Text); int g = int.Parse(_grams.Text);
                if (_kind.Text == "damage") _ctx.StockLedger.RecordDamage((long)row.ItemId, (long)row.Id, u, g, _reason.Text, _ctx.CurrentUser.Id);
                else _ctx.StockLedger.RecordWastage((long)row.ItemId, (long)row.Id, u, g, _reason.Text, _ctx.CurrentUser.Id);
                MessageBox.Show("Recorded"); Close();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }
    }

    // -----------------------------------------------------------------------------------
    // Unit conversion (bag to loose)
    // -----------------------------------------------------------------------------------
    public class UnitConversionForm : Form
    {
        private readonly AppContext _ctx;
        private ComboBox _sourceBatch, _targetBatch;
        private TextBox _unitsRemoved, _gramsAdded;
        private IList<dynamic> _batchRows;

        public UnitConversionForm(AppContext ctx)
        {
            _ctx = ctx;
            Text = "Unit conversion (bag to loose)";
            Width = 700; Height = 260;
            StartPosition = FormStartPosition.CenterParent;

            int y = 12;
            Controls.Add(new Label { Text = "Source", Left = 10, Top = y + 3, Width = 60 });
            _sourceBatch = new ComboBox { Left = 80, Top = y, Width = 580, DropDownStyle = ComboBoxStyle.DropDownList };
            Controls.Add(_sourceBatch);
            y += 34;
            Controls.Add(new Label { Text = "Target", Left = 10, Top = y + 3, Width = 60 });
            _targetBatch = new ComboBox { Left = 80, Top = y, Width = 580, DropDownStyle = ComboBoxStyle.DropDownList };
            Controls.Add(_targetBatch);
            y += 34;
            Controls.Add(new Label { Text = "Units out", Left = 10, Top = y + 3, Width = 80 });
            _unitsRemoved = new TextBox { Left = 90, Top = y, Width = 80, Text = "1" }; Controls.Add(_unitsRemoved);
            Controls.Add(new Label { Text = "Grams in", Left = 200, Top = y + 3, Width = 80 });
            _gramsAdded = new TextBox { Left = 280, Top = y, Width = 100, Text = "50000" }; Controls.Add(_gramsAdded);
            y += 40;
            var save = new Button { Text = "Convert", Left = 80, Top = y, Width = 120 };
            save.Click += (s, e) => Save();
            Controls.Add(save);

            Load += (s, e) => LoadBatches();
        }

        private void LoadBatches()
        {
            using (var c = _ctx.Db.Open())
            {
                _batchRows = c.Query<dynamic>(@"SELECT b.id AS Id, i.name AS ItemName, b.batch_code AS BatchCode,
                    b.qty_units AS QtyUnits, b.qty_grams AS QtyGrams, b.item_id AS ItemId
                    FROM batches b JOIN items i ON i.id=b.item_id ORDER BY i.name").ToList();
            }
            foreach (var r in _batchRows)
            {
                string s = (long)r.Id + " | " + (string)r.ItemName + " | " + (string)r.BatchCode + " | u=" + (long)r.QtyUnits + " g=" + (long)r.QtyGrams;
                _sourceBatch.Items.Add(s); _targetBatch.Items.Add(s);
            }
        }

        private void Save()
        {
            try
            {
                if (_sourceBatch.SelectedIndex < 0 || _targetBatch.SelectedIndex < 0)
                { MessageBox.Show("Pick both batches"); return; }
                var src = _batchRows[_sourceBatch.SelectedIndex];
                var tgt = _batchRows[_targetBatch.SelectedIndex];
                if ((long)src.ItemId != (long)tgt.ItemId) { MessageBox.Show("Source and target must be same item"); return; }
                _ctx.StockLedger.RecordConversion((long)src.ItemId, (long)src.Id, (long)tgt.Id,
                    int.Parse(_unitsRemoved.Text), int.Parse(_gramsAdded.Text), _ctx.CurrentUser.Id);
                MessageBox.Show("Converted"); Close();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }
    }

    // -----------------------------------------------------------------------------------
    // Near expiry / reorder reports
    // -----------------------------------------------------------------------------------
    public class NearExpiryReportForm : Form
    {
        public NearExpiryReportForm(AppContext ctx)
        {
            Text = "Near-expiry (30 days)"; Width = 900; Height = 500;
            StartPosition = FormStartPosition.CenterParent;
            var grid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, RowHeadersVisible = false };
            Controls.Add(grid);
            Load += (s, e) =>
            {
                using (var c = ctx.Db.Open())
                {
                    string cutoff = DateTime.Today.AddDays(30).ToString("yyyy-MM-dd");
                    var rows = c.Query<dynamic>(@"SELECT i.name AS Item, b.batch_code AS Batch,
                        b.expiry_date AS Expiry, b.qty_units AS Units, b.qty_grams AS Grams,
                        b.mrp_paise AS MrpPaise
                        FROM batches b JOIN items i ON i.id=b.item_id
                        WHERE b.expiry_date IS NOT NULL AND b.expiry_date <= @d
                        AND (b.qty_units>0 OR b.qty_grams>0) ORDER BY b.expiry_date", new { d = cutoff }).ToList();
                    grid.DataSource = rows.Select(r => new {
                        Item = (string)r.Item, Batch = (string)r.Batch,
                        Expiry = (string)r.Expiry, Units = (long)r.Units, Grams = (long)r.Grams,
                        MRP = new Money((long)r.MrpPaise).ToString()
                    }).ToList();
                }
            };
        }
    }

    public class ReorderReportForm : Form
    {
        public ReorderReportForm(AppContext ctx)
        {
            Text = "Reorder report"; Width = 800; Height = 500;
            StartPosition = FormStartPosition.CenterParent;
            var grid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, RowHeadersVisible = false };
            Controls.Add(grid);
            Load += (s, e) =>
            {
                using (var c = ctx.Db.Open())
                {
                    var rows = c.Query<dynamic>(@"SELECT i.name AS Item, i.sku AS Sku, i.reorder_level AS Reorder,
                        COALESCE((SELECT SUM(qty_units + qty_grams/1000) FROM batches b WHERE b.item_id=i.id),0) AS OnHand
                        FROM items i WHERE i.is_active=1 ORDER BY i.name").ToList();
                    var below = rows.Where(r => (long)r.OnHand < (long)r.Reorder).ToList();
                    grid.DataSource = below.Select(r => new {
                        Item = (string)r.Item, Sku = (string)r.Sku,
                        Reorder = (long)r.Reorder, OnHand = (long)r.OnHand
                    }).ToList();
                }
            };
        }
    }
}
