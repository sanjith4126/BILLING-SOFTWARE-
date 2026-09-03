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
            Theme.ApplyForm(this);
            Text = "Dashboard";
            Width = 1180; Height = 720;
            MinimumSize = new Size(900, 600);
            StartPosition = FormStartPosition.CenterScreen;

            var header = Theme.Header("Dashboard", DateTime.Today.ToString("dddd, dd MMMM yyyy"));

            _cardsPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 108,
                BackColor = Theme.Background,
                Padding = new Padding(Theme.Md, Theme.Sm, Theme.Md, Theme.Sm)
            };

            var chartHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Background,
                Padding = new Padding(Theme.Md, 0, Theme.Md, Theme.Md)
            };
            var chartCard = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Surface,
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(Theme.Md)
            };
            _chartPanel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface };
            _chartPanel.Paint += (s, e) => DrawChart(e.Graphics);
            _chartPanel.Resize += (s, e) => _chartPanel.Invalidate();
            chartCard.Controls.Add(_chartPanel);
            chartCard.Controls.Add(new Label
            {
                Text = "Sales over the last 7 days",
                Dock = DockStyle.Top,
                Height = 26,
                Font = Theme.BodyBold,
                TextAlign = ContentAlignment.MiddleLeft
            });
            chartHost.Controls.Add(chartCard);

            var footer = new Panel { Dock = DockStyle.Bottom, Height = 56, BackColor = Theme.Surface, Padding = new Padding(Theme.Md, Theme.Sm, Theme.Md, Theme.Sm) };
            var close = Theme.SecondaryButton("Close");
            close.Width = 120; close.Height = 40; close.Dock = DockStyle.Right;
            close.Click += (s, e) => Close();
            footer.Controls.Add(close);

            // Fill goes in first, or it covers the cards and the header.
            Controls.Add(chartHost);
            Controls.Add(footer);
            Controls.Add(_cardsPanel);
            Controls.Add(header);
            CancelButton = close;

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
                        - COALESCE((SELECT cost_paise FROM batches ba WHERE ba.id=bl.batch_id), 0)
                          * (bl.qty_units + bl.qty_grams/1000)),0)
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
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Theme.Surface);

            int w = _chartPanel.ClientSize.Width;
            int h = _chartPanel.ClientSize.Height;
            if (w < 80 || h < 80) return;

            using (var axisFont = Theme.Body)
            using (var valueFont = Theme.Data)
            using (var axisPen = new Pen(Theme.Outline))
            using (var gridPen = new Pen(Color.FromArgb(238, 240, 243)))
            using (var barBrush = new SolidBrush(Color.FromArgb(60, 110, 175)))
            using (var todayBrush = new SolidBrush(Theme.Primary))
            using (var textBrush = new SolidBrush(Theme.Muted))
            using (var boldBrush = new SolidBrush(Theme.OnSurface))
            {
                long max = 0;
                foreach (var v in _sales7) if (v > max) max = v;

                if (max == 0)
                {
                    string msg = "No sales in the last 7 days.";
                    var size = g.MeasureString(msg, axisFont);
                    g.DrawString(msg, axisFont, textBrush,
                                 (w - size.Width) / 2, (h - size.Height) / 2);
                    return;
                }

                // A quarter of headroom, so the tallest bar never touches the top
                // and its figure always has room to print above it. Without this
                // the busiest day filled the whole panel edge to edge.
                long scaleMax = (long)(max * 1.25);

                int left = 70, right = w - 16, top = 16, bottom = h - 34;
                if (right <= left || bottom <= top) return;

                // Four faint gridlines, with the rupee value down the left.
                for (int i = 0; i <= 4; i++)
                {
                    int y = bottom - (bottom - top) * i / 4;
                    g.DrawLine(gridPen, left, y, right, y);
                    long v = scaleMax * i / 4;
                    string label = new Money(v).ToString();
                    var sz = g.MeasureString(label, valueFont);
                    g.DrawString(label, valueFont, textBrush, left - sz.Width - 6, y - sz.Height / 2);
                }
                g.DrawLine(axisPen, left, top, left, bottom);
                g.DrawLine(axisPen, left, bottom, right, bottom);

                int slot = (right - left) / 7;
                int barW = Math.Max(8, Math.Min(64, slot - 18));

                for (int i = 0; i < 7; i++)
                {
                    float barH = (float)((_sales7[i] * (double)(bottom - top)) / scaleMax);
                    float x = left + i * slot + (slot - barW) / 2f;
                    var rect = new RectangleF(x, bottom - barH, barW, barH);

                    bool isToday = _days7[i].Date == DateTime.Today;
                    if (barH >= 1f)
                        g.FillRectangle(isToday ? todayBrush : barBrush, rect);

                    // Day name under the axis; today in darker text.
                    string day = _days7[i].ToString("ddd dd");
                    var daySize = g.MeasureString(day, axisFont);
                    g.DrawString(day, axisFont, isToday ? boldBrush : textBrush,
                                 x + (barW - daySize.Width) / 2f, bottom + 6);

                    // Amount above the bar, only where there was a sale.
                    if (_sales7[i] > 0)
                    {
                        string amt = new Money(_sales7[i]).ToString();
                        var amtSize = g.MeasureString(amt, valueFont);
                        g.DrawString(amt, valueFont, boldBrush,
                                     x + (barW - amtSize.Width) / 2f, rect.Top - amtSize.Height - 2);
                    }
                }
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
            Theme.Retrofit(this);
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
            Theme.ApplyForm(this);
            Text = "Reports and GST";
            Width = 860; Height = 640;
            MinimumSize = new Size(720, 520);
            StartPosition = FormStartPosition.CenterScreen;

            var header = Theme.Header("Reports and GST",
                "Pick a report. Each one opens with a date range you can change.");

            var body = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Theme.Surface,
                Padding = new Padding(Theme.Lg, Theme.Md, Theme.Lg, Theme.Lg)
            };

            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Width = 780
            };

            Group(flow, "How the shop is doing");
            Row(flow,
                Btn("Dashboard", "Today at a glance", () => new DashboardForm(_ctx).ShowDialog()),
                Btn("Sales register", "Every bill for a period",
                    () => new ReportForm(_ctx, "Sales register", Reports.SalesRegister).ShowDialog()));
            Row(flow,
                Btn("Margin report", "What you actually made",
                    () => new ReportForm(_ctx, "Margin report", Reports.Margin).ShowDialog()),
                Btn("Cashier performance", "Who billed what",
                    () => new ReportForm(_ctx, "Cashier performance", Reports.CashierPerformance).ShowDialog()));

            Group(flow, "Stock");
            Row(flow,
                Btn("Item movement", "What sold, what did not",
                    () => new ReportForm(_ctx, "Item movement", Reports.ItemMovement).ShowDialog()),
                Btn("Stock valuation", "What your stock is worth",
                    () => new ReportForm(_ctx, "Stock valuation", Reports.StockValuation).ShowDialog()));
            Row(flow,
                Btn("Dead stock", "Nothing sold in 90 days",
                    () => new ReportForm(_ctx, "Dead stock 90d", Reports.DeadStock).ShowDialog()),
                null);

            Group(flow, "Credit (kadan)");
            Row(flow,
                Btn("Collections", "Money collected, by day and by staff",
                    () => new ReportForm(_ctx, "Collections", Reports.Collections).ShowDialog()),
                null);

            Group(flow, "For your accountant");
            Row(flow,
                Btn("Tax by HSN", "For the GST return",
                    () => new ReportForm(_ctx, "Tax by HSN", Reports.TaxByHsn).ShowDialog()),
                Btn("Tally daybook", "Save a file for Tally",
                    () => Export(x => new ExportService(_ctx.Db).Tally(DateTime.Today.AddMonths(-1), DateTime.Today, x), "tally.csv")));
            Row(flow,
                Btn("GSTR-1 file", "Save a file for GST filing",
                    () => Export(x => new ExportService(_ctx.Db).Gstr1(DateTime.Today.AddMonths(-1), DateTime.Today, x), "gstr1.csv")),
                Btn("GSTR-3B file", "Save a file for GST filing",
                    () => Export(x => new ExportService(_ctx.Db).Gstr3b(DateTime.Today.AddMonths(-1), DateTime.Today, x), "gstr3b.csv")));

            body.Controls.Add(flow);

            var footer = new Panel { Dock = DockStyle.Bottom, Height = 56, BackColor = Theme.Surface, Padding = new Padding(Theme.Md, Theme.Sm, Theme.Md, Theme.Sm) };
            var close = Theme.SecondaryButton("Close");
            close.Width = 120; close.Height = 40; close.Dock = DockStyle.Right;
            close.Click += (s, e) => Close();
            footer.Controls.Add(close);

            Controls.Add(body);
            Controls.Add(footer);
            Controls.Add(header);
            CancelButton = close;
        }

        /// <summary>A heading with a rule under it, to break the list into groups.</summary>
        private static void Group(FlowLayoutPanel host, string title)
        {
            host.Controls.Add(new Label
            {
                Text = title,
                Font = Theme.Headline,
                AutoSize = false,
                Width = 760,
                Height = 30,
                TextAlign = ContentAlignment.BottomLeft,
                Margin = new Padding(0, Theme.Md, 0, Theme.Xs)
            });
            host.Controls.Add(new Panel
            {
                Width = 760, Height = 1, BackColor = Theme.Outline,
                Margin = new Padding(0, 0, 0, Theme.Sm)
            });
        }

        /// <summary>Two report cards side by side.</summary>
        private static void Row(FlowLayoutPanel host, Control a, Control b)
        {
            var row = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0, 0, 0, Theme.Sm)
            };
            if (a != null) row.Controls.Add(a);
            if (b != null) row.Controls.Add(b);
            host.Controls.Add(row);
        }

        /// <summary>A report button that says what the report is for.</summary>
        private Panel Btn(string title, string subtitle, Action onClick)
        {
            var card = new Panel
            {
                Width = 370,
                Height = 62,
                BackColor = Theme.Surface,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 0, Theme.Sm, 0),
                Cursor = Cursors.Hand
            };
            var t = new Label
            {
                Text = title, Font = Theme.BodyBold, ForeColor = Theme.Primary,
                AutoSize = false, Left = Theme.Md, Top = 8, Width = 330, Height = 22,
                TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.Transparent
            };
            var sub = new Label
            {
                Text = subtitle, Font = Theme.Body, ForeColor = Theme.Muted,
                AutoSize = false, Left = Theme.Md, Top = 30, Width = 330, Height = 22,
                TextAlign = ContentAlignment.TopLeft, BackColor = Color.Transparent
            };
            card.Controls.Add(t);
            card.Controls.Add(sub);

            EventHandler click = (s, e) =>
            {
                try { onClick(); }
                catch (Exception ex)
                {
                    Theme.Error("This report could not be opened." +
                                Environment.NewLine + Environment.NewLine +
                                "Details: " + ex.Message);
                }
            };
            foreach (Control c in new Control[] { card, t, sub })
            {
                c.Click += click;
                c.MouseEnter += (s, e) => card.BackColor = Color.FromArgb(240, 244, 250);
                c.MouseLeave += (s, e) => card.BackColor = Theme.Surface;
            }
            return card;
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
                    .Select(r => (object)new { BillNo = "INV-" + L(r.BillNo), At = S(r.At), Net = new Money(L(r.Net)).ToString(), Customer = S(r.Customer), Status = S(r.Status) }).ToList();
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
                    .Select(r => (object)new { Item = S(r.Item), Units = L(r.Units), Grams = L(r.Grams), Amount = new Money(L(r.Amount)).ToString() }).ToList();
            }
        }

        /// <summary>
        /// SQL aggregates return NULL when they match no rows, and casting that
        /// straight to long throws "Cannot convert null to long". Reports must
        /// never crash on an empty shop, so every numeric read goes through here.
        /// </summary>
        private static long L(object v)
        {
            if (v == null || v is DBNull) return 0L;
            try { return Convert.ToInt64(v); } catch { return 0L; }
        }

        /// <summary>Same idea for text: a missing name reads blank, not a crash.</summary>
        private static string S(object v)
        {
            return (v == null || v is DBNull) ? "" : Convert.ToString(v);
        }

        internal static List<object> Margin(AppContext ctx, DateTime from, DateTime to)
        {
            using (var c = ctx.Db.Open())
            {
                return c.Query<dynamic>(@"SELECT i.name AS Item,
                    COALESCE(SUM(bl.amount_paise - bl.tax_paise), 0) AS Revenue,
                    COALESCE(SUM(COALESCE((SELECT cost_paise FROM batches ba WHERE ba.id=bl.batch_id), 0)
                                 * (bl.qty_units + bl.qty_grams/1000)), 0) AS Cost
                    FROM bill_lines bl JOIN bills b ON b.id=bl.bill_id JOIN items i ON i.id=bl.item_id
                    WHERE b.status='completed' AND b.billed_at BETWEEN @f AND @t
                    GROUP BY i.id ORDER BY Revenue DESC",
                    new { f = from.ToString("yyyy-MM-dd"), t = to.ToString("yyyy-MM-dd 23:59:59") })
                    .Select(r => {
                        long rev = L(r.Revenue), cost = L(r.Cost);
                        return (object)new { Item = S(r.Item), Revenue = new Money(rev).ToString(), Cost = new Money(cost).ToString(), Margin = new Money(rev - cost).ToString() };
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
                    .Select(r => (object)new { Item = S(r.Item), Batch = S(r.Batch), Units = L(r.Units), Grams = L(r.Grams), Value = new Money(L(r.Value)).ToString() }).ToList();
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
                    .Select(r => (object)new { Item = S(r.Item), Sku = S(r.Sku), OnHand = L(r.OnHand), LastSold = (string)(r.LastSold ?? "never") }).ToList();
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
                    .Select(r => (object)new { HSN = S(r.Hsn), RatePct = (L(r.Rate) / 100.0).ToString("0.00"), Taxable = new Money(L(r.Taxable)).ToString(), Tax = new Money(L(r.Tax)).ToString() }).ToList();
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
                    .Select(r => (object)new { Cashier = S(r.Cashier), Bills = L(r.Bills), Sales = new Money(L(r.Sales)).ToString() }).ToList();
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
                    .Select(r => (object)new { Day = S(r.Day), Mode = S(r.Mode), By = S(r.ReceivedBy), Amount = new Money(L(r.Amount)).ToString() }).ToList();
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
                    .Select(r => (object)new { At = S(r.At), Customer = (string)(r.Customer ?? ""), Type = S(r.Type), Attempted = new Money((long)(r.Attempted ?? 0L)).ToString(), Balance = new Money((long)(r.Balance ?? 0L)).ToString(), Reason = (string)(r.Reason ?? "") }).ToList();
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
                    .Select(r => (object)new { At = S(r.At), Customer = S(r.Customer), Amount = new Money(L(r.Amount)).ToString(), Reason = S(r.Reason), By = S(r.By) }).ToList();
            }
        }
    }
}
