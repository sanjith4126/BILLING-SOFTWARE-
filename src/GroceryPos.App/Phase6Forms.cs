using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Dapper;
using GroceryPos.Data;
using GroceryPos.Domain;

namespace GroceryPos.App
{
    // -----------------------------------------------------------------------------------
    // Dashboard — simple cards + owner-drawn bar chart of last-7-day sales
    // -----------------------------------------------------------------------------------
    public class DashboardForm : Form
    {
        private readonly AppContext _ctx;
        private Panel _cardsPanel;
        private Panel _chartPanel;
        private long[] _sales7 = new long[7];
        private DateTime[] _days7 = new DateTime[7];

        public DashboardForm(AppContext ctx)
        {
            _ctx = ctx;
            Text = "Dashboard";
            Width = 1000; Height = 640;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(245, 247, 250);
            WindowState = FormWindowState.Maximized;

            var header = new Panel { Dock = DockStyle.Top, Height = 60, BackColor = Color.FromArgb(30, 60, 100) };
            header.Controls.Add(new Label
            {
                Text = "Dashboard  ·  " + DateTime.Today.ToString("dd MMM yyyy"),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                Left = 20, Top = 14, Width = 500, Height = 32
            });
            Controls.Add(header);

            _cardsPanel = new Panel { Dock = DockStyle.Top, Height = 130, BackColor = Color.FromArgb(245, 247, 250) };
            Controls.Add(_cardsPanel);

            _chartPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(10) };
            _chartPanel.Paint += (s, e) => DrawChart(e.Graphics);
            Controls.Add(_chartPanel);

            Load += (s, e) => Reload();
            _cardsPanel.Resize += (s, e) => LayoutCards();
        }

        private long _salesToday, _billCount, _avgBill, _grossMargin, _cashInHand;

        private void Reload()
        {
            using (var c = _ctx.Db.Open())
            {
                string todayFrom = DateTime.Today.ToString("yyyy-MM-dd 00:00:00");
                string todayTo = DateTime.Today.ToString("yyyy-MM-dd 23:59:59");
                _salesToday = c.ExecuteScalar<long>("SELECT COALESCE(SUM(net_paise),0) FROM bills WHERE status='completed' AND billed_at BETWEEN @f AND @t",
                    new { f = todayFrom, t = todayTo });
                _billCount = c.ExecuteScalar<long>("SELECT COUNT(*) FROM bills WHERE status='completed' AND billed_at BETWEEN @f AND @t",
                    new { f = todayFrom, t = todayTo });
                _avgBill = _billCount == 0 ? 0 : _salesToday / _billCount;
                _grossMargin = c.ExecuteScalar<long>(@"
                    SELECT COALESCE(SUM(bl.amount_paise - bl.tax_paise
                        - (SELECT cost_paise FROM batches ba WHERE ba.id=bl.batch_id) * (bl.qty_units + bl.qty_grams/1000)),0)
                    FROM bill_lines bl JOIN bills b ON b.id=bl.bill_id
                    WHERE b.status='completed' AND b.billed_at BETWEEN @f AND @t",
                    new { f = todayFrom, t = todayTo });
                _cashInHand = c.ExecuteScalar<long>(@"
                    SELECT COALESCE(SUM(p.amount_paise),0) FROM payments p JOIN bills b ON b.id=p.bill_id
                    WHERE p.mode='cash' AND b.status='completed' AND b.billed_at BETWEEN @f AND @t",
                    new { f = todayFrom, t = todayTo });

                for (int i = 6; i >= 0; i--)
                {
                    var d = DateTime.Today.AddDays(-i);
                    _days7[6 - i] = d;
                    _sales7[6 - i] = c.ExecuteScalar<long>(@"SELECT COALESCE(SUM(net_paise),0) FROM bills
                        WHERE status='completed' AND billed_at BETWEEN @f AND @t",
                        new { f = d.ToString("yyyy-MM-dd 00:00:00"), t = d.ToString("yyyy-MM-dd 23:59:59") });
                }
                _chartPanel.Invalidate();
                LayoutCards();
            }
        }

        private void LayoutCards()
        {
            _cardsPanel.Controls.Clear();
            var cards = new[]
            {
                Card("Sales today", "Rs. " + new Money(_salesToday), Color.FromArgb(30, 130, 76)),
                Card("Bills", _billCount.ToString(), Color.FromArgb(60, 100, 160)),
                Card("Average bill", "Rs. " + new Money(_avgBill), Color.FromArgb(220, 130, 40)),
                Card("Gross margin", "Rs. " + new Money(_grossMargin), Color.FromArgb(90, 60, 140)),
                Card("Cash in hand", "Rs. " + new Money(_cashInHand), Color.FromArgb(30, 130, 76))
            };
            int w = 200, gap = 10;
            int total = cards.Length * w + (cards.Length - 1) * gap;
            int left = Math.Max(10, (_cardsPanel.ClientSize.Width - total) / 2);
            for (int i = 0; i < cards.Length; i++)
            {
                cards[i].Left = left + i * (w + gap);
                cards[i].Top = 15;
                cards[i].Width = w;
                cards[i].Height = 100;
                _cardsPanel.Controls.Add(cards[i]);
            }
        }

        private Panel Card(string title, string value, Color color)
        {
            var p = new Panel { BackColor = color };
            var t = new Label
            {
                Text = title, ForeColor = Color.FromArgb(230, 240, 250),
                Font = new Font("Segoe UI", 10F),
                Left = 12, Top = 12, Width = 176, Height = 20,
                BackColor = Color.FromArgb(0, 0, 0, 0)
            };
            var v = new Label
            {
                Text = value, ForeColor = Color.White,
                Font = new Font("Segoe UI", 18F, FontStyle.Bold),
                Left = 12, Top = 36, Width = 176, Height = 50,
                BackColor = Color.FromArgb(0, 0, 0, 0)
            };
            p.Controls.Add(t);
            p.Controls.Add(v);
            return p;
        }

        private void DrawChart(Graphics g)
        {
            g.Clear(Color.White);
            var f = new Font("Segoe UI", 9F);
            g.DrawString("Sales — last 7 days", f, Brushes.Black, 10, 6);
            int left = 40, right = _chartPanel.Width - 20, top = 30, bottom = _chartPanel.Height - 30;
            g.DrawLine(Pens.Black, left, top, left, bottom);
            g.DrawLine(Pens.Black, left, bottom, right, bottom);
            long max = 1;
            foreach (var v in _sales7) if (v > max) max = v;
            int barW = (right - left) / 8;
            for (int i = 0; i < 7; i++)
            {
                float h = (float)((_sales7[i] * (double)(bottom - top)) / max);
                var rect = new RectangleF(left + 10 + i * barW, bottom - h, barW - 10, h);
                g.FillRectangle(Brushes.SteelBlue, rect);
                g.DrawString(_days7[i].ToString("dd/MM"), f, Brushes.Black, rect.Left, bottom + 4);
                g.DrawString(new Money(_sales7[i]).ToString(), f, Brushes.Black, rect.Left, rect.Top - 14);
            }
        }
    }

    // -----------------------------------------------------------------------------------
    // Generic report form — grid + optional date range + CSV export
    // -----------------------------------------------------------------------------------
    public class ReportForm : Form
    {
        private readonly AppContext _ctx;
        private readonly string _title;
        private readonly Func<AppContext, DateTime, DateTime, List<object>> _load;
        private DataGridView _grid;
        private DateTimePicker _from, _to;

        public ReportForm(AppContext ctx, string title, Func<AppContext, DateTime, DateTime, List<object>> load)
        {
            _ctx = ctx; _title = title; _load = load;
            Text = title;
            Width = 1000; Height = 600;
            StartPosition = FormStartPosition.CenterParent;
            var top = new Panel { Dock = DockStyle.Top, Height = 40 };
            top.Controls.Add(new Label { Text = "From", Left = 8, Top = 12, Width = 40 });
            _from = new DateTimePicker { Left = 50, Top = 8, Width = 120, Value = DateTime.Today.AddDays(-30) };
            top.Controls.Add(_from);
            top.Controls.Add(new Label { Text = "To", Left = 180, Top = 12, Width = 30 });
            _to = new DateTimePicker { Left = 210, Top = 8, Width = 120, Value = DateTime.Today };
            top.Controls.Add(_to);
            var runBtn = new Button { Text = "Run", Left = 340, Top = 6, Width = 80 };
            runBtn.Click += (s, e) => Reload();
            top.Controls.Add(runBtn);
            var csvBtn = new Button { Text = "Export CSV", Left = 430, Top = 6, Width = 100 };
            csvBtn.Click += (s, e) => ExportCsv();
            top.Controls.Add(csvBtn);
            Controls.Add(top);
            _grid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, RowHeadersVisible = false };
            Controls.Add(_grid);
            Load += (s, e) => Reload();
        }

        private void Reload()
        {
            _grid.DataSource = _load(_ctx, _from.Value.Date, _to.Value.Date);
        }

        private void ExportCsv()
        {
            using (var d = new SaveFileDialog { Filter = "CSV|*.csv", FileName = _title.Replace(' ', '_') + ".csv" })
            {
                if (d.ShowDialog() != DialogResult.OK) return;
                using (var w = new StreamWriter(d.FileName, false, Encoding.UTF8))
                {
                    var cols = _grid.Columns.Cast<DataGridViewColumn>().Where(c => c.Visible).ToList();
                    w.WriteLine(string.Join(",", cols.Select(c => Csv(c.HeaderText))));
                    foreach (DataGridViewRow r in _grid.Rows)
                    {
                        if (r.IsNewRow) continue;
                        w.WriteLine(string.Join(",", cols.Select(c => Csv(Convert.ToString(r.Cells[c.Index].Value)))));
                    }
                }
                MessageBox.Show("Saved " + d.FileName);
            }
        }

        private static string Csv(string s) { if (s == null) return ""; if (s.IndexOfAny(new[] { ',', '"', '\n' }) < 0) return s; return "\"" + s.Replace("\"", "\"\"") + "\""; }
    }

    // -----------------------------------------------------------------------------------
    // Reports menu form
    // -----------------------------------------------------------------------------------
    public class ReportsMenuForm : Form
    {
        private readonly AppContext _ctx;

        public ReportsMenuForm(AppContext ctx)
        {
            _ctx = ctx;
            Text = "Reports"; Width = 500; Height = 600;
            StartPosition = FormStartPosition.CenterParent;
            var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(12) };
            flow.Controls.Add(Btn("Dashboard", () => new DashboardForm(_ctx).ShowDialog()));
            flow.Controls.Add(Btn("Sales register", () => new ReportForm(_ctx, "Sales register", Reports.SalesRegister).ShowDialog()));
            flow.Controls.Add(Btn("Item movement", () => new ReportForm(_ctx, "Item movement", Reports.ItemMovement).ShowDialog()));
            flow.Controls.Add(Btn("Margin report", () => new ReportForm(_ctx, "Margin report", Reports.Margin).ShowDialog()));
            flow.Controls.Add(Btn("Stock valuation", () => new ReportForm(_ctx, "Stock valuation", Reports.StockValuation).ShowDialog()));
            flow.Controls.Add(Btn("Dead stock (90d)", () => new ReportForm(_ctx, "Dead stock 90d", Reports.DeadStock).ShowDialog()));
            flow.Controls.Add(Btn("Tax by HSN", () => new ReportForm(_ctx, "Tax by HSN", Reports.TaxByHsn).ShowDialog()));
            flow.Controls.Add(Btn("Cashier performance", () => new ReportForm(_ctx, "Cashier performance", Reports.CashierPerformance).ShowDialog()));
            flow.Controls.Add(Btn("Collections (khata)", () => new ReportForm(_ctx, "Collections", Reports.Collections).ShowDialog()));
            flow.Controls.Add(Btn("Limit overrides", () => new ReportForm(_ctx, "Limit overrides", Reports.LimitOverrides).ShowDialog()));
            flow.Controls.Add(Btn("Write-offs", () => new ReportForm(_ctx, "Write-offs", Reports.WriteOffs).ShowDialog()));
            flow.Controls.Add(Btn("Export GSTR-1 CSV", () => Export(x => new ExportService(_ctx.Db).Gstr1(DateTime.Today.AddMonths(-1), DateTime.Today, x), "gstr1.csv")));
            flow.Controls.Add(Btn("Export GSTR-3B CSV", () => Export(x => new ExportService(_ctx.Db).Gstr3b(DateTime.Today.AddMonths(-1), DateTime.Today, x), "gstr3b.csv")));
            flow.Controls.Add(Btn("Export Tally daybook CSV", () => Export(x => new ExportService(_ctx.Db).Tally(DateTime.Today.AddMonths(-1), DateTime.Today, x), "tally.csv")));
            Controls.Add(flow);
        }

        private Button Btn(string t, Action a)
        {
            var b = new Button { Text = t, Width = 300, Height = 34 };
            b.Click += (s, e) => a();
            return b;
        }

        private void Export(Action<string> run, string filename)
        {
            using (var d = new SaveFileDialog { Filter = "CSV|*.csv", FileName = filename })
            {
                if (d.ShowDialog() != DialogResult.OK) return;
                try { run(d.FileName); MessageBox.Show("Wrote " + d.FileName); }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
            }
        }
    }

    // -----------------------------------------------------------------------------------
    // Report queries
    // -----------------------------------------------------------------------------------
    internal static class Reports
    {
        internal static List<object> SalesRegister(AppContext ctx, DateTime from, DateTime to)
        {
            using (var c = ctx.Db.Open())
            {
                return c.Query<dynamic>(@"SELECT b.bill_no AS BillNo, b.billed_at AS At, b.net_paise AS Net,
                    COALESCE(cu.name, '-') AS Customer, b.status AS Status
                    FROM bills b LEFT JOIN customers cu ON cu.id=b.customer_id
                    WHERE b.billed_at BETWEEN @f AND @t ORDER BY b.billed_at",
                    new { f = from.ToString("yyyy-MM-dd"), t = to.ToString("yyyy-MM-dd 23:59:59") })
                    .Select(r => (object)new { BillNo = "INV-" + (long)r.BillNo, At = (string)r.At, Net = new Money((long)r.Net).ToString(), Customer = (string)r.Customer, Status = (string)r.Status }).ToList();
            }
        }

        internal static List<object> ItemMovement(AppContext ctx, DateTime from, DateTime to)
        {
            using (var c = ctx.Db.Open())
            {
                return c.Query<dynamic>(@"SELECT i.name AS Item, SUM(bl.qty_units) AS Units, SUM(bl.qty_grams) AS Grams,
                    SUM(bl.amount_paise) AS Amount
                    FROM bill_lines bl JOIN bills b ON b.id=bl.bill_id JOIN items i ON i.id=bl.item_id
                    WHERE b.status='completed' AND b.billed_at BETWEEN @f AND @t
                    GROUP BY i.id ORDER BY Amount DESC",
                    new { f = from.ToString("yyyy-MM-dd"), t = to.ToString("yyyy-MM-dd 23:59:59") })
                    .Select(r => (object)new { Item = (string)r.Item, Units = (long)r.Units, Grams = (long)r.Grams, Amount = new Money((long)r.Amount).ToString() }).ToList();
            }
        }

        internal static List<object> Margin(AppContext ctx, DateTime from, DateTime to)
        {
            using (var c = ctx.Db.Open())
            {
                return c.Query<dynamic>(@"SELECT i.name AS Item,
                    SUM(bl.amount_paise - bl.tax_paise) AS Revenue,
                    SUM((SELECT cost_paise FROM batches ba WHERE ba.id=bl.batch_id) * (bl.qty_units + bl.qty_grams/1000)) AS Cost
                    FROM bill_lines bl JOIN bills b ON b.id=bl.bill_id JOIN items i ON i.id=bl.item_id
                    WHERE b.status='completed' AND b.billed_at BETWEEN @f AND @t
                    GROUP BY i.id ORDER BY Revenue DESC",
                    new { f = from.ToString("yyyy-MM-dd"), t = to.ToString("yyyy-MM-dd 23:59:59") })
                    .Select(r => {
                        long rev = (long)r.Revenue, cost = (long)r.Cost;
                        return (object)new { Item = (string)r.Item, Revenue = new Money(rev).ToString(), Cost = new Money(cost).ToString(), Margin = new Money(rev - cost).ToString() };
                    }).ToList();
            }
        }

        internal static List<object> StockValuation(AppContext ctx, DateTime from, DateTime to)
        {
            using (var c = ctx.Db.Open())
            {
                return c.Query<dynamic>(@"SELECT i.name AS Item, b.batch_code AS Batch,
                    b.qty_units AS Units, b.qty_grams AS Grams, b.cost_paise AS Cost,
                    (b.cost_paise * b.qty_units + b.cost_paise * b.qty_grams / 1000) AS Value
                    FROM batches b JOIN items i ON i.id=b.item_id
                    WHERE b.qty_units>0 OR b.qty_grams>0 ORDER BY Value DESC")
                    .Select(r => (object)new { Item = (string)r.Item, Batch = (string)r.Batch, Units = (long)r.Units, Grams = (long)r.Grams, Value = new Money((long)r.Value).ToString() }).ToList();
            }
        }

        internal static List<object> DeadStock(AppContext ctx, DateTime from, DateTime to)
        {
            using (var c = ctx.Db.Open())
            {
                string cutoff = DateTime.Today.AddDays(-90).ToString("yyyy-MM-dd");
                return c.Query<dynamic>(@"SELECT i.name AS Item, i.sku AS Sku,
                    COALESCE((SELECT SUM(qty_units + qty_grams/1000) FROM batches b WHERE b.item_id=i.id),0) AS OnHand,
                    (SELECT MAX(b.billed_at) FROM bill_lines bl JOIN bills b ON b.id=bl.bill_id
                     WHERE bl.item_id=i.id AND b.status='completed') AS LastSold
                    FROM items i
                    WHERE i.is_active=1
                    AND ((SELECT MAX(b.billed_at) FROM bill_lines bl JOIN bills b ON b.id=bl.bill_id
                          WHERE bl.item_id=i.id AND b.status='completed') IS NULL
                        OR (SELECT MAX(b.billed_at) FROM bill_lines bl JOIN bills b ON b.id=bl.bill_id
                          WHERE bl.item_id=i.id AND b.status='completed') < @c)
                    ORDER BY i.name", new { c = cutoff })
                    .Select(r => (object)new { Item = (string)r.Item, Sku = (string)r.Sku, OnHand = (long)r.OnHand, LastSold = (string)(r.LastSold ?? "never") }).ToList();
            }
        }

        internal static List<object> TaxByHsn(AppContext ctx, DateTime from, DateTime to)
        {
            using (var c = ctx.Db.Open())
            {
                return c.Query<dynamic>(@"SELECT bl.hsn_code AS Hsn, bl.tax_rate_bp AS Rate,
                    SUM(bl.amount_paise - bl.tax_paise) AS Taxable, SUM(bl.tax_paise) AS Tax
                    FROM bill_lines bl JOIN bills b ON b.id=bl.bill_id
                    WHERE b.status='completed' AND b.billed_at BETWEEN @f AND @t
                    GROUP BY bl.hsn_code, bl.tax_rate_bp ORDER BY bl.hsn_code",
                    new { f = from.ToString("yyyy-MM-dd"), t = to.ToString("yyyy-MM-dd 23:59:59") })
                    .Select(r => (object)new { HSN = (string)r.Hsn, RatePct = ((long)r.Rate / 100.0).ToString("0.00"), Taxable = new Money((long)r.Taxable).ToString(), Tax = new Money((long)r.Tax).ToString() }).ToList();
            }
        }

        internal static List<object> CashierPerformance(AppContext ctx, DateTime from, DateTime to)
        {
            using (var c = ctx.Db.Open())
            {
                return c.Query<dynamic>(@"SELECT u.name AS Cashier, COUNT(*) AS Bills, SUM(b.net_paise) AS Sales
                    FROM bills b JOIN users u ON u.id=b.user_id
                    WHERE b.status='completed' AND b.billed_at BETWEEN @f AND @t
                    GROUP BY u.id ORDER BY Sales DESC",
                    new { f = from.ToString("yyyy-MM-dd"), t = to.ToString("yyyy-MM-dd 23:59:59") })
                    .Select(r => (object)new { Cashier = (string)r.Cashier, Bills = (long)r.Bills, Sales = new Money((long)r.Sales).ToString() }).ToList();
            }
        }

        internal static List<object> Collections(AppContext ctx, DateTime from, DateTime to)
        {
            using (var c = ctx.Db.Open())
            {
                return c.Query<dynamic>(@"SELECT date(p.received_at) AS Day, p.mode AS Mode,
                    u.name AS ReceivedBy, SUM(p.amount_paise) AS Amount
                    FROM credit_payments p JOIN users u ON u.id=p.received_by
                    WHERE p.received_at BETWEEN @f AND @t
                    GROUP BY date(p.received_at), p.mode, p.received_by ORDER BY Day DESC",
                    new { f = from.ToString("yyyy-MM-dd"), t = to.ToString("yyyy-MM-dd 23:59:59") })
                    .Select(r => (object)new { Day = (string)r.Day, Mode = (string)r.Mode, By = (string)r.ReceivedBy, Amount = new Money((long)r.Amount).ToString() }).ToList();
            }
        }

        internal static List<object> LimitOverrides(AppContext ctx, DateTime from, DateTime to)
        {
            using (var c = ctx.Db.Open())
            {
                return c.Query<dynamic>(@"SELECT e.at AS At, cu.name AS Customer, e.event_type AS Type,
                    e.attempted_paise AS Attempted, e.balance_at_time_paise AS Balance,
                    e.reason AS Reason
                    FROM credit_limit_events e LEFT JOIN customers cu ON cu.id=e.customer_id
                    WHERE e.at BETWEEN @f AND @t ORDER BY e.at DESC",
                    new { f = from.ToString("yyyy-MM-dd"), t = to.ToString("yyyy-MM-dd 23:59:59") })
                    .Select(r => (object)new { At = (string)r.At, Customer = (string)(r.Customer ?? ""), Type = (string)r.Type, Attempted = new Money((long)(r.Attempted ?? 0L)).ToString(), Balance = new Money((long)(r.Balance ?? 0L)).ToString(), Reason = (string)(r.Reason ?? "") }).ToList();
            }
        }

        internal static List<object> WriteOffs(AppContext ctx, DateTime from, DateTime to)
        {
            using (var c = ctx.Db.Open())
            {
                return c.Query<dynamic>(@"SELECT l.at AS At, cu.name AS Customer, l.credit_paise AS Amount,
                    l.description AS Reason, u.name AS By
                    FROM customer_ledger l JOIN customers cu ON cu.id=l.customer_id JOIN users u ON u.id=l.user_id
                    WHERE l.type='write_off' AND l.at BETWEEN @f AND @t ORDER BY l.at DESC",
                    new { f = from.ToString("yyyy-MM-dd"), t = to.ToString("yyyy-MM-dd 23:59:59") })
                    .Select(r => (object)new { At = (string)r.At, Customer = (string)r.Customer, Amount = new Money((long)r.Amount).ToString(), Reason = (string)r.Reason, By = (string)r.By }).ToList();
            }
        }
    }
}
