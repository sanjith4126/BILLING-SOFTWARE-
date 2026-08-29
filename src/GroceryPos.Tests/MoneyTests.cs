using GroceryPos.Domain;
using Xunit;

namespace GroceryPos.Tests
{
    public class MoneyTests
    {
        [Fact]
        public void FormatsTwoDecimals()
        {
            Assert.Equal("12.50", Money.FromPaise(1250).ToString());
            Assert.Equal("0.05", Money.FromPaise(5).ToString());
            Assert.Equal("0.00", Money.Zero.ToString());
            Assert.Equal("-3.14", Money.FromPaise(-314).ToString());
        }

        [Fact]
        public void ParsesRupees()
        {
            Assert.Equal(1250L, Money.ParseRupees("12.50").Paise);
            Assert.Equal(1200L, Money.ParseRupees("12").Paise);
            Assert.Equal(1200L, Money.ParseRupees("12.0").Paise);
            Assert.Equal(-500L, Money.ParseRupees("-5.00").Paise);
        }

        [Fact]
        public void AppliesRateBpCorrectly()
        {
            // 100.00 * 18% = 18.00
            Assert.Equal(1800L, Money.FromPaise(10000).ApplyRateBp(1800).Paise);
            // 100.50 * 5% = 5.025 -> rounds to 5.03 (half-up)
            Assert.Equal(503L, Money.FromPaise(10050).ApplyRateBp(500).Paise);
        }

        [Fact]
        public void RoundsToRupee()
        {
            Money delta;
            Assert.Equal(77000L, Money.FromPaise(76950).RoundToRupee(out delta).Paise);
            Assert.Equal(50L, delta.Paise);
            Assert.Equal(76900L, Money.FromPaise(76949).RoundToRupee(out delta).Paise);
            Assert.Equal(-49L, delta.Paise);
        }
    }

    public class GramsTests
    {
        [Fact]
        public void FormatsKgAndG()
        {
            Assert.Equal("1.240kg", new Grams(1240).ToString());
            Assert.Equal("245g", new Grams(245).ToString());
            Assert.Equal("30.000kg", new Grams(30000).ToString());
        }

        [Fact]
        public void RoundsToStep()
        {
            Assert.Equal(1240, new Grams(1238).RoundToStep(5).Value);
            Assert.Equal(1240, new Grams(1242).RoundToStep(5).Value);
            Assert.Equal(1245, new Grams(1243).RoundToStep(5).Value);
            Assert.Equal(1245, new Grams(1247).RoundToStep(5).Value);
            Assert.Equal(1250, new Grams(1248).RoundToStep(5).Value);
            Assert.Equal(30010, new Grams(30008).RoundToStep(10).Value);
        }
    }
}
