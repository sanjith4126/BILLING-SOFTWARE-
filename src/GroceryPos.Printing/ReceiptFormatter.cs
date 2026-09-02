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

        /// <summary>Solid horizontal line — CP437 0xC4 (Unicode U+2500 BOX DRAWINGS LIGHT
        /// HORIZONTAL). Encodes cleanly on the TVS RP 3230.</summary>
        public const char LineChar = '─';

        /// <summary>
        /// How money is prefixed on the printed bill.
        ///
        /// The shop asked for the rupee symbol. It cannot be used: the RP 3230
        /// runs code page PC437, which has no rupee glyph, and its own self-test
        /// slip confirms this. Encoding U+20B9 to CP437 produces byte 0x3F, so
        /// the bill would read "GRAND TOTAL : ? 770.00". "Rs." is the only thing
        /// this printer can render.
        ///
        /// If the shop later buys a printer whose code page carries the symbol,
        /// change this one constant.
        /// </summary>
        public const string CurrencyPrefix = "Rs.";

        /// <summary>Rich version returning per-line emphasis so the caller can print
        /// the NET line double-size bold.</summary>
        public IList<ReceiptLine> FormatRich(StoreInfo store, Bill bill, string cashierName,
                                             Dictionary<long, string> itemNamesById,
                                             bool duplicate = false,
                                             Money? previousBalance = null,
                                             Money? newBalance = null,
                                             LoyaltyBlock loyalty = null)
        {
            var text = Format(store, bill, cashierName, itemNamesById, duplicate, previousBalance, newBalance, loyalty);
            string footer = !string.IsNullOrWhiteSpace(store.Footer)
                ? SafeAscii(store.Footer)
                : "Thank you, visit again";

            var rich = new List<ReceiptLine>(text.Count);
            foreach (var l in text)
            {
                var line = new ReceiptLine(l);
                if (l == null) { rich.Add(line); continue; }

                if (l.StartsWith("GRAND TOTAL", StringComparison.OrdinalIgnoreCase))
                {
                    // The one number the customer looks at on a busy counter.
                    line.Bold = true;
                    line.DoubleSize = true;
                }
                else if (l.StartsWith("Bill: ", StringComparison.OrdinalIgnoreCase))
                {
                    // Bill number and date: bold, but normal size so the line
                    // still fits 48 columns.
                    line.Bold = true;
                }
                else if (l.Trim() == footer.Trim())
                {
                    line.Bold = true;
                }
                rich.Add(line);
            }
            return rich;
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

            // With a GSTIN this is a tax invoice by law. Without one, name the
            // slip after how it was actually paid for, as the shop asked.
            string title = hasGstin
                ? "TAX INVOICE"
                : TitleForPayment(bill, store.TitleNoGst);
            lines.Add(Center("- - - - - " + title + " - - - - -"));
            if (duplicate) lines.Add(Center("*** DUPLICATE COPY ***"));

            string billNo = "Bill: INV-" + bill.BillNo;
            string when = bill.BilledAt.ToString("dd/MM/yy HH:mm", CultureInfo.InvariantCulture);
            lines.Add(PadPair(billNo, when));
            lines.Add(PadPair("Cashier: " + SafeAscii(cashierName), "Ctr " + store.CounterId));
            lines.Add(new string(LineChar, Width));

            // Header row: Item(22) Qty(9) Rate(7) Amount(10) => 22+9+7+10=48
            lines.Add(PadRight("Item", 22) + PadLeft("Qty", 9) + PadLeft("Rate", 7) + PadLeft("Amount", 10));
            lines.Add(new string(LineChar, Width));

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

            lines.Add(new string(LineChar, Width));
            var discount = new Money(bill.DiscountPaise);
            var cgst = new Money(bill.CgstPaise);
            var sgst = new Money(bill.SgstPaise);
            var roundOff = new Money(bill.RoundOffPaise);
            var net = new Money(bill.NetPaise);

            // The shop asked for a count of what was bought here, in place of the
            // taxable value. "Items" is how many lines are on the bill; "Total Qty"
            // adds up the quantities, so 12 items can be 23 pieces.
            int itemCount = bill.Lines.Count;
            int totalQty = 0;
            bool anyWeighed = false;
            foreach (var l in bill.Lines)
            {
                if (l.QtyGrams > 0) { anyWeighed = true; totalQty += 1; }
                else totalQty += l.QtyUnits;
            }
            lines.Add(PadPair("Items", itemCount.ToString(CultureInfo.InvariantCulture)));
            lines.Add(PadPair("Total Qty", totalQty.ToString(CultureInfo.InvariantCulture)
                                           + (anyWeighed ? " (loose counted as 1)" : "")));

            if (discount.Paise != 0)
                lines.Add(PadPair("Discount", "-" + discount.ToString()));
            if (cgst.Paise != 0 || sgst.Paise != 0)
            {
                lines.Add(PadPair("CGST", cgst.ToString()));
                lines.Add(PadPair("SGST", sgst.ToString()));
            }

            // Grand total block: what it came to, what rounding did, what to pay.
            lines.Add(new string(LineChar, Width));
            lines.Add(PadPair("Sub Total", new Money(bill.NetPaise - bill.RoundOffPaise).ToString()));
            lines.Add(PadPair("Round off", (roundOff.Paise >= 0 ? "+" : "") + roundOff.ToString()));
            lines.Add(PadPair("GRAND TOTAL", CurrencyPrefix + " " + net.ToString()));
            lines.Add(new string(LineChar, Width));

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
                lines.Add(new string(LineChar, Width));
                lines.Add(PadPair("Previous balance", previousBalance.Value.ToString()));
                lines.Add(PadPair("This bill (khata)", (net - previousBalance.Value + newBalance.Value).ToString()));
                lines.Add(PadPair("Total outstanding", newBalance.Value.ToString()));
            }

            if (loyalty != null)
            {
                lines.Add(new string(LineChar, Width));
                if (!string.IsNullOrWhiteSpace(loyalty.CustomerName))
                    lines.Add(PadPair("Customer: " + SafeAscii(loyalty.CustomerName),
                                      "Ph: " + SafeAscii(loyalty.CustomerPhone ?? "")));
                else
                    lines.Add("Customer: " + SafeAscii(loyalty.CustomerPhone ?? ""));
                lines.Add(PadPair("Points earned this bill", loyalty.PointsEarnedThisBill.ToString()));
                lines.Add(PadPair("Points balance", loyalty.PointsBalance.ToString()));
            }

            // The thank-you gets its own ruled section, like the payment block,
            // rather than trailing off the bottom of the slip.
            string footer = !string.IsNullOrWhiteSpace(store.Footer)
                ? SafeAscii(store.Footer)
                : "Thank you, visit again";
            lines.Add(new string(LineChar, Width));
            lines.Add(Center(footer));
            lines.Add(new string(LineChar, Width));

            // Enforce invariant: every line <=48
            for (int i = 0; i < lines.Count; i++)
                if (lines[i].Length > Width)
                    lines[i] = lines[i].Substring(0, Width);

            return lines;
        }

        /// <summary>
        /// Names the slip after how it was paid: CASH BILL, CARD BILL, UPI BILL
        /// or KHATA BILL. A split payment is named after whichever mode paid the
        /// most, since one name has to be chosen.
        /// </summary>
        private static string TitleForPayment(Bill bill, string fallback)
        {
            if (bill.Payments == null || bill.Payments.Count == 0)
                return !string.IsNullOrWhiteSpace(fallback) ? fallback : "CASH BILL";

            PaymentMode biggest = PaymentMode.Cash;
            long most = -1;
            foreach (var p in bill.Payments)
            {
                if (p.AmountPaise > most) { most = p.AmountPaise; biggest = p.Mode; }
            }

            switch (biggest)
            {
                case PaymentMode.Card: return "CARD BILL";
                case PaymentMode.Upi: return "UPI BILL";
                case PaymentMode.Khata: return "KHATA BILL";
                default: return "CASH BILL";
            }
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
