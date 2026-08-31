using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GroceryPos.Domain;

namespace GroceryPos.Printing
{
    /// <summary>
    /// Builds a 48-column plain-text receipt. Every line is guaranteed &lt;= 48 chars.
    /// The caller wraps in ESC/POS bytes and CP437 encoding.
    /// </summary>
    public class ReceiptFormatter
    {
        public const int Width = 48;

        public class StoreInfo
        {
            public string Name;
            public string Address1;
            public string Address2;
            public string Phone;
            public string Gstin;
            public string Footer;
            public string TitleNoGst;
            public int CounterId;
        }

        public class LoyaltyBlock
        {
            public string CustomerName;
            public string CustomerPhone;
            public long PointsEarnedThisBill;
            public long PointsBalance;
        }

        public IList<string> Format(StoreInfo store, Bill bill, string cashierName,
                                    Dictionary<long, string> itemNamesById,
                                    bool duplicate = false,
                                    Money? previousBalance = null,
                                    Money? newBalance = null,
                                    LoyaltyBlock loyalty = null)
        {
            var lines = new List<string>();
            lines.Add(Center(SafeAscii(store.Name)));
            if (!string.IsNullOrWhiteSpace(store.Address1)) lines.Add(Center(SafeAscii(store.Address1)));
            if (!string.IsNullOrWhiteSpace(store.Address2)) lines.Add(Center(SafeAscii(store.Address2)));
            if (!string.IsNullOrWhiteSpace(store.Phone)) lines.Add(Center("Ph: " + SafeAscii(store.Phone)));
            bool hasGstin = !string.IsNullOrWhiteSpace(store.Gstin);
            if (hasGstin) lines.Add(Center("GSTIN: " + SafeAscii(store.Gstin)));
            string title = hasGstin
                ? "TAX INVOICE"
                : (!string.IsNullOrWhiteSpace(store.TitleNoGst) ? store.TitleNoGst : "CASH BILL");
            lines.Add(Center("- - - - - " + title + " - - - - -"));
            if (duplicate) lines.Add(Center("*** DUPLICATE COPY ***"));

            string billNo = "Bill: INV-" + bill.BillNo;
            string when = bill.BilledAt.ToString("dd/MM/yy HH:mm", CultureInfo.InvariantCulture);
            lines.Add(PadPair(billNo, when));
            lines.Add(PadPair("Cashier: " + SafeAscii(cashierName), "Ctr " + store.CounterId));
            lines.Add(new string('-', Width));

            // Header row: Item(22) Qty(9) Rate(7) Amount(10) => 22+9+7+10=48
            lines.Add(PadRight("Item", 22) + PadLeft("Qty", 9) + PadLeft("Rate", 7) + PadLeft("Amount", 10));
            lines.Add(new string('-', Width));

            foreach (var l in bill.Lines)
            {
                string name = SafeAscii(itemNamesById.ContainsKey(l.ItemId) ? itemNamesById[l.ItemId] : "Item " + l.ItemId);
                // Item name on its own line if it needs the room, else inline.
                if (name.Length > 22)
                {
                    lines.Add(TrimTo(name, Width));
                    string qty = FormatQty(l);
                    string rate = new Money(l.RatePaise).ToString();
                    string amt = new Money(l.AmountPaise).ToString();
                    lines.Add(PadRight("", 22) + PadLeft(qty, 9) + PadLeft(rate, 7) + PadLeft(amt, 10));
                }
                else
                {
                    string qty = FormatQty(l);
                    string rate = new Money(l.RatePaise).ToString();
                    string amt = new Money(l.AmountPaise).ToString();
                    lines.Add(PadRight(name, 22) + PadLeft(qty, 9) + PadLeft(rate, 7) + PadLeft(amt, 10));
                }
                if (!string.IsNullOrWhiteSpace(l.HsnCode))
                    lines.Add("  HSN " + SafeAscii(l.HsnCode));
            }

            lines.Add(new string('-', Width));
            var subtotal = new Money(bill.SubtotalPaise);
            var discount = new Money(bill.DiscountPaise);
            var taxable = new Money(bill.TaxablePaise);
            var cgst = new Money(bill.CgstPaise);
            var sgst = new Money(bill.SgstPaise);
            var roundOff = new Money(bill.RoundOffPaise);
            var net = new Money(bill.NetPaise);

            if (discount.Paise != 0)
                lines.Add(PadPair("Subtotal", subtotal.ToString()));
            if (discount.Paise != 0)
                lines.Add(PadPair("Discount", "-" + discount.ToString()));
            lines.Add(PadPair("Taxable value", taxable.ToString()));
            if (cgst.Paise != 0 || sgst.Paise != 0)
            {
                lines.Add(PadPair("CGST", cgst.ToString()));
                lines.Add(PadPair("SGST", sgst.ToString()));
            }
            if (roundOff.Paise != 0)
                lines.Add(PadPair("Round off", (roundOff.Paise >= 0 ? "+" : "") + roundOff.ToString()));

            lines.Add(new string('-', Width));
            lines.Add(PadPair("NET PAYABLE", "Rs. " + net.ToString()));
            lines.Add(new string('-', Width));

            if (bill.Payments != null)
            {
                foreach (var p in bill.Payments)
                {
                    string mode = p.Mode.ToString().ToUpperInvariant();
                    string label = mode + (string.IsNullOrEmpty(p.Reference) ? "" : " " + SafeAscii(p.Reference));
                    lines.Add(PadPair(label, new Money(p.AmountPaise).ToString()));
                }
            }

            if (previousBalance.HasValue && newBalance.HasValue)
            {
                lines.Add(new string('-', Width));
                lines.Add(PadPair("Previous balance", previousBalance.Value.ToString()));
                lines.Add(PadPair("This bill (khata)", (net - previousBalance.Value + newBalance.Value).ToString()));
                lines.Add(PadPair("Total outstanding", newBalance.Value.ToString()));
            }

            if (loyalty != null)
            {
                lines.Add(new string('-', Width));
                if (!string.IsNullOrWhiteSpace(loyalty.CustomerName))
                    lines.Add(PadPair("Customer: " + SafeAscii(loyalty.CustomerName),
                                      "Ph: " + SafeAscii(loyalty.CustomerPhone ?? "")));
                else
                    lines.Add("Customer: " + SafeAscii(loyalty.CustomerPhone ?? ""));
                lines.Add(PadPair("Points earned this bill", loyalty.PointsEarnedThisBill.ToString()));
                lines.Add(PadPair("Points balance", loyalty.PointsBalance.ToString()));
            }

            string footer = !string.IsNullOrWhiteSpace(store.Footer) ? SafeAscii(store.Footer) : "Thank you, visit again";
            lines.Add(Center(footer));

            // Enforce invariant: every line <=48
            for (int i = 0; i < lines.Count; i++)
                if (lines[i].Length > Width)
                    lines[i] = lines[i].Substring(0, Width);

            return lines;
        }

        private static string FormatQty(BillLine l)
        {
            if (l.QtyGrams > 0) return new Grams(l.QtyGrams).ToString();
            return l.QtyUnits.ToString(CultureInfo.InvariantCulture);
        }

        public static string SafeAscii(string s)
        {
            if (s == null) return "";
            var sb = new StringBuilder(s.Length);
            foreach (var ch in s)
            {
                if (ch == '₹') { sb.Append("Rs."); continue; } // rupee
                if (ch == '‘' || ch == '’') { sb.Append('\''); continue; }
                if (ch == '“' || ch == '”') { sb.Append('"'); continue; }
                if (ch == '–' || ch == '—') { sb.Append('-'); continue; }
                if (ch < 32 || ch > 126) { sb.Append('?'); continue; }
                sb.Append(ch);
            }
            return sb.ToString();
        }

        public static string PadRight(string s, int w)
        {
            s = s ?? "";
            if (s.Length > w) return s.Substring(0, w);
            return s + new string(' ', w - s.Length);
        }

        public static string PadLeft(string s, int w)
        {
            s = s ?? "";
            if (s.Length > w) return s.Substring(s.Length - w, w);
            return new string(' ', w - s.Length) + s;
        }

        public static string Center(string s)
        {
            s = s ?? "";
            if (s.Length >= Width) return s.Substring(0, Width);
            int left = (Width - s.Length) / 2;
            return new string(' ', left) + s;
        }

        public static string PadPair(string left, string right)
        {
            left = left ?? ""; right = right ?? "";
            if (left.Length + right.Length + 1 > Width)
                left = left.Substring(0, Math.Max(0, Width - right.Length - 1));
            int gap = Width - left.Length - right.Length;
            if (gap < 1) gap = 1;
            return left + new string(' ', gap) + right;
        }

        public static string TrimTo(string s, int w)
        {
            s = s ?? "";
            return s.Length > w ? s.Substring(0, w) : s;
        }
    }
}
