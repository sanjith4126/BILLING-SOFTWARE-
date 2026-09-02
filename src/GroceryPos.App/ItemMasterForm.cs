using System;
using System.Collections.Generic;
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

        public ItemMasterForm(AppContext ctx)
        {
            _ctx = ctx;
            Text = "Item Master";
            Width = 900; Height = 560;
            StartPosition = FormStartPosition.CenterParent;

            var top = new Panel { Dock = DockStyle.Top, Height = 40 };
            var lbl = new Label { Text = "Search:", Left = 8, Top = 12, Width = 60 };
            _search = new TextBox { Left = 70, Top = 8, Width = 250 };
            _search.TextChanged += (s, e) => Reload();
            var newBtn = new Button { Text = "New", Left = 330, Top = 6, Width = 80 };
            newBtn.Click += (s, e) => EditItem(new Item { SoldBy = SoldBy.Piece, Unit = "pc", IsActive = true, RoundToGrams = 5, MinSaleGrams = 100, AllowDiscount = true });
            var editBtn = new Button { Text = "Edit", Left = 420, Top = 6, Width = 80 };
            editBtn.Click += (s, e) => EditSelected();
            var importBtn = new Button { Text = "Import CSV", Left = 510, Top = 6, Width = 100 };
            importBtn.Click += (s, e) => ImportCsv();
            top.Controls.AddRange(new Control[] { lbl, _search, newBtn, editBtn, importBtn });

            _grid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect, AutoGenerateColumns = false };
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "ID", DataPropertyName = "Id", Width = 50 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "SKU", DataPropertyName = "Sku", Width = 100 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Name", DataPropertyName = "Name", Width = 280 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Sold by", DataPropertyName = "SoldBy", Width = 80 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Unit", DataPropertyName = "Unit", Width = 60 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Tax bp", DataPropertyName = "TaxRateBp", Width = 60 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "HSN", DataPropertyName = "HsnCode", Width = 80 });
            _grid.CellDoubleClick += (s, e) => EditSelected();

            Controls.Add(_grid);
            Controls.Add(top);
            Reload();
        }

        private void Reload() { _grid.DataSource = _ctx.Items.Search(_search.Text); }

        private void EditSelected()
        {
            if (_grid.CurrentRow == null) return;
            var it = (Item)_grid.CurrentRow.DataBoundItem;
            var full = _ctx.Items.FindById(it.Id);
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
                _it.TaxRateBp = int.Parse(_tax.Text);
                _it.TareGrams = int.Parse(_tare.Text);
                _it.RoundToGrams = int.Parse(_round.Text);
                _it.MinSaleGrams = int.Parse(_minSale.Text);
                _it.WeighAtCounter = _weighAtCounter.Checked;
                _it.AllowDiscount = _allowDiscount.Checked;
                _it.IsActive = _isActive.Checked;
                _it.DefaultCostPaise = RupeeStringToPaise(_cost.Text);
                _it.DefaultSellingPaise = RupeeStringToPaise(_selling.Text);
                _it.DefaultMrpPaise = RupeeStringToPaise(_mrp.Text);

                if (string.IsNullOrWhiteSpace(_it.Name)) throw new Exception("Name is required");
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
            catch (Exception ex) { MessageBox.Show(ex.Message); }
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
