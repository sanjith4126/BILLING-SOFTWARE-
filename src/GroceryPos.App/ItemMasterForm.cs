using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.IO;
using System.Windows.Forms;
using GroceryPos.Domain;

namespace GroceryPos.App
{
    public class ItemMasterForm : Form
    {
        private readonly AppContext _ctx;
        private DataGridView _grid;
        private TextBox _search;

        private Panel _emptyState;

        public ItemMasterForm(AppContext ctx)
        {
            _ctx = ctx;
            Theme.ApplyForm(this);
            Text = "Item master - your product list";
            Width = 1100; Height = 680;
            MinimumSize = new System.Drawing.Size(860, 520);
            StartPosition = FormStartPosition.CenterScreen;

            var header = Theme.Header("Item master", "Every product you sell, with its price and tax.");

            var top = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = Theme.Surface,
                Padding = new Padding(Theme.Md, Theme.Sm, Theme.Md, Theme.Sm)
            };
            var lbl = Theme.FieldLabel("Search by name or code");
            lbl.SetBounds(Theme.Md, 2, 240, 16);
            _search = Theme.TextField(300);
            _search.SetBounds(Theme.Md, 20, 300, Theme.FieldHeight);
            _search.TextChanged += (s, e) => Reload();

            var newBtn = Theme.PrimaryButton("Add a new item");
            newBtn.SetBounds(340, 20, 150, Theme.ButtonHeight);
            newBtn.Click += (s, e) => EditItem(new Item
            {
                SoldBy = SoldBy.Piece, Unit = "pc", IsActive = true,
                RoundToGrams = 5, MinSaleGrams = 100, AllowDiscount = true
            });
            var editBtn = Theme.SecondaryButton("Edit selected");
            editBtn.SetBounds(500, 20, 130, Theme.ButtonHeight);
            editBtn.Click += (s, e) => EditSelected();
            var importBtn = Theme.SecondaryButton("Import from CSV");
            importBtn.SetBounds(640, 20, 150, Theme.ButtonHeight);
            importBtn.Click += (s, e) => ImportCsv();

            top.Controls.AddRange(new Control[] { lbl, _search, newBtn, editBtn, importBtn });

            var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(Theme.Md), BackColor = Theme.Background };
            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AutoGenerateColumns = false
            };
            Theme.ApplyGrid(_grid);
            // The internal id and basis-point tax rate mean nothing to a shopkeeper,
            // so show the code, the name, and prices in rupees instead.
            _grid.Columns.Add(Theme.TextColumn("Sku", "Code", 120));
            var nameCol = Theme.TextColumn("Name", "Item name", 300);
            nameCol.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            nameCol.MinimumWidth = 200;
            _grid.Columns.Add(nameCol);
            _grid.Columns.Add(Theme.TextColumn("SoldByText", "Sold by", 90));
            _grid.Columns.Add(Theme.NumberColumn("SellingText", "Selling", 100));
            _grid.Columns.Add(Theme.NumberColumn("MrpText", "MRP", 100));
            _grid.Columns.Add(Theme.NumberColumn("TaxText", "GST", 70));
            _grid.Columns.Add(Theme.TextColumn("HsnCode", "HSN", 90));
            _grid.Columns.Add(Theme.TextColumn("StatusText", "Status", 80));
            _grid.CellDoubleClick += (s, e) => EditSelected();

            _emptyState = Theme.EmptyState(
                "No items match that search.\r\n\r\nClear the search box, or add a new item.",
                "Add a new item",
                () => EditItem(new Item
                {
                    SoldBy = SoldBy.Piece, Unit = "pc", IsActive = true,
                    RoundToGrams = 5, MinSaleGrams = 100, AllowDiscount = true
                }));

            body.Controls.Add(_grid);
            body.Controls.Add(_emptyState);

            Controls.Add(body);
            Controls.Add(top);
            Controls.Add(header);
            Reload();
        }

        private void Reload()
        {
            var items = _ctx.Items.Search(_search.Text);
            var view = items.Select(i => new ItemRow
            {
                Id = i.Id,
                Sku = i.Sku,
                Name = i.Name,
                SoldByText = i.SoldBy == SoldBy.Weight ? "Weight"
                           : i.SoldBy == SoldBy.Volume ? "Volume" : "Piece",
                SellingText = new Money(i.DefaultSellingPaise).ToString(),
                MrpText = new Money(i.DefaultMrpPaise).ToString(),
                TaxText = (i.TaxRateBp / 100m).ToString("0.##") + "%",
                HsnCode = i.HsnCode,
                StatusText = i.IsActive ? "Active" : "Inactive"
            }).ToList();

            _grid.DataSource = new BindingList<ItemRow>(view);
            bool empty = view.Count == 0;
            _emptyState.Visible = empty;
            _grid.Visible = !empty;
            if (empty) _emptyState.BringToFront();
        }

        /// <summary>Display shape for the list; the real Item is loaded on edit.</summary>
        private class ItemRow
        {
            public long Id { get; set; }
            public string Sku { get; set; }
            public string Name { get; set; }
            public string SoldByText { get; set; }
            public string SellingText { get; set; }
            public string MrpText { get; set; }
            public string TaxText { get; set; }
            public string HsnCode { get; set; }
            public string StatusText { get; set; }
        }

        private void EditSelected()
        {
            if (_grid.CurrentRow == null)
            {
                Theme.Warn("Select an item in the list first.");
                return;
            }
            var row = _grid.CurrentRow.DataBoundItem as ItemRow;
            if (row == null) return;
            var full = _ctx.Items.FindById(row.Id);
            if (full == null) { Theme.Warn("That item could no longer be found."); Reload(); return; }
            EditItem(full);
        }

        private void EditItem(Item it)
        {
            using (var f = new ItemEditForm(_ctx, it))
            {
                if (f.ShowDialog() == DialogResult.OK) Reload();
            }
        }

        private void ImportCsv()
        {
            using (var d = new OpenFileDialog { Filter = "CSV|*.csv" })
            {
                if (d.ShowDialog() != DialogResult.OK) return;
                var report = CsvImporter.Import(d.FileName, _ctx);
                MessageBox.Show(report, "Import Report");
                Reload();
            }
        }
    }

    public class ItemEditForm : Form
    {
        private readonly AppContext _ctx;
        private readonly Item _it;
        private TextBox _sku, _name, _printName, _unit, _hsn, _tax, _round, _minSale, _tare;
        private TextBox _cost, _selling, _mrp;
        private Label _marginLabel;
        private ComboBox _soldBy;
        private CheckBox _weighAtCounter, _allowDiscount, _isActive;
        private TextBox _barcodes;

        public ItemEditForm(AppContext ctx, Item it)
        {
            _ctx = ctx; _it = it;
            Text = it.Id == 0 ? "New item" : "Edit item";
            Width = 520; Height = 680;
            StartPosition = FormStartPosition.CenterParent;

            int y = 12;
            _sku = AddField("SKU", it.Sku, ref y);
            _name = AddField("Name", it.Name, ref y);
            _printName = AddField("Print name", it.PrintName, ref y);

            var l = new Label { Text = "Sold by", Left = 12, Top = y + 4, Width = 100 };
            _soldBy = new ComboBox { Left = 120, Top = y, Width = 360, DropDownStyle = ComboBoxStyle.DropDownList };
            _soldBy.Items.AddRange(new object[] { "Piece", "Weight", "Volume" });
            _soldBy.SelectedItem = it.SoldBy.ToString();
            Controls.Add(l); Controls.Add(_soldBy); y += 30;

            _unit = AddField("Unit", it.Unit ?? "pc", ref y);
            _hsn = AddField("HSN", it.HsnCode, ref y);
            _tax = AddField("Tax rate (bp, e.g. 1800=18%)", it.TaxRateBp.ToString(), ref y);

            // Prices — all in rupees on screen, converted to paise on save.
            var priceHeader = new Label { Text = "Prices (Rs.)", Left = 12, Top = y, Width = 200,
                Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold) };
            Controls.Add(priceHeader); y += 22;

            Controls.Add(new Label { Text = "Cost", Left = 12, Top = y + 4, Width = 60 });
            _cost = new TextBox { Left = 80, Top = y, Width = 100, Text = PaiseToRupeeString(it.DefaultCostPaise) };
            Controls.Add(new Label { Text = "Selling", Left = 200, Top = y + 4, Width = 60 });
            _selling = new TextBox { Left = 265, Top = y, Width = 100, Text = PaiseToRupeeString(it.DefaultSellingPaise) };
            Controls.Add(new Label { Text = "MRP", Left = 380, Top = y + 4, Width = 40 });
            _mrp = new TextBox { Left = 420, Top = y, Width = 80, Text = PaiseToRupeeString(it.DefaultMrpPaise) };
            Controls.Add(_cost); Controls.Add(_selling); Controls.Add(_mrp);
            y += 26;

            _marginLabel = new Label { Left = 80, Top = y, Width = 400,
                ForeColor = System.Drawing.Color.DarkGreen, Text = "" };
            Controls.Add(_marginLabel);
            _cost.TextChanged += (s, e) => UpdateMargin();
            _selling.TextChanged += (s, e) => UpdateMargin();
            _mrp.TextChanged += (s, e) => UpdateMargin();
            UpdateMargin();
            y += 26;

            _tare = AddField("Tare grams", it.TareGrams.ToString(), ref y);
            _round = AddField("Round to grams", it.RoundToGrams.ToString(), ref y);
            _minSale = AddField("Min sale grams", it.MinSaleGrams.ToString(), ref y);

            _weighAtCounter = new CheckBox { Text = "Weigh at counter", Left = 120, Top = y, Width = 200, Checked = it.WeighAtCounter };
            Controls.Add(_weighAtCounter); y += 24;
            _allowDiscount = new CheckBox { Text = "Allow discount", Left = 120, Top = y, Width = 200, Checked = it.AllowDiscount };
            Controls.Add(_allowDiscount); y += 24;
            _isActive = new CheckBox { Text = "Active", Left = 120, Top = y, Width = 200, Checked = it.IsActive };
            Controls.Add(_isActive); y += 30;

            var bcLbl = new Label { Text = "Barcodes (one per line)", Left = 12, Top = y, Width = 200 };
            Controls.Add(bcLbl); y += 18;
            _barcodes = new TextBox { Left = 12, Top = y, Width = 440, Height = 60, Multiline = true };
            if (it.Id != 0) _barcodes.Text = string.Join("\r\n", _ctx.Items.BarcodesFor(it.Id));
            Controls.Add(_barcodes); y += 70;

            var save = new Button { Text = "Save", Left = 260, Top = y, Width = 90 };
            save.Click += (s, e) => Save();
            var cancel = new Button { Text = "Cancel", Left = 360, Top = y, Width = 90, DialogResult = DialogResult.Cancel };
            Controls.Add(save); Controls.Add(cancel);
            Theme.Retrofit(this);
        }

        private TextBox AddField(string label, string val, ref int y)
        {
            var l = new Label { Text = label, Left = 12, Top = y + 4, Width = 100 };
            var t = new TextBox { Left = 120, Top = y, Width = 360, Text = val ?? "" };
            Controls.Add(l); Controls.Add(t);
            y += 30;
            return t;
        }

        private static string PaiseToRupeeString(long paise)
        {
            if (paise == 0) return "";
            long rupees = paise / 100;
            long pp = paise % 100;
            return rupees + "." + pp.ToString("00");
        }

        private static long RupeeStringToPaise(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return 0;
            decimal d;
            if (!decimal.TryParse(s.Trim(), System.Globalization.NumberStyles.Any,
                                  System.Globalization.CultureInfo.InvariantCulture, out d))
                throw new Exception("Not a valid amount: " + s);
            return (long)System.Math.Round(d * 100m);
        }

        private void UpdateMargin()
        {
            try
            {
                long cost = RupeeStringToPaise(_cost.Text);
                long selling = RupeeStringToPaise(_selling.Text);
                long mrp = RupeeStringToPaise(_mrp.Text);
                if (selling == 0 || cost == 0) { _marginLabel.Text = ""; return; }
                decimal margin = (decimal)(selling - cost) / selling * 100m;
                string txt = "Margin " + margin.ToString("0.0") + "%";
                if (mrp > 0 && selling > mrp) txt += "   WARNING: selling above MRP";
                _marginLabel.Text = txt;
                _marginLabel.ForeColor = (mrp > 0 && selling > mrp)
                    ? System.Drawing.Color.Red
                    : System.Drawing.Color.DarkGreen;
            }
            catch { _marginLabel.Text = ""; }
        }

        private void Save()
        {
            try
            {
                _it.Sku = _sku.Text.Trim();
                _it.Name = _name.Text.Trim();
                _it.PrintName = string.IsNullOrWhiteSpace(_printName.Text) ? _it.Name : _printName.Text.Trim();
                _it.SoldBy = (SoldBy)Enum.Parse(typeof(SoldBy), (string)_soldBy.SelectedItem);
                _it.Unit = _unit.Text.Trim();
                _it.HsnCode = _hsn.Text.Trim();

                // Every numeric field is validated with a plain message rather than
                // letting int.Parse throw "Input string was not in a correct format".
                int tax, tare, round, minSale;
                if (!ReadWhole(_tax, "Tax rate", 0, 10000, out tax)) return;
                if (!ReadWhole(_tare, "Tare grams", 0, 100000, out tare)) return;
                if (!ReadWhole(_round, "Round to grams", 1, 1000, out round)) return;
                if (!ReadWhole(_minSale, "Minimum sale grams", 0, 100000, out minSale)) return;
                _it.TaxRateBp = tax;
                _it.TareGrams = tare;
                _it.RoundToGrams = round;
                _it.MinSaleGrams = minSale;
                _it.WeighAtCounter = _weighAtCounter.Checked;
                _it.AllowDiscount = _allowDiscount.Checked;
                _it.IsActive = _isActive.Checked;
                _it.DefaultCostPaise = RupeeStringToPaise(_cost.Text);
                _it.DefaultSellingPaise = RupeeStringToPaise(_selling.Text);
                _it.DefaultMrpPaise = RupeeStringToPaise(_mrp.Text);

                if (string.IsNullOrWhiteSpace(_it.Name))
                {
                    Theme.Warn("Enter the item name.");
                    _name.Focus();
                    return;
                }
                if (string.IsNullOrWhiteSpace(_it.Sku))
                {
                    Theme.Warn("Enter a code (SKU) for this item.\r\n" +
                               "It is how the item is found when a barcode does not scan.");
                    _sku.Focus();
                    return;
                }

                // Business rule 6: selling price may never exceed MRP. Previously
                // this was only a coloured label, so an over-MRP price could be saved.
                if (_it.DefaultMrpPaise > 0 && _it.DefaultSellingPaise > _it.DefaultMrpPaise)
                {
                    Theme.Warn(
                        "The selling price cannot be more than the MRP.\r\n" +
                        "Selling: Rs. " + new Money(_it.DefaultSellingPaise) + "\r\n" +
                        "MRP:     Rs. " + new Money(_it.DefaultMrpPaise) + "\r\n\r\n" +
                        "Selling above the printed MRP is not allowed.");
                    _selling.Focus();
                    return;
                }
                if (_it.DefaultCostPaise > 0 && _it.DefaultSellingPaise > 0 &&
                    _it.DefaultSellingPaise < _it.DefaultCostPaise)
                {
                    if (!Theme.Confirm(
                        "The selling price is below what the item costs you.\r\n" +
                        "Cost:    Rs. " + new Money(_it.DefaultCostPaise) + "\r\n" +
                        "Selling: Rs. " + new Money(_it.DefaultSellingPaise) + "\r\n\r\n" +
                        "Every sale would lose money. Save anyway?", "Check the price"))
                        return;
                }

                long id = _ctx.Items.Save(_it, _ctx.CurrentUser.Id);
                // Rewrite barcodes: naive — add new ones only, skip existing
                var existing = new HashSet<string>(_ctx.Items.BarcodesFor(id));
                foreach (var line in (_barcodes.Text ?? "").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var bc = line.Trim();
                    if (bc.Length == 0 || existing.Contains(bc)) continue;
                    try { _ctx.Items.AddBarcode(id, bc, existing.Count == 0); existing.Add(bc); } catch { }
                }
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                if (ex.Message.IndexOf("UNIQUE", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Theme.Warn("Another item already uses the code \"" + _it.Sku + "\"." + Environment.NewLine + Environment.NewLine +
                               "Give this item a different code.");
                    return;
                }
                Theme.Error(ex.Message);
            }
        }

        /// <summary>Reads a whole number, or explains what is wrong and returns false.</summary>
        private bool ReadWhole(TextBox box, string fieldName, int min, int max, out int value)
        {
            string s = (box.Text ?? "").Trim();
            if (s.Length == 0) s = "0";
            if (!int.TryParse(s, out value))
            {
                Theme.Warn(fieldName + " must be a whole number, such as " + min + ".");
                box.Focus();
                box.SelectAll();
                return false;
            }
            if (value < min || value > max)
            {
                Theme.Warn(fieldName + " must be between " + min + " and " + max + ".");
                box.Focus();
                box.SelectAll();
                return false;
            }
            return true;
        }
    }

    public static class CsvImporter
    {
        public static string Import(string path, AppContext ctx)
        {
            int added = 0, updated = 0, failed = 0;
            var errors = new List<string>();
            using (var r = new StreamReader(path))
            {
                string header = r.ReadLine();
                if (header == null) return "Empty file";
                var cols = header.Split(',');
                int idxSku = Array.IndexOf(cols, "sku");
                int idxName = Array.IndexOf(cols, "name");
                int idxSoldBy = Array.IndexOf(cols, "sold_by");
                int idxUnit = Array.IndexOf(cols, "unit");
                int idxTax = Array.IndexOf(cols, "tax_bp");
                int idxHsn = Array.IndexOf(cols, "hsn");
                if (idxName < 0) return "CSV must have a 'name' column";
                string line; int rowNum = 1;
                while ((line = r.ReadLine()) != null)
                {
                    rowNum++;
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var parts = line.Split(',');
                    try
                    {
                        string sku = idxSku >= 0 && idxSku < parts.Length ? parts[idxSku].Trim() : null;
                        Item existing = !string.IsNullOrEmpty(sku) ? ctx.Items.FindBySku(sku) : null;
                        var it = existing ?? new Item { SoldBy = SoldBy.Piece, Unit = "pc", RoundToGrams = 5, MinSaleGrams = 100, AllowDiscount = true, IsActive = true };
                        it.Sku = sku;
                        it.Name = parts[idxName].Trim();
                        it.PrintName = it.Name;
                        if (idxSoldBy >= 0 && idxSoldBy < parts.Length)
                        {
                            var sb = parts[idxSoldBy].Trim().ToLowerInvariant();
                            it.SoldBy = sb == "weight" ? SoldBy.Weight : sb == "volume" ? SoldBy.Volume : SoldBy.Piece;
                        }
                        if (idxUnit >= 0 && idxUnit < parts.Length) it.Unit = parts[idxUnit].Trim();
                        if (idxTax >= 0 && idxTax < parts.Length) { int bp; int.TryParse(parts[idxTax].Trim(), out bp); it.TaxRateBp = bp; }
                        if (idxHsn >= 0 && idxHsn < parts.Length) it.HsnCode = parts[idxHsn].Trim();

                        ctx.Items.Save(it, ctx.CurrentUser.Id);
                        if (existing == null) added++; else updated++;
                    }
                    catch (Exception ex) { failed++; errors.Add("Row " + rowNum + ": " + ex.Message); }
                }
            }
            string msg = "Added " + added + ", updated " + updated + ", failed " + failed;
            if (errors.Count > 0) msg += "\r\n\r\n" + string.Join("\r\n", errors.GetRange(0, Math.Min(20, errors.Count)));
            return msg;
        }
    }

}
