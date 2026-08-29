using System;
using System.Globalization;

namespace GroceryPos.Domain
{
    /// <summary>Weight in integer grams. Never floating point.</summary>
    public struct Grams : IEquatable<Grams>, IComparable<Grams>
    {
        public int Value { get; }

        public Grams(int grams) { Value = grams; }

        public static Grams Zero => new Grams(0);
        public static Grams FromGrams(int g) { return new Grams(g); }

        public static Grams operator +(Grams a, Grams b) { return new Grams(a.Value + b.Value); }
        public static Grams operator -(Grams a, Grams b) { return new Grams(a.Value - b.Value); }
        public static bool operator ==(Grams a, Grams b) { return a.Value == b.Value; }
        public static bool operator !=(Grams a, Grams b) { return a.Value != b.Value; }
        public static bool operator <(Grams a, Grams b) { return a.Value < b.Value; }
        public static bool operator >(Grams a, Grams b) { return a.Value > b.Value; }
        public static bool operator <=(Grams a, Grams b) { return a.Value <= b.Value; }
        public static bool operator >=(Grams a, Grams b) { return a.Value >= b.Value; }

        public bool Equals(Grams other) { return Value == other.Value; }
        public override bool Equals(object obj) { return obj is Grams && Equals((Grams)obj); }
        public override int GetHashCode() { return Value.GetHashCode(); }
        public int CompareTo(Grams other) { return Value.CompareTo(other.Value); }

        /// <summary>Round to nearest step (e.g. 5g). Half rounds up.</summary>
        public Grams RoundToStep(int stepGrams)
        {
            if (stepGrams <= 0) throw new ArgumentException("Step must be positive");
            int rem = Value % stepGrams;
            if (rem == 0) return this;
            if (rem >= stepGrams / 2 + (stepGrams % 2))
                return new Grams(Value + (stepGrams - rem));
            return new Grams(Value - rem);
        }

        /// <summary>"1.240kg" for >=1000g, else "245g".</summary>
        public override string ToString()
        {
            if (Value >= 1000 || Value <= -1000)
            {
                int abs = Value < 0 ? -Value : Value;
                int kg = abs / 1000;
                int g = abs % 1000;
                string s = kg.ToString(CultureInfo.InvariantCulture) + "." +
                           g.ToString("D3", CultureInfo.InvariantCulture) + "kg";
                return Value < 0 ? "-" + s : s;
            }
            return Value.ToString(CultureInfo.InvariantCulture) + "g";
        }
    }
}
