using System;
using System.IO;
using System.Linq;
using GroceryPos.Data;
using GroceryPos.Domain;
using Xunit;

namespace GroceryPos.Tests
{
    /// <summary>
    /// Scanning the same packet twice must show "2", not two identical lines,
    /// and a quantity typed over the top must reprice the line correctly.
    /// </summary>
    public class BillEditingTests : IDisposable
    {
        private readonly string _dbPath;

        public BillEditingTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), "edit_" + Guid.NewGuid().ToString("N") + ".sqlite");
            var db = new Db(_dbPath);
            new Migrator(db).Migrate();
        }

        public void Dispose() { try { File.Delete(_dbPath); } catch { } }

        private static BillLine Line(long itemId, int units, long rate, int taxBp = 500)
        {
            return new BillLine
            {
                LineNo = 1, ItemId = itemId, BatchId = 1,
                QtyUnits = units, RatePaise = rate, TaxRateBp = taxBp,
                ItemName = "x", HsnCode = "1", WeightSource = WeightSource.Na
            };
        }

        /// <summary>Raising the quantity must multiply the amount, not just the tax.</summary>
        [Fact]
        public void RaisingTheQuantity_RepricesTheLine()
        {
            var bill = new Bill { CounterId = 1, BilledAt = DateTime.Now, Status = BillStatus.Completed };
            bill.Lines.Add(Line(1, 1, 10000));      // one at Rs.100
            BillCalculator.ComputeBill(bill);
            long oneNet = bill.NetPaise;

            bill.Lines[0].QtyUnits = 3;             // as if "3" were typed into Qty
            BillCalculator.ComputeBill(bill);

            // Three at Rs.100 is Rs.300 of goods, so the taxable value must be
            // exactly three times the single-item case.
            Assert.Equal(30000L, bill.TaxablePaise);
            Assert.True(bill.NetPaise > oneNet * 2,
                "Three of an item should cost about three times one.");
        }

        /// <summary>A discount typed on the line must come off the taxable value.</summary>
        [Fact]
        public void ADiscountOnTheLine_ReducesWhatIsTaxed()
        {
            var bill = new Bill { CounterId = 1, BilledAt = DateTime.Now, Status = BillStatus.Completed };
            var l = Line(1, 2, 10000);              // two at Rs.100 = Rs.200
            bill.Lines.Add(l);
            BillCalculator.ComputeBill(bill);
            long taxBefore = bill.CgstPaise + bill.SgstPaise;

            l.DiscountPaise = 5000;                 // Rs.50 off
            BillCalculator.ComputeBill(bill);

            Assert.Equal(15000L, bill.TaxablePaise);
            Assert.True(bill.CgstPaise + bill.SgstPaise < taxBefore,
                "Tax must be charged on the discounted amount, not the full one.");
        }

        /// <summary>
        /// Two weighings of the same product are two different weights and must
        /// stay on separate lines, so the customer can see each one.
        /// </summary>
        [Fact]
        public void TwoWeighings_StayOnSeparateLines()
        {
            var bill = new Bill { CounterId = 1, BilledAt = DateTime.Now, Status = BillStatus.Completed };
            bill.Lines.Add(new BillLine
            {
                LineNo = 1, ItemId = 7, BatchId = 1, QtyGrams = 1240, RatePaise = 4000,
                TaxRateBp = 0, ItemName = "Tomato", HsnCode = "0702",
                WeightSource = WeightSource.Scale
            });
            bill.Lines.Add(new BillLine
            {
                LineNo = 2, ItemId = 7, BatchId = 1, QtyGrams = 860, RatePaise = 4000,
                TaxRateBp = 0, ItemName = "Tomato", HsnCode = "0702",
                WeightSource = WeightSource.Scale
            });
            BillCalculator.ComputeBill(bill);

            Assert.Equal(2, bill.Lines.Count);
            Assert.Equal(4960L, bill.Lines[0].AmountPaise);   // 1.240kg x Rs.40
            Assert.Equal(3440L, bill.Lines[1].AmountPaise);   // 0.860kg x Rs.40
        }
    }
}
