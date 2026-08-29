using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Dapper;
using GroceryPos.Domain;
using GroceryPos.Printing;

namespace GroceryPos.App
{
    // -----------------------------------------------------------------------------------
    // Customer ledger screen
    // -----------------------------------------------------------------------------------
    public class CustomerLedgerForm : Form
    {
        private readonly AppContext _ctx;
        private TextBox _phone;
        private Label _header;
        private DataGridView _grid;
        private Customer _current;
        private List<LedgerEntry> _entries = new List<LedgerEntry>();

        public CustomerLedgerForm(AppContext ctx)
        {
            _ctx = ctx;
            Text = "Customer khata";
            Width = 1000; Height = 620;
            StartPosition = FormStartPosition.CenterParent;

            var top = new Panel { Dock = DockStyle.Top, Height = 80 };
            top.Controls.Add(new Label { Text = "Phone", Left = 8, Top = 12, Width = 40 });
            _phone = new TextBox { Left = 60, Top = 8, Width = 160 };
            top.Controls.Add(_phone);
            var lookup = new Button { Text = "Lookup", Left = 230, Top = 6, Width = 80 };
            lookup.Click += (s, e) => Lookup();
            top.Controls.Add(lookup);
            var newBtn = new Button { Text = "New customer", Left = 320, Top = 6, Width = 120 };
            newBtn.Click += (s, e) => NewCustomer();
            top.Controls.Add(newBtn);
            var payBtn = new Button { Text = "Record payment (F2)", Left = 450, Top = 6, Width = 160 };
            payBtn.Click += (s, e) => RecordPayment();
            top.Controls.Add(payBtn);
            var stmtBtn = new Button { Text = "Print statement (thermal)", Left = 620, Top = 6, Width = 190 };
            stmtBtn.Click += (s, e) => PrintStatement(false);
            top.Controls.Add(stmtBtn);
            var stmtA4 = new Button { Text = "A4 statement", Left = 820, Top = 6, Width = 130 };
            stmtA4.Click += (s, e) => PrintStatement(true);
            top.Controls.Add(stmtA4);

            var limitBtn = new Button { Text = "Adjust limit (owner)", Left = 8, Top = 44, Width = 160 };
            limitBtn.Click += (s, e) => AdjustLimit();
            top.Controls.Add(limitBtn);
            var disable = new Button { Text = "Disable credit", Left = 175, Top = 44, Width = 120 };
            disable.Click += (s, e) => ToggleCredit(false);
            top.Controls.Add(disable);
            var enable = new Button { Text = "Enable credit", Left = 300, Top = 44, Width = 120 };
            enable.Click += (s, e) => ToggleCredit(true);
            top.Controls.Add(enable);
            var writeOff = new Button { Text = "Write-off (owner)", Left = 425, Top = 44, Width = 140 };
            writeOff.Click += (s, e) => new WriteOffForm(_ctx, _current).ShowDialog(this);
            top.Controls.Add(writeOff);
            var adj = new Button { Text = "Adjustment (owner)", Left = 570, Top = 44, Width = 140 };
            adj.Click += (s, e) => new AdjustmentForm(_ctx, _current).ShowDialog(this);
            top.Controls.Add(adj);

            Controls.Add(top);

            _header = new Label { Dock = DockStyle.Top, Height = 60, Font = new Font("Segoe UI", 10F) };
            Controls.Add(_header);

            _grid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, RowHeadersVisible = false, AutoGenerateColumns = false };
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Date", DataPropertyName = "Date", Width = 120 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Type", DataPropertyName = "Type", Width = 90 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Reference", DataPropertyName = "Reference", Width = 130 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Description", DataPropertyName = "Description", Width = 260 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Debit", DataPropertyName = "Debit", Width = 90 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Credit", DataPropertyName = "Credit", Width = 90 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Balance", DataPropertyName = "Balance", Width = 100 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "By", DataPropertyName = "By", Width = 80 });
            _grid.CellDoubleClick += (s, e) => DrillReference();
            Controls.Add(_grid);
        }

        private void Lookup()
        {
            _current = _ctx.Customers.FindByPhone(_phone.Text);
            if (_current == null) { MessageBox.Show("Not found"); return; }
            Reload();
        }

        private void NewCustomer()
        {
            using (var f = new CustomerEditForm(_ctx))
            {
                if (f.ShowDialog() == DialogResult.OK)
                {
                    _phone.Text = f.SavedPhone;
                    Lookup();
                }
            }
        }

        private void Reload()
        {
            _entries = _ctx.CustomerLedger.ForCustomer(_current.Id).ToList();
            long outstanding = _current.CurrentBalancePaise;
            var oldestUnpaid = OldestUnpaidDays();
            var lastPayment = LastPaymentDate();
            _header.Text = "Name: " + _current.Name + "   |   Outstanding: Rs. " + new Money(outstanding) +
                "   |   Limit: Rs. " + new Money(_current.CreditLimitPaise) +
                "   |   Available: Rs. " + new Money(_current.CreditLimitPaise - outstanding) +
                "\r\nOldest unpaid: " + (oldestUnpaid.HasValue ? oldestUnpaid.Value + " days" : "—") +
                "   |   Last payment: " + (lastPayment.HasValue ? lastPayment.Value.ToString("dd/MM/yy") : "—") +
                "   |   Since: " + _current.Since.ToString("dd/MM/yy") +
                "   |   Credit allowed: " + (_current.CreditAllowed ? "YES" : "NO");

            var users = _ctx.Users.All().ToDictionary(u => u.Id, u => u.Name);
            var display = _entries.Select(e => new
            {
                Date = e.At.ToString("dd/MM/yy HH:mm"),
                Type = e.Type.ToString(),
                Reference = e.RefTable == "bills" && e.RefId.HasValue ? "INV-" + e.RefId : (e.RefTable ?? ""),
                Description = e.Description,
                Debit = e.DebitPaise > 0 ? new Money(e.DebitPaise).ToString() : "",
                Credit = e.CreditPaise > 0 ? new Money(e.CreditPaise).ToString() : "",
                Balance = new Money(e.BalancePaise).ToString(),
                By = users.ContainsKey(e.UserId) ? users[e.UserId] : "?",
                _entry = e
            }).ToList();
            _grid.DataSource = display;
        }

        private int? OldestUnpaidDays()
        {
            using (var c = _ctx.Db.Open())
            {
                var d = c.QueryFirstOrDefault<string>(@"
                    SELECT MIN(b.billed_at) FROM bills b
                    WHERE b.customer_id=@c AND b.is_credit_sale=1 AND b.status='completed'
                      AND (b.net_paise - COALESCE((SELECT SUM(allocated_paise) FROM credit_allocations WHERE bill_id=b.id),0)) > 0",
                    new { c = _current.Id });
                if (d == null) return null;
                return (int)(DateTime.Now - DateTime.Parse(d)).TotalDays;
            }
        }

        private DateTime? LastPaymentDate()
        {
            using (var c = _ctx.Db.Open())
            {
                var d = c.QueryFirstOrDefault<string>(
                    "SELECT MAX(received_at) FROM credit_payments WHERE customer_id=@c", new { c = _current.Id });
                return d == null ? (DateTime?)null : DateTime.Parse(d);
            }
        }

        private void DrillReference()
        {
            if (_grid.CurrentRow == null) return;
            dynamic row = _grid.CurrentRow.DataBoundItem;
            LedgerEntry e = (LedgerEntry)row._entry;
            if (e.RefTable == "bills" && e.RefId.HasValue)
            {
                var bill = _ctx.Bills.FindById(e.RefId.Value);
                if (bill != null) new BillDetailForm(bill).ShowDialog(this);
            }
        }

        private void RecordPayment()
        {
            if (_current == null) { MessageBox.Show("Lookup a customer first"); return; }
            using (var f = new CreditPaymentForm(_ctx, _current))
            {
                if (f.ShowDialog() == DialogResult.OK)
                {
                    _current = _ctx.Customers.FindById(_current.Id);
                    Reload();
                }
            }
        }

        private void AdjustLimit()
        {
            if (_current == null) return;
            if (_ctx.CurrentUser.Role != UserRole.Owner) { MessageBox.Show("Owner only"); return; }
            string s = Prompt("New limit (Rs.)", new Money(_current.CreditLimitPaise).ToString());
            if (s == null) return;
            try
            {
                _ctx.Customers.SetCreditLimit(_current.Id, Money.ParseRupees(s).Paise, _ctx.CurrentUser.Id);
                _current = _ctx.Customers.FindById(_current.Id);
                Reload();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void ToggleCredit(bool allowed)
        {
            if (_current == null) return;
            if (_ctx.CurrentUser.Role == UserRole.Cashier) { MessageBox.Show("Manager or owner only"); return; }
            _ctx.Customers.SetCreditAllowed(_current.Id, allowed, _ctx.CurrentUser.Id);
            _current = _ctx.Customers.FindById(_current.Id);
            Reload();
        }

        private void PrintStatement(bool a4)
        {
            if (_current == null) return;
            var h = new StatementFormatter.Header
            {
                StoreName = _ctx.Settings.Get("store.name", "STORE"),
                StoreAddress = _ctx.Settings.Get("store.address", ""),
                CustomerName = _current.Name,
                CustomerPhone = _current.Phone,
                From = _entries.Count > 0 ? _entries[0].At : DateTime.Today.AddMonths(-1),
                To = DateTime.Now,
                PrintedBy = _ctx.CurrentUser.Name
            };
            long opening = 0;
            long closing = _current.CurrentBalancePaise;
            var lines = new StatementFormatter().Format(h, opening, _entries, closing);
            if (a4)
            {
                PrintA4(lines);
            }
            else
            {
                var q = _ctx.Settings.Get("printer.queue", "");
                if (string.IsNullOrEmpty(q)) { MessageBox.Show("Set printer.queue in Settings"); return; }
                var bytes = EscPos.Build(lines, cut: true, drawerKick: false, drawerPin: 0);
                try { _ctx.Printer.Print(q, bytes); } catch (Exception ex) { MessageBox.Show(ex.Message); return; }
            }
            _ctx.Audit.Write(_ctx.CurrentUser.Id, "statement_print", "customer", _current.Id, null, new { a4 });
        }

        private void PrintA4(IList<string> lines)
        {
            var doc = new PrintDocument();
            int idx = 0;
            doc.PrintPage += (s, e) =>
            {
                var f = new Font("Courier New", 10F);
                float y = 40; float x = 40;
                while (idx < lines.Count)
                {
                    e.Graphics.DrawString(lines[idx], f, Brushes.Black, x, y);
                    y += 14; idx++;
                    if (y > e.MarginBounds.Bottom) { e.HasMorePages = true; return; }
                }
                e.HasMorePages = false;
            };
            using (var dlg = new PrintDialog { Document = doc })
            {
                if (dlg.ShowDialog() == DialogResult.OK) doc.Print();
            }
        }

        internal static string Prompt(string label, string initial = "")
        {
            using (var f = new Form { Text = label, Width = 400, Height = 140, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog })
            {
                var t = new TextBox { Left = 10, Top = 20, Width = 360, Text = initial ?? "" };
                var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Left = 200, Top = 60, Width = 80 };
                var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Left = 290, Top = 60, Width = 80 };
                f.Controls.AddRange(new Control[] { t, ok, cancel });
                f.AcceptButton = ok; f.CancelButton = cancel;
                return f.ShowDialog() == DialogResult.OK ? t.Text : null;
            }
        }
    }

    // -----------------------------------------------------------------------------------
    // New customer
    // -----------------------------------------------------------------------------------
    public class CustomerEditForm : Form
    {
        private readonly AppContext _ctx;
        private TextBox _phone, _name, _address, _limit;
        private CheckBox _creditAllowed;
        public string SavedPhone;

        public CustomerEditForm(AppContext ctx)
        {
            _ctx = ctx;
            Text = "New customer"; Width = 400; Height = 320;
            StartPosition = FormStartPosition.CenterParent;
            int y = 12;
            Controls.Add(new Label { Text = "Phone", Left = 8, Top = y + 3, Width = 80 });
            _phone = new TextBox { Left = 100, Top = y, Width = 260 }; Controls.Add(_phone); y += 30;
            Controls.Add(new Label { Text = "Name", Left = 8, Top = y + 3, Width = 80 });
            _name = new TextBox { Left = 100, Top = y, Width = 260 }; Controls.Add(_name); y += 30;
            Controls.Add(new Label { Text = "Address", Left = 8, Top = y + 3, Width = 80 });
            _address = new TextBox { Left = 100, Top = y, Width = 260 }; Controls.Add(_address); y += 30;
            Controls.Add(new Label { Text = "Credit limit (Rs)", Left = 8, Top = y + 3, Width = 120 });
            _limit = new TextBox { Left = 130, Top = y, Width = 100, Text = "0" }; Controls.Add(_limit); y += 30;
            _creditAllowed = new CheckBox { Text = "Credit allowed (owner/manager sets)", Left = 8, Top = y, Width = 300 }; Controls.Add(_creditAllowed); y += 30;
            var save = new Button { Text = "Save", Left = 100, Top = y + 10, Width = 120 };
            save.Click += (s, e) => Save();
            Controls.Add(save);
        }

        private void Save()
        {
            try
            {
                var cust = new Customer
                {
                    Phone = _phone.Text.Trim(),
                    Name = _name.Text.Trim(),
                    Address = _address.Text,
                    CreditLimitPaise = Money.ParseRupees(_limit.Text).Paise,
                    CreditAllowed = _creditAllowed.Checked
                };
                _ctx.Customers.Create(cust, _ctx.CurrentUser.Id);
                SavedPhone = cust.Phone;
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }
    }

    // -----------------------------------------------------------------------------------
    // Bill detail (drill-through)
    // -----------------------------------------------------------------------------------
    public class BillDetailForm : Form
    {
        public BillDetailForm(Bill bill)
        {
            Text = "Bill INV-" + bill.BillNo;
            Width = 700; Height = 500;
            StartPosition = FormStartPosition.CenterParent;
            var txt = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Font = new Font("Consolas", 9F) };
            var s = new System.Text.StringBuilder();
            s.AppendLine("Bill INV-" + bill.BillNo + "   " + bill.BilledAt.ToString("dd/MM/yy HH:mm"));
            s.AppendLine("Status: " + bill.Status);
            s.AppendLine("Net: Rs. " + new Money(bill.NetPaise));
            s.AppendLine("---- Lines ----");
            foreach (var l in bill.Lines)
            {
                s.AppendLine(l.LineNo + ". " + l.ItemName + "  units=" + l.QtyUnits + " grams=" + l.QtyGrams +
                    "  rate=" + new Money(l.RatePaise) + "  amt=" + new Money(l.AmountPaise));
            }
            s.AppendLine("---- Payments ----");
            foreach (var p in bill.Payments)
                s.AppendLine(p.Mode + " Rs. " + new Money(p.AmountPaise) + " ref=" + p.Reference);
            txt.Text = s.ToString();
            Controls.Add(txt);
        }
    }

    // -----------------------------------------------------------------------------------
    // Credit payment
    // -----------------------------------------------------------------------------------
    public class CreditPaymentForm : Form
    {
        private readonly AppContext _ctx;
        private readonly Customer _cust;
        private TextBox _amount, _reference, _note;
        private ComboBox _mode;
        private DataGridView _allocGrid;
        private CheckBox _override;
        private List<AllocRow> _rows = new List<AllocRow>();

        public CreditPaymentForm(AppContext ctx, Customer cust)
        {
            _ctx = ctx; _cust = cust;
            Text = "Record credit payment — " + cust.Name;
            Width = 800; Height = 500;
            StartPosition = FormStartPosition.CenterParent;

            int y = 12;
            Controls.Add(new Label { Text = "Amount (Rs)", Left = 8, Top = y + 3, Width = 100 });
            _amount = new TextBox { Left = 110, Top = y, Width = 120, Text = "0" };
            _amount.TextChanged += (s, e) => RecomputeFifo();
            Controls.Add(_amount);
            Controls.Add(new Label { Text = "Mode", Left = 240, Top = y + 3, Width = 60 });
            _mode = new ComboBox { Left = 300, Top = y, Width = 100, DropDownStyle = ComboBoxStyle.DropDownList };
            _mode.Items.AddRange(new object[] { "Cash", "Upi", "Card" });
            _mode.SelectedIndex = 0;
            Controls.Add(_mode);
            Controls.Add(new Label { Text = "Ref", Left = 410, Top = y + 3, Width = 40 });
            _reference = new TextBox { Left = 450, Top = y, Width = 200 };
            Controls.Add(_reference);
            y += 34;
            Controls.Add(new Label { Text = "Note", Left = 8, Top = y + 3, Width = 60 });
            _note = new TextBox { Left = 110, Top = y, Width = 540 };
            Controls.Add(_note);
            y += 34;
            _override = new CheckBox { Text = "Override FIFO (edit allocations below)", Left = 8, Top = y, Width = 400 };
            Controls.Add(_override);
            y += 28;

            _allocGrid = new DataGridView { Left = 8, Top = y, Width = 760, Height = 220, AutoGenerateColumns = false, RowHeadersVisible = false };
            _allocGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Bill", DataPropertyName = "BillNo", Width = 100, ReadOnly = true });
            _allocGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Date", DataPropertyName = "Date", Width = 100, ReadOnly = true });
            _allocGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Outstanding", DataPropertyName = "Outstanding", Width = 120, ReadOnly = true });
            _allocGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Allocate (Rs)", DataPropertyName = "AllocateRs", Width = 120 });
            Controls.Add(_allocGrid);
            y += 230;

            var save = new Button { Text = "Save payment", Left = 110, Top = y, Width = 160 };
            save.Click += (s, e) => Save();
            Controls.Add(save);
            Load += (s, e) => LoadOpen();
        }

        private void LoadOpen()
        {
            using (var c = _ctx.Db.Open())
            {
                var rows = c.Query<dynamic>(@"
                    SELECT b.id AS Id, b.bill_no AS BillNo, b.billed_at AS BilledAt, b.net_paise AS Net,
                        COALESCE((SELECT SUM(allocated_paise) FROM credit_allocations WHERE bill_id=b.id),0) AS Allocated
                    FROM bills b
                    WHERE b.customer_id=@c AND b.is_credit_sale=1 AND b.status='completed'
                    ORDER BY b.billed_at, b.id", new { c = _cust.Id }).ToList();
                foreach (var b in rows)
                {
                    long outstanding = (long)b.Net - (long)b.Allocated;
                    if (outstanding <= 0) continue;
                    _rows.Add(new AllocRow
                    {
                        BillId = (long)b.Id, BillNo = "INV-" + (long)b.BillNo,
                        Date = DateTime.Parse((string)b.BilledAt).ToString("dd/MM/yy"),
                        OutstandingPaise = outstanding,
                        Outstanding = new Money(outstanding).ToString(),
                        AllocateRs = "0"
                    });
                }
            }
            _allocGrid.DataSource = _rows;
        }

        private void RecomputeFifo()
        {
            if (_override.Checked) return;
            long remaining;
            try { remaining = Money.ParseRupees(_amount.Text).Paise; } catch { return; }
            foreach (var r in _rows)
            {
                long take = Math.Min(remaining, r.OutstandingPaise);
                r.AllocateRs = new Money(take).ToString();
                remaining -= take;
                if (remaining <= 0) remaining = 0;
            }
            _allocGrid.DataSource = null;
            _allocGrid.DataSource = _rows;
        }

        private void Save()
        {
            try
            {
                long amount = Money.ParseRupees(_amount.Text).Paise;
                if (amount <= 0) { MessageBox.Show("Amount must be > 0"); return; }
                var mode = (PaymentMode)Enum.Parse(typeof(PaymentMode), _mode.Text);
                var shift = _ctx.Shifts.OpenShiftFor(1);
                IList<Tuple<long, long>> overrides = null;
                if (_override.Checked)
                {
                    overrides = new List<Tuple<long, long>>();
                    long sum = 0;
                    foreach (var r in _rows)
                    {
                        long p = Money.ParseRupees(r.AllocateRs ?? "0").Paise;
                        if (p > 0) { overrides.Add(Tuple.Create(r.BillId, p)); sum += p; }
                    }
                    if (sum > amount) { MessageBox.Show("Allocations exceed amount"); return; }
                }
                long pid = _ctx.CreditPayments.Receive(_cust.Id, amount, mode, _reference.Text,
                    _ctx.CurrentUser.Id, shift == null ? (long?)null : shift.Id, _note.Text, overrides);

                // Print thermal receipt
                var q = _ctx.Settings.Get("printer.queue", "");
                if (!string.IsNullOrEmpty(q))
                {
                    var lines = new List<string>();
                    lines.Add(ReceiptFormatter.Center(_ctx.Settings.Get("store.name", "STORE")));
                    lines.Add(ReceiptFormatter.Center("PAYMENT RECEIPT"));
                    lines.Add(new string('-', ReceiptFormatter.Width));
                    lines.Add(ReceiptFormatter.PadPair("Customer", _cust.Name));
                    lines.Add(ReceiptFormatter.PadPair("Amount", "Rs. " + new Money(amount)));
                    lines.Add(ReceiptFormatter.PadPair("Mode", mode.ToString()));
                    if (!string.IsNullOrEmpty(_reference.Text))
                        lines.Add(ReceiptFormatter.PadPair("Ref", _reference.Text));
                    lines.Add(ReceiptFormatter.PadPair("Received by", _ctx.CurrentUser.Name));
                    long newBal = _cust.CurrentBalancePaise - amount;
                    lines.Add(ReceiptFormatter.PadPair("New balance", "Rs. " + new Money(newBal)));
                    lines.Add(ReceiptFormatter.PadPair("At", DateTime.Now.ToString("dd/MM/yy HH:mm")));
                    try { _ctx.Printer.Print(q, EscPos.Build(lines, true, false, 0)); } catch { }
                }
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        public class AllocRow
        {
            public long BillId { get; set; }
            public string BillNo { get; set; }
            public string Date { get; set; }
            public long OutstandingPaise { get; set; }
            public string Outstanding { get; set; }
            public string AllocateRs { get; set; }
        }
    }

    // -----------------------------------------------------------------------------------
    // Opening balance import
    // -----------------------------------------------------------------------------------
    public class OpeningBalanceImportForm : Form
    {
        private readonly AppContext _ctx;
        private TextBox _log;

        public OpeningBalanceImportForm(AppContext ctx)
        {
            _ctx = ctx;
            Text = "Opening balance import (owner only)";
            Width = 800; Height = 500;
            StartPosition = FormStartPosition.CenterParent;
            var open = new Button { Text = "Pick CSV (phone,name,opening_paise,as_of_date)", Dock = DockStyle.Top, Height = 40 };
            open.Click += (s, e) => Pick();
            Controls.Add(open);
            _log = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Font = new Font("Consolas", 9F) };
            Controls.Add(_log);
        }

        private void Pick()
        {
            if (_ctx.CurrentUser.Role != UserRole.Owner) { MessageBox.Show("Owner only"); return; }
            using (var d = new OpenFileDialog { Filter = "CSV|*.csv" })
            {
                if (d.ShowDialog() != DialogResult.OK) return;
                Import(d.FileName);
            }
        }

        private void Import(string path)
        {
            var log = new System.Text.StringBuilder();
            int ok = 0, err = 0;
            using (var r = new StreamReader(path))
            {
                string header = r.ReadLine();
                log.AppendLine("Header: " + header);
                int line = 1;
                while (!r.EndOfStream)
                {
                    line++;
                    var s = r.ReadLine();
                    if (string.IsNullOrWhiteSpace(s)) continue;
                    var f = s.Split(',');
                    if (f.Length < 4) { err++; log.AppendLine("Line " + line + ": too few fields"); continue; }
                    try
                    {
                        string phone = f[0].Trim();
                        string name = f[1].Trim();
                        long paise = long.Parse(f[2].Trim());
                        DateTime at = DateTime.Parse(f[3].Trim());
                        var cust = _ctx.Customers.FindByPhone(phone);
                        if (cust == null)
                        {
                            long id = _ctx.Customers.Create(new Customer { Phone = phone, Name = name, CreditAllowed = true }, _ctx.CurrentUser.Id);
                            cust = _ctx.Customers.FindById(id);
                        }
                        _ctx.CustomerLedger.WriteOpening(cust.Id, paise, at, _ctx.CurrentUser.Id);
                        ok++;
                        log.AppendLine("Line " + line + ": " + name + " opening " + new Money(paise));
                    }
                    catch (Exception ex) { err++; log.AppendLine("Line " + line + ": " + ex.Message); }
                }
            }
            log.AppendLine("---");
            log.AppendLine("Imported " + ok + ", errors " + err);
            _log.Text = log.ToString();
            MessageBox.Show("Done. Imported " + ok + ", errors " + err);
        }
    }

    // -----------------------------------------------------------------------------------
    // Ageing report
    // -----------------------------------------------------------------------------------
    public class AgeingReportForm : Form
    {
        public AgeingReportForm(AppContext ctx)
        {
            Text = "Ageing report"; Width = 900; Height = 560;
            StartPosition = FormStartPosition.CenterParent;
            var grid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, RowHeadersVisible = false };
            Controls.Add(grid);
            Load += (s, e) =>
            {
                var customers = ctx.Customers.WithOutstanding();
                var rows = new List<object>();
                foreach (var c in customers)
                {
                    var b = ctx.CreditPayments.Ageing(c.Id, DateTime.Now);
                    long b0 = b.Where(x => x.Bucket == "0-30").Sum(x => x.OutstandingPaise);
                    long b1 = b.Where(x => x.Bucket == "31-60").Sum(x => x.OutstandingPaise);
                    long b2 = b.Where(x => x.Bucket == "61-90").Sum(x => x.OutstandingPaise);
                    long b3 = b.Where(x => x.Bucket == ">90").Sum(x => x.OutstandingPaise);
                    rows.Add(new
                    {
                        Name = c.Name,
                        Phone = c.Phone,
                        Outstanding = new Money(c.CurrentBalancePaise).ToString(),
                        B0_30 = new Money(b0).ToString(),
                        B31_60 = new Money(b1).ToString(),
                        B61_90 = new Money(b2).ToString(),
                        Over90 = new Money(b3).ToString()
                    });
                }
                grid.DataSource = rows;
            };
        }
    }

    // -----------------------------------------------------------------------------------
    // Shift form (open / close)
    // -----------------------------------------------------------------------------------
    public class ShiftForm : Form
    {
        private readonly AppContext _ctx;
        private Label _status;
        private DataGridView _denomGrid;
        private Label _summary;

        private static readonly long[] Denoms = new long[] { 200000, 50000, 20000, 10000, 5000, 2000, 1000, 500, 200, 100, 1000, 500, 200, 100 };
        // 2000, 500, 200, 100, 50, 20, 10, 5, 2, 1 notes; 10, 5, 2, 1 coins

        public ShiftForm(AppContext ctx)
        {
            _ctx = ctx;
            Text = "Shift / day close";
            Width = 700; Height = 620;
            StartPosition = FormStartPosition.CenterParent;

            _status = new Label { Dock = DockStyle.Top, Height = 40, Font = new Font("Segoe UI", 10F) };
            Controls.Add(_status);

            var openBtn = new Button { Text = "Open shift (opening float)", Dock = DockStyle.Top, Height = 36 };
            openBtn.Click += (s, e) => OpenShift();
            Controls.Add(openBtn);

            var pettyBtn = new Button { Text = "Record petty cash", Dock = DockStyle.Top, Height = 36 };
            pettyBtn.Click += (s, e) => new PettyCashForm(_ctx).ShowDialog(this);
            Controls.Add(pettyBtn);

            _denomGrid = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false, RowHeadersVisible = false };
            _denomGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Denom (Rs)", DataPropertyName = "Denom", Width = 100, ReadOnly = true });
            _denomGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Count", DataPropertyName = "Count", Width = 100 });
            _denomGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Total (Rs)", DataPropertyName = "Total", Width = 120, ReadOnly = true });
            _denomGrid.CellEndEdit += (s, e) => RefreshTotals();
            Controls.Add(_denomGrid);

            _summary = new Label { Dock = DockStyle.Bottom, Height = 60, Font = new Font("Segoe UI", 10F) };
            Controls.Add(_summary);

            var closeBtn = new Button { Text = "Close shift (writes Z report)", Dock = DockStyle.Bottom, Height = 40 };
            closeBtn.Click += (s, e) => CloseShift();
            Controls.Add(closeBtn);

            Load += (s, e) => Reload();
        }

        private void OpenShift()
        {
            string sf = CustomerLedgerForm.Prompt("Opening float (Rs)", "0");
            if (sf == null) return;
            try
            {
                _ctx.Shifts.Open(1, _ctx.CurrentUser.Id, Money.ParseRupees(sf).Paise);
                Reload();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void Reload()
        {
            var sh = _ctx.Shifts.OpenShiftFor(1);
            if (sh == null)
            {
                _status.Text = "No open shift on counter 1.";
                _denomGrid.DataSource = null;
                _summary.Text = "";
                return;
            }
            _status.Text = "Shift #" + sh.Id + " opened " + sh.OpenedAt.ToString("dd/MM/yy HH:mm") +
                " by user " + sh.UserId + " — float Rs. " + new Money(sh.OpeningFloatPaise);
            var labels = new[] { "2000 note", "500 note", "200 note", "100 note", "50 note", "20 note", "10 note", "5 note", "2 note", "1 note", "10 coin", "5 coin", "2 coin", "1 coin" };
            var rows = new List<DenomRow>();
            for (int i = 0; i < Denoms.Length; i++)
                rows.Add(new DenomRow { DenomPaise = Denoms[i], Denom = labels[i], Count = 0, Total = "0.00" });
            _denomGrid.DataSource = rows;
            _currentRows = rows;
            var nc = _ctx.Shifts.NonCashTotals(sh.Id, 1);
            long expected = _ctx.Shifts.ExpectedCash(sh.Id, 1);
            _summary.Text = "Expected cash: Rs. " + new Money(expected) +
                "   |   UPI: Rs. " + new Money(nc["upi"]) +
                "   |   Card: Rs. " + new Money(nc["card"]) +
                "   |   Khata: Rs. " + new Money(nc["khata"]);
        }

        private List<DenomRow> _currentRows;

        private void RefreshTotals()
        {
            long tot = 0;
            foreach (var r in _currentRows) { r.Total = new Money(r.DenomPaise * r.Count).ToString(); tot += r.DenomPaise * r.Count; }
            _denomGrid.Refresh();
        }

        private void CloseShift()
        {
            var sh = _ctx.Shifts.OpenShiftFor(1);
            if (sh == null) { MessageBox.Show("No open shift"); return; }
            var list = new List<Tuple<long, int>>();
            long counted = 0;
            foreach (var r in _currentRows)
            {
                list.Add(Tuple.Create(r.DenomPaise, r.Count));
                counted += r.DenomPaise * r.Count;
            }
            try
            {
                _ctx.Shifts.Close(sh.Id, list, _ctx.CurrentUser.Id);
                var nc = _ctx.Shifts.NonCashTotals(sh.Id, 1);
                var closed = _ctx.Shifts.FindById(sh.Id);
                // Print Z report
                var q = _ctx.Settings.Get("printer.queue", "");
                if (!string.IsNullOrEmpty(q))
                {
                    var lines = new List<string>();
                    lines.Add(ReceiptFormatter.Center(_ctx.Settings.Get("store.name", "STORE")));
                    lines.Add(ReceiptFormatter.Center("Z REPORT"));
                    lines.Add(new string('-', ReceiptFormatter.Width));
                    lines.Add(ReceiptFormatter.PadPair("Shift", "#" + sh.Id));
                    lines.Add(ReceiptFormatter.PadPair("Opened", sh.OpenedAt.ToString("dd/MM/yy HH:mm")));
                    lines.Add(ReceiptFormatter.PadPair("Closed", DateTime.Now.ToString("dd/MM/yy HH:mm")));
                    lines.Add(ReceiptFormatter.PadPair("Expected cash", "Rs. " + new Money(closed.ExpectedCashPaise)));
                    lines.Add(ReceiptFormatter.PadPair("Counted cash", "Rs. " + new Money(closed.CountedCashPaise)));
                    lines.Add(ReceiptFormatter.PadPair("Difference", "Rs. " + new Money(closed.DifferencePaise)));
                    lines.Add(ReceiptFormatter.PadPair("UPI", "Rs. " + new Money(nc["upi"])));
                    lines.Add(ReceiptFormatter.PadPair("Card", "Rs. " + new Money(nc["card"])));
                    lines.Add(ReceiptFormatter.PadPair("Khata", "Rs. " + new Money(nc["khata"])));
                    try { _ctx.Printer.Print(q, EscPos.Build(lines, true, false, 0)); } catch { }
                }
                MessageBox.Show("Shift closed. Counted Rs. " + new Money(counted) + ", diff Rs. " + new Money(closed.DifferencePaise));
                Reload();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        public class DenomRow
        {
            public long DenomPaise { get; set; }
            public string Denom { get; set; }
            public int Count { get; set; }
            public string Total { get; set; }
        }
    }

    // -----------------------------------------------------------------------------------
    // Petty cash
    // -----------------------------------------------------------------------------------
    public class PettyCashForm : Form
    {
        public PettyCashForm(AppContext ctx)
        {
            Text = "Petty cash"; Width = 400; Height = 220;
            StartPosition = FormStartPosition.CenterParent;
            int y = 12;
            Controls.Add(new Label { Text = "Amount (Rs)", Left = 8, Top = y + 3, Width = 100 });
            var amt = new TextBox { Left = 110, Top = y, Width = 100 };
            Controls.Add(amt); y += 34;
            Controls.Add(new Label { Text = "Note", Left = 8, Top = y + 3, Width = 100 });
            var note = new TextBox { Left = 110, Top = y, Width = 260 };
            Controls.Add(note); y += 40;
            var save = new Button { Text = "Save", Left = 110, Top = y, Width = 100 };
            save.Click += (s, e) =>
            {
                try
                {
                    var sh = ctx.Shifts.OpenShiftFor(1);
                    if (sh == null) { MessageBox.Show("No open shift"); return; }
                    ctx.Shifts.RecordPettyCash(sh.Id, Money.ParseRupees(amt.Text).Paise, note.Text, ctx.CurrentUser.Id);
                    Close();
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
            };
            Controls.Add(save);
        }
    }

    // -----------------------------------------------------------------------------------
    // Write-off / adjustment (owner)
    // -----------------------------------------------------------------------------------
    public class WriteOffForm : Form
    {
        public WriteOffForm(AppContext ctx, Customer cust)
        {
            if (cust == null) { Text = "Pick a customer first"; return; }
            Text = "Write-off — " + cust.Name; Width = 500; Height = 240;
            StartPosition = FormStartPosition.CenterParent;
            int y = 12;
            Controls.Add(new Label { Text = "Amount (Rs)", Left = 8, Top = y + 3, Width = 100 });
            var amt = new TextBox { Left = 110, Top = y, Width = 120 };
            Controls.Add(amt); y += 34;
            Controls.Add(new Label { Text = "Reason", Left = 8, Top = y + 3, Width = 100 });
            var reason = new TextBox { Left = 110, Top = y, Width = 360 };
            Controls.Add(reason); y += 40;
            var save = new Button { Text = "Write off", Left = 110, Top = y, Width = 120 };
            save.Click += (s, e) =>
            {
                try
                {
                    if (ctx.CurrentUser.Role != UserRole.Owner) { MessageBox.Show("Owner only"); return; }
                    ctx.CustomerLedger.WriteWriteOff(cust.Id, Money.ParseRupees(amt.Text).Paise, reason.Text, ctx.CurrentUser.Id);
                    MessageBox.Show("Written off");
                    Close();
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
            };
            Controls.Add(save);
        }
    }

    public class AdjustmentForm : Form
    {
        public AdjustmentForm(AppContext ctx, Customer cust)
        {
            if (cust == null) { Text = "Pick a customer first"; return; }
            Text = "Adjustment — " + cust.Name; Width = 500; Height = 280;
            StartPosition = FormStartPosition.CenterParent;
            int y = 12;
            Controls.Add(new Label { Text = "Debit (Rs)", Left = 8, Top = y + 3, Width = 100 });
            var deb = new TextBox { Left = 110, Top = y, Width = 100, Text = "0" };
            Controls.Add(deb); y += 34;
            Controls.Add(new Label { Text = "Credit (Rs)", Left = 8, Top = y + 3, Width = 100 });
            var cr = new TextBox { Left = 110, Top = y, Width = 100, Text = "0" };
            Controls.Add(cr); y += 34;
            Controls.Add(new Label { Text = "Reason", Left = 8, Top = y + 3, Width = 100 });
            var reason = new TextBox { Left = 110, Top = y, Width = 360 };
            Controls.Add(reason); y += 40;
            var save = new Button { Text = "Save", Left = 110, Top = y, Width = 120 };
            save.Click += (s, e) =>
            {
                try
                {
                    if (ctx.CurrentUser.Role != UserRole.Owner) { MessageBox.Show("Owner only"); return; }
                    ctx.CustomerLedger.WriteAdjustment(cust.Id,
                        Money.ParseRupees(deb.Text).Paise, Money.ParseRupees(cr.Text).Paise,
                        reason.Text, ctx.CurrentUser.Id);
                    MessageBox.Show("Saved"); Close();
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
            };
            Controls.Add(save);
        }
    }
}
