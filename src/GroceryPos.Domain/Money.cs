using System;
using System.Globalization;

namespace GroceryPos.Domain
{
    /// <summary>
    /// Money stored as integer paise. Never floating point.
    /// </summary>
    public struct Money : IEquatable<Money>, IComparable<Money>
    {
        public long Paise { get; }

        public Money(long paise)
        {
            Paise = paise;
        }

        public static Money Zero => new Money(0);

        public static Money FromPaise(long paise)
        {
            return new Money(paise);
        }

        public static Money FromRupees(int rupees)
        {
            return new Money((long)rupees * 100L);
        }

        /// <summary>
        /// Parse a string of the form "123.45" (rupees.paise). Rejects blank and floating point garbage.
        /// </summary>
        public static Money ParseRupees(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
                throw new ArgumentException("Blank money value");
            s = s.Trim();
            bool negative = false;
            if (s.StartsWith("-"))
            {
                negative = true;
                s = s.Substring(1);
            }
            int dot = s.IndexOf('.');
            long rupees;
            long paise = 0;
            if (dot < 0)
            {
                if (!long.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out rupees))
                    throw new FormatException("Not a valid money value: " + s);
            }
            else
            {
                string rp = s.Substring(0, dot);
                string pp = s.Substring(dot + 1);
                if (pp.Length == 1) pp = pp + "0";
                if (pp.Length != 2) throw new FormatException("Money must have two decimal places: " + s);
                if (!long.TryParse(rp, NumberStyles.None, CultureInfo.InvariantCulture, out rupees))
                    throw new FormatException("Bad money: " + s);
                if (!long.TryParse(pp, NumberStyles.None, CultureInfo.InvariantCulture, out paise))
                    throw new FormatException("Bad money: " + s);
            }
            long total = rupees * 100L + paise;
            return new Money(negative ? -total : total);
        }

        public static Money operator +(Money a, Money b) { return new Money(a.Paise + b.Paise); }
        public static Money operator -(Money a, Money b) { return new Money(a.Paise - b.Paise); }
        public static Money operator -(Money a) { return new Money(-a.Paise); }
        public static Money operator *(Money a, int n) { return new Money(a.Paise * n); }
        public static Money operator *(int n, Money a) { return new Money(a.Paise * n); }

        public static bool operator ==(Money a, Money b) { return a.Paise == b.Paise; }
        public static bool operator !=(Money a, Money b) { return a.Paise != b.Paise; }
        public static bool operator <(Money a, Money b) { return a.Paise < b.Paise; }
        public static bool operator >(Money a, Money b) { return a.Paise > b.Paise; }
        public static bool operator <=(Money a, Money b) { return a.Paise <= b.Paise; }
        public static bool operator >=(Money a, Money b) { return a.Paise >= b.Paise; }

        public bool Equals(Money other) { return Paise == other.Paise; }
        public override bool Equals(object obj) { return obj is Money && Equals((Money)obj); }
        public override int GetHashCode() { return Paise.GetHashCode(); }
        public int CompareTo(Money other) { return Paise.CompareTo(other.Paise); }

        /// <summary>Formats as "1234.50" — no symbol. Callers add "Rs. " where needed.</summary>
        public override string ToString()
        {
            long p = Paise;
            bool neg = p < 0;
            if (neg) p = -p;
            long rup = p / 100L;
            long pai = p % 100L;
            string s = rup.ToString(CultureInfo.InvariantCulture) + "." + pai.ToString("D2", CultureInfo.InvariantCulture);
            return neg ? "-" + s : s;
        }

        /// <summary>
        /// Multiply by a tax rate expressed in basis points (1800 = 18.00%).
        /// Rounds half-away-from-zero to nearest paise. Never uses floating point.
        /// </summary>
        public Money ApplyRateBp(int rateBp)
        {
            // paise * rate / 10000, rounded
            long numer = Paise * rateBp;
            long q = numer / 10000L;
            long r = numer - q * 10000L;
            if (r >= 5000L) q += 1;
            else if (r <= -5000L) q -= 1;
            return new Money(q);
        }

        /// <summary>Round to nearest whole rupee, returning the round-off delta.</summary>
        public Money RoundToRupee(out Money delta)
        {
            long rem = Paise % 100L;
            long rounded;
            if (rem >= 50L) rounded = Paise + (100L - rem);
            else if (rem <= -50L) rounded = Paise - (100L + rem);
            else rounded = Paise - rem;
            delta = new Money(rounded - Paise);
            return new Money(rounded);
        }
    }
}
