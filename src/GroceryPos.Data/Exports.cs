using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Dapper;

namespace GroceryPos.Data
{
    /// <summary>
    /// CSV exports for GSTR-1, GSTR-3B and Tally. Formats are simplified/assumed;
    /// header comments call this out. Confirm with the client's accountant before go-live.
    /// </summary>
    public class ExportService
    {
        private readonly Db _db;
        public ExportService(Db db) { _db = db; }

        public void Gstr1(DateTime from, DateTime to, string path)
        {
            using (var w = new StreamWriter(path, false, Encoding.UTF8))
            using (var c = _db.Open())
            {
                w.WriteLine("# GSTR-1 (simplified B2C summary by tax rate). Confirm layout with accountant before filing.");
                w.WriteLine("Rate%,Taxable,CGST,SGST");
                var rows = c.Query<dynamic>(@"
                    SELECT bl.tax_rate_bp AS Rate, SUM(bl.amount_paise - bl.tax_paise) AS Taxable,
                        SUM(bl.tax_paise/2) AS Cgst, SUM(bl.tax_paise/2) AS Sgst
                    FROM bill_lines bl JOIN bills b ON b.id=bl.bill_id
                    WHERE b.status='completed' AND b.billed_at BETWEEN @f AND @t
                    GROUP BY bl.tax_rate_bp ORDER BY bl.tax_rate_bp",
                    new { f = from.ToString("yyyy-MM-dd"), t = to.ToString("yyyy-MM-dd 23:59:59") });
                foreach (var r in rows)
                    w.WriteLine(((long)r.Rate / 100.0).ToString("0.00") + "," +
                        Rupees((long)r.Taxable) + "," + Rupees((long)r.Cgst) + "," + Rupees((long)r.Sgst));
            }
        }

        public void Gstr3b(DateTime from, DateTime to, string path)
        {
            using (var w = new StreamWriter(path, false, Encoding.UTF8))
            using (var c = _db.Open())
            {
                w.WriteLine("# GSTR-3B (simplified summary). Confirm layout with accountant before filing.");
                w.WriteLine("Metric,Amount");
                var row = c.QueryFirstOrDefault<dynamic>(@"
                    SELECT COALESCE(SUM(taxable_paise),0) AS Taxable,
                        COALESCE(SUM(cgst_paise),0) AS Cgst,
                        COALESCE(SUM(sgst_paise),0) AS Sgst
                    FROM bills WHERE status='completed' AND billed_at BETWEEN @f AND @t",
                    new { f = from.ToString("yyyy-MM-dd"), t = to.ToString("yyyy-MM-dd 23:59:59") });
                w.WriteLine("Outward taxable," + Rupees((long)row.Taxable));
                w.WriteLine("CGST," + Rupees((long)row.Cgst));
                w.WriteLine("SGST," + Rupees((long)row.Sgst));
            }
        }

        public void Tally(DateTime from, DateTime to, string path)
        {
            using (var w = new StreamWriter(path, false, Encoding.UTF8))
            using (var c = _db.Open())
            {
                w.WriteLine("# Tally daybook export (simplified). Confirm import format with the accountant.");
                w.WriteLine("Date,VoucherType,VoucherNumber,PartyLedger,SalesLedger,Amount");
                var bills = c.Query<dynamic>(@"SELECT b.bill_no AS BillNo, b.billed_at AS At, b.net_paise AS Net,
                    COALESCE(cu.name, 'Cash Sales') AS Party
                    FROM bills b LEFT JOIN customers cu ON cu.id=b.customer_id
                    WHERE b.status='completed' AND b.billed_at BETWEEN @f AND @t
                    ORDER BY b.billed_at",
                    new { f = from.ToString("yyyy-MM-dd"), t = to.ToString("yyyy-MM-dd 23:59:59") });
                foreach (var b in bills)
                {
                    var date = DateTime.Parse((string)b.At).ToString("yyyy-MM-dd");
                    w.WriteLine(date + ",Sales,INV-" + (long)b.BillNo + "," + Csv((string)b.Party) + ",Sales," + Rupees((long)b.Net));
                }
            }
        }

        private static string Rupees(long paise) { return (paise / 100L).ToString() + "." + Math.Abs(paise % 100L).ToString("D2"); }
        private static string Csv(string s) { if (s == null) return ""; if (s.IndexOfAny(new[] { ',', '"' }) < 0) return s; return "\"" + s.Replace("\"", "\"\"") + "\""; }
    }
}
