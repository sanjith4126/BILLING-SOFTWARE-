using System;
using System.Collections.Generic;
using System.ComponentModel;
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
            Theme.ApplyForm(this);
            Text = "Customer credit (kadan)";
            Width = 1100; Height = 680;
            MinimumSize = new Size(900, 560);
            StartPosition = FormStartPosition.CenterScreen;

            var pageHeader = Theme.Header("Customer credit (kadan)",
                "Who owes what, and what it was for.");

            var top = new Panel
            {
                Dock = DockStyle.Top,
                Height = 62,
                BackColor = Theme.Surface,
                Padding = new Padding(Theme.Md, Theme.Sm, Theme.Md, Theme.Sm)
            };
            var lblPhone = Theme.FieldLabel("Customer phone number");
            lblPhone.SetBounds(Theme.Md, 2, 220, 16);
            top.Controls.Add(lblPhone);
            _phone = Theme.TextField(180);
            _phone.SetBounds(Theme.Md, 20, 180, Theme.FieldHeight);
            top.Controls.Add(_phone);

            var lookup = Theme.PrimaryButton("Find");
            lookup.SetBounds(206, 20, 90, Theme.ButtonHeight);
            lookup.Click += (s, e) => Lookup();
            top.Controls.Add(lookup);

            var newBtn = Theme.SecondaryButton("New customer");
            newBtn.SetBounds(306, 20, 140, Theme.ButtonHeight);
            newBtn.Click += (s, e) => NewCustomer();
            top.Controls.Add(newBtn);

            var payBtn = Theme.PrimaryButton("Record a payment");
            payBtn.SetBounds(456, 20, 170, Theme.ButtonHeight);
            payBtn.Click += (s, e) => RecordPayment();
            top.Controls.Add(payBtn);

            var stmtBtn = Theme.SecondaryButton("Print statement");
            stmtBtn.SetBounds(636, 20, 150, Theme.ButtonHeight);
            stmtBtn.Click += (s, e) => PrintStatement(false);
            top.Controls.Add(stmtBtn);

            var stmtA4 = Theme.SecondaryButton("A4 statement");
            stmtA4.SetBounds(796, 20, 140, Theme.ButtonHeight);
            stmtA4.Click += (s, e) => PrintStatement(true);
            top.Controls.Add(stmtA4);

            // Credit limits, enable/disable, write-off and adjustment were all
            // removed at the shop's request: they extend credit on judgement, not
            // on a number, and the extra buttons only got in the way.

            _header = new Label { Dock = DockStyle.Top, Height = 60, Font = new Font("Segoe UI", 10F) };

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

            // Docked children are laid out in reverse order of addition, so the
            // Fill grid must go in first or it covers the balance header above it.
            Controls.Add(_grid);
            Controls.Add(_header);
            Controls.Add(top);
            Controls.Add(pageHeader);
            Theme.Retrofit(this);
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
            // A cleared balance is the thing the owner most wants to see at a
            // glance, so it is stated in a word rather than left as "0.00".
            bool settled = outstanding == 0;
            bool inAdvance = outstanding < 0;

            string status = settled ? "SETTLED - nothing owed"
                          : inAdvance ? "IN ADVANCE Rs. " + new Money(-outstanding)
                          : "OWES Rs. " + new Money(outstanding);

            _header.Text = _current.Name + "   (" + (_current.Phone ?? "") + ")" +
                "   |   " + status +
                "\r\nOldest unpaid: " + (oldestUnpaid.HasValue ? oldestUnpaid.Value + " days" : "-") +
                "   |   Last payment: " + (lastPayment.HasValue ? lastPayment.Value.ToString("dd/MM/yy") : "-") +
                "   |   Customer since: " + _current.Since.ToString("dd/MM/yy");

            _header.ForeColor = settled ? Theme.Success
                              : inAdvance ? Theme.Primary
                              : Theme.Danger;
            _header.Font = Theme.BodyBold;

            var users = _ctx.Users.All().ToDictionary(u => u.Id, u => u.Name);
            var display = _entries.Select(e => new
            {
                Date = e.At.ToString("dd/MM/yy HH:mm"),
                Type = LedgerTypeName(e.Type),
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

        /// <summary>
        /// Plain words for a ledger row's type. The stored value is unchanged;
        /// this is only what the owner reads on the screen.
        /// </summary>
        private static string LedgerTypeName(LedgerType t)
        {
            switch (t)
            {
                case LedgerType.CreditSale: return "Credit sale";
                case LedgerType.WriteOff: return "Write-off";
                case LedgerType.Opening: return "Opening";
                case LedgerType.Payment: return "Payment";
                case LedgerType.Discount: return "Discount";
                case LedgerType.Adjustment: return "Adjustment";
                case LedgerType.Reversal: return "Reversal";
                default: return t.ToString();
            }
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
                StoreName = _ctx.Settings.Get("store_name", "GROCERY STORE"),
                StoreAddress = _ctx.Settings.Get("store_address_1", ""),
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
                var q = _ctx.Settings.Get("printer_name", "");
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
        private TextBox _phone, _name, _address;
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
            // No credit limit and no enable switch: the shop decides who gets
            // credit by knowing the person, and every customer can take it.
            Controls.Add(new Label
            {
                Text = "Anyone added here can buy on credit (kadan).",
                Left = 8, Top = y + 3, Width = 350, Height = 20,
                ForeColor = Theme.Muted
            });
            y += 30;
            var save = new Button { Text = "Save", Left = 100, Top = y + 10, Width = 120 };
            save.Click += (s, e) => Save();
            Controls.Add(save);
            Theme.Retrofit(this);
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
                    // Credit is always allowed, with no ceiling. The ledger still
                    // records every rupee, which is what the owner actually needs.
                    CreditLimitPaise = 0,
                    CreditAllowed = true
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
                s.AppendLine(GroceryPos.Printing.ReceiptFormatter.PaymentModeName(p.Mode) +
                    " Rs. " + new Money(p.AmountPaise) + " ref=" + p.Reference);
            txt.Text = s.ToString();
            Controls.Add(txt);
            Theme.Retrofit(this);
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
            Theme.Retrofit(this);
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
                var q = _ctx.Settings.Get("printer_name", "");
                if (!string.IsNullOrEmpty(q))
                {
                    var lines = new List<string>();
                    lines.Add(ReceiptFormatter.Center(_ctx.Settings.Get("store_name", "GROCERY STORE")));
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
            Theme.Retrofit(this);
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
            Theme.Retrofit(this);
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

        /// <summary>
        /// Indian currency actually in circulation, largest first. Value and label
        /// are paired in one table so the two can never drift out of step — they
        /// were previously two parallel arrays, which is easy to break silently.
        /// </summary>
        private static readonly Tuple<long, string>[] Denoms =
        {
            Tuple.Create(50000L, "500 note"),
            Tuple.Create(20000L, "200 note"),
            Tuple.Create(10000L, "100 note"),
            Tuple.Create(5000L,  "50 note"),
            Tuple.Create(2000L,  "20 note"),
            Tuple.Create(1000L,  "10 note"),
            Tuple.Create(2000L,  "20 coin"),
            Tuple.Create(1000L,  "10 coin"),
            Tuple.Create(500L,   "5 coin"),
            Tuple.Create(200L,   "2 coin"),
            Tuple.Create(100L,   "1 coin"),
        };

        public ShiftForm(AppContext ctx)
        {
            _ctx = ctx;
            Text = "Shift / day close";
            Width = 700; Height = 620;
            StartPosition = FormStartPosition.CenterParent;

            Theme.ApplyForm(this);
            Width = 820; Height = 720;
            MinimumSize = new Size(680, 560);
            StartPosition = FormStartPosition.CenterScreen;

            var header = Theme.Header("Shift and day close",
                "Count the cash drawer note by note, then close the day.");

            var top = new Panel
            {
                Dock = DockStyle.Top,
                Height = 96,
                BackColor = Theme.Surface,
                Padding = new Padding(Theme.Md, Theme.Sm, Theme.Md, Theme.Sm)
            };
            _status = new Label
            {
                Dock = DockStyle.Top,
                Height = 44,
                Font = Theme.Body,
                TextAlign = ContentAlignment.MiddleLeft
            };
            var buttons = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = Theme.Surface };
            var openBtn = Theme.SecondaryButton("Open a shift");
            openBtn.SetBounds(0, 0, 170, Theme.ButtonHeight);
            openBtn.Click += (s, e) => OpenShift();
            var pettyBtn = Theme.SecondaryButton("Record petty cash");
            pettyBtn.SetBounds(180, 0, 170, Theme.ButtonHeight);
            pettyBtn.Click += (s, e) => new PettyCashForm(_ctx).ShowDialog(this);
            buttons.Controls.Add(openBtn);
            buttons.Controls.Add(pettyBtn);
            top.Controls.Add(buttons);
            top.Controls.Add(_status);

            var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(Theme.Md, Theme.Sm, Theme.Md, Theme.Sm), BackColor = Theme.Background };
            _denomGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false
            };
            Theme.ApplyGrid(_denomGrid);
            var dCol = Theme.TextColumn("Denom", "Note or coin", 180);
            dCol.ReadOnly = true;
            _denomGrid.Columns.Add(dCol);
            var cCol = Theme.NumberColumn("Count", "How many", 130);
            Theme.MarkEditable(cCol);
            _denomGrid.Columns.Add(cCol);
            var tCol = Theme.NumberColumn("Total", "Value (Rs.)", 150);
            tCol.ReadOnly = true;
            tCol.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _denomGrid.Columns.Add(tCol);
            // Recalculate as the cashier types, not only when the cell loses focus.
            _denomGrid.CellValueChanged += (s, e) => RefreshTotals();
            _denomGrid.CellValidating += DenomGrid_CellValidating;
            body.Controls.Add(_denomGrid);

            var footer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 132,
                BackColor = Theme.Surface,
                Padding = new Padding(Theme.Md, Theme.Sm, Theme.Md, Theme.Sm)
            };
            _summary = new Label
            {
                Dock = DockStyle.Top,
                Height = 76,
                Font = Theme.Data,
                TextAlign = ContentAlignment.TopLeft
            };
            var closeBtn = Theme.PrimaryButton("Close the shift and print the Z report");
            closeBtn.Dock = DockStyle.Bottom;
            closeBtn.Height = 44;
            closeBtn.Click += (s, e) => CloseShift();
            footer.Controls.Add(_summary);
            footer.Controls.Add(closeBtn);

            // Fill first, then the edges, so nothing is hidden underneath.
            Controls.Add(body);
            Controls.Add(footer);
            Controls.Add(top);
            Controls.Add(header);

            Load += (s, e) => Reload();
        }

        private void DenomGrid_CellValidating(object sender, DataGridViewCellValidatingEventArgs e)
        {
            if (_denomGrid.Columns[e.ColumnIndex].DataPropertyName != "Count") return;
            string s = (e.FormattedValue ?? "").ToString().Trim();
            int v;
            if (s.Length == 0 || (int.TryParse(s, out v) && v >= 0)) return;
            Theme.Warn("Enter how many notes or coins you counted, as a whole number.");
            e.Cancel = true;
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
                _status.Text = "No shift is open.\r\n" +
                               "Press \"Open a shift\" and enter the cash you are starting the day with.";
                _denomGrid.DataSource = null;
                _currentRows = null;
                _summary.Text = "";
                _summary.ForeColor = Theme.Muted;
                return;
            }
            _status.Text = "Shift #" + sh.Id + " open since " + sh.OpenedAt.ToString("dd/MM/yy HH:mm") +
                "\r\nStarting cash in the drawer: Rs. " + new Money(sh.OpeningFloatPaise);
            var rows = new List<DenomRow>();
            foreach (var d in Denoms)
                rows.Add(new DenomRow { DenomPaise = d.Item1, Denom = d.Item2, Count = 0, Total = "0.00" });
            _denomGrid.DataSource = new BindingList<DenomRow>(rows);
            _currentRows = rows;
            _nonCash = _ctx.Shifts.NonCashTotals(sh.Id, 1);
            _expectedCash = _ctx.Shifts.ExpectedCash(sh.Id, 1);
            RefreshTotals();
        }

        private List<DenomRow> _currentRows;

        private long _expectedCash;
        private System.Collections.Generic.IDictionary<string, long> _nonCash;

        private void RefreshTotals()
        {
            if (_currentRows == null) return;

            long counted = 0;
            foreach (var r in _currentRows)
            {
                long lineValue = r.DenomPaise * r.Count;
                r.Total = new Money(lineValue).ToString();
                counted += lineValue;
            }
            _denomGrid.Refresh();

            long diff = counted - _expectedCash;
            string diffWord = diff == 0 ? "matches exactly"
                            : diff > 0 ? "OVER by Rs. " + new Money(Math.Abs(diff))
                            : "SHORT by Rs. " + new Money(Math.Abs(diff));

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Expected in the drawer : Rs. " + new Money(_expectedCash));
            sb.AppendLine("You counted            : Rs. " + new Money(counted) + "   -   " + diffWord);
            if (_nonCash != null)
            {
                sb.Append("Not cash: UPI Rs. " + new Money(_nonCash["upi"]) +
                          "   Card Rs. " + new Money(_nonCash["card"]) +
                          "   Credit Rs. " + new Money(_nonCash["khata"]));
            }
            _summary.Text = sb.ToString();
            _summary.ForeColor = diff == 0 ? Theme.Success : Theme.Danger;
        }

        private void CloseShift()
        {
            var sh = _ctx.Shifts.OpenShiftFor(1);
            if (sh == null)
            {
                Theme.Warn("There is no open shift to close.");
                return;
            }
            if (_currentRows == null) return;

            var list = new List<Tuple<long, int>>();
            long counted = 0;
            foreach (var r in _currentRows)
            {
                list.Add(Tuple.Create(r.DenomPaise, r.Count));
                counted += r.DenomPaise * r.Count;
            }

            long diff = counted - _expectedCash;
            string diffLine = diff == 0
                ? "The drawer matches exactly."
                : diff > 0
                    ? "There is Rs. " + new Money(Math.Abs(diff)) + " MORE than expected."
                    : "There is Rs. " + new Money(Math.Abs(diff)) + " LESS than expected.";

            // Closing is final, so say plainly what it means before doing it.
            if (!Theme.Confirm(
                    "Expected: Rs. " + new Money(_expectedCash) + "\r\n" +
                    "Counted:  Rs. " + new Money(counted) + "\r\n\r\n" +
                    diffLine + "\r\n\r\n" +
                    "Once the shift is closed it cannot be changed. Close it now?",
                    "Close the shift"))
                return;

            try
            {
                _ctx.Shifts.Close(sh.Id, list, _ctx.CurrentUser.Id);
                var nc = _ctx.Shifts.NonCashTotals(sh.Id, 1);
                var closed = _ctx.Shifts.FindById(sh.Id);
                // Print Z report
                var q = _ctx.Settings.Get("printer_name", "");
                if (!string.IsNullOrEmpty(q))
                {
                    var lines = new List<string>();
                    lines.Add(ReceiptFormatter.Center(_ctx.Settings.Get("store_name", "GROCERY STORE")));
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
                    lines.Add(ReceiptFormatter.PadPair("Credit", "Rs. " + new Money(nc["khata"])));
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
            Theme.Retrofit(this);
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
            Theme.Retrofit(this);
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
            Theme.Retrofit(this);
        }
    }
}
