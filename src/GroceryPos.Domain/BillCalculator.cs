using System.Collections.Generic;

namespace GroceryPos.Domain
{
    /// <summary>
    /// Pure computation over bill lines. Tax computed per line then summed. CGST=SGST=tax/2.
    /// Round-off adjusts net to nearest rupee.
    /// </summary>
    public static class BillCalculator
    {
        /// <summary>Compute line amount and tax. Rate is per-unit or per-kg.
        /// For weight lines, amount = rate * grams / 1000 (integer maths).</summary>
        public static void ComputeLine(BillLine l)
        {
            long baseAmt;
            if (l.QtyGrams > 0)
            {
                // rate is per kg (paise per kg). Amount = rate * grams / 1000.
                long numer = l.RatePaise * (long)l.QtyGrams;
                long q = numer / 1000L;
                long r = numer - q * 1000L;
                if (r >= 500L) q += 1;
                baseAmt = q;
            }
            else
            {
                baseAmt = l.RatePaise * (long)l.QtyUnits;
            }
            long afterDisc = baseAmt - l.DiscountPaise;
            if (afterDisc < 0) afterDisc = 0;
            l.TaxPaise = new Money(afterDisc).ApplyRateBp(l.TaxRateBp).Paise;
            // amount stored is line total INCLUDING tax? For consumer receipt total; tax already
            // added into amount. We keep amount = after-discount + tax so bill net matches.
            l.AmountPaise = afterDisc + l.TaxPaise;
        }

        public static void ComputeBill(Bill b)
        {
            long subtotal = 0, discount = 0, taxable = 0, tax = 0;
            foreach (var l in b.Lines)
            {
                ComputeLine(l);
                long baseAmt = l.QtyGrams > 0
                    ? ((l.RatePaise * (long)l.QtyGrams + 500L) / 1000L)
                    : l.RatePaise * (long)l.QtyUnits;
                subtotal += baseAmt;
                discount += l.DiscountPaise;
                taxable += baseAmt - l.DiscountPaise;
                tax += l.TaxPaise;
            }
            b.SubtotalPaise = subtotal;
            b.DiscountPaise = discount;
            b.TaxablePaise = taxable;
            b.CgstPaise = tax / 2;
            b.SgstPaise = tax - b.CgstPaise;
            long net = taxable + tax;
            Money delta;
            var rounded = new Money(net).RoundToRupee(out delta);
            b.RoundOffPaise = delta.Paise;
            b.NetPaise = rounded.Paise;
            b.Lines = new List<BillLine>(b.Lines);
        }
    }
}
