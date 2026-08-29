using System;
using System.Collections.Generic;
using System.Globalization;
using GroceryPos.Domain;

namespace GroceryPos.Printing
{
    /// <summary>Renders a customer statement — thermal 48-col plain text.
    /// A4 formatting is delegated to a WinForms PrintDocument in the App layer.</summary>
    public class StatementFormatter
    {
        public const int Width = 48;

        public class Header
        {
            public string StoreName;
            public string StoreAddress;
            public string CustomerName;
            public string CustomerPhone;
            public DateTime From;
            public DateTime To;
            public string PrintedBy;
        }

        public IList<string> Format(Header h, long openingPaise, IList<LedgerEntry> rows, long closingPaise)
        {
            var lines = new List<string>();
            lines.Add(ReceiptFormatter.Center(ReceiptFormatter.SafeAscii(h.StoreName)));
            if (!string.IsNullOrWhiteSpace(h.StoreAddress)) lines.Add(ReceiptFormatter.Center(ReceiptFormatter.SafeAscii(h.StoreAddress)));
            lines.Add(ReceiptFormatter.Center("CUSTOMER STATEMENT"));
            lines.Add(new string('-', Width));
            lines.Add(ReceiptFormatter.PadPair("Customer", ReceiptFormatter.SafeAscii(h.CustomerName ?? "")));
            lines.Add(ReceiptFormatter.PadPair("Phone", h.CustomerPhone ?? ""));
            lines.Add(ReceiptFormatter.PadPair("Period", h.From.ToString("dd/MM/yy") + " to " + h.To.ToString("dd/MM/yy")));
            lines.Add(new string('-', Width));
            lines.Add(ReceiptFormatter.PadPair("Opening balance", new Money(openingPaise).ToString()));
            lines.Add(new string('-', Width));

            foreach (var r in rows)
            {
                string dt = r.At.ToString("dd/MM/yy", CultureInfo.InvariantCulture);
                string type = r.Type.ToString();
                string sign = r.DebitPaise > 0 ? "+" + new Money(r.DebitPaise) : "-" + new Money(r.CreditPaise);
                string desc = ReceiptFormatter.SafeAscii(r.Description ?? "");
                lines.Add(ReceiptFormatter.PadPair(dt + " " + type, sign));
                if (!string.IsNullOrEmpty(desc))
                    lines.Add("  " + ReceiptFormatter.TrimTo(desc, Width - 2));
                lines.Add(ReceiptFormatter.PadPair("  balance", new Money(r.BalancePaise).ToString()));
            }
            lines.Add(new string('-', Width));
            lines.Add(ReceiptFormatter.PadPair("CLOSING BALANCE", "Rs. " + new Money(closingPaise)));
            lines.Add(new string('-', Width));
            lines.Add(ReceiptFormatter.PadPair("Printed", DateTime.Now.ToString("dd/MM/yy HH:mm")));
            if (!string.IsNullOrEmpty(h.PrintedBy))
                lines.Add(ReceiptFormatter.PadPair("By", ReceiptFormatter.SafeAscii(h.PrintedBy)));

            for (int i = 0; i < lines.Count; i++)
                if (lines[i].Length > Width) lines[i] = lines[i].Substring(0, Width);
            return lines;
        }
    }
}
