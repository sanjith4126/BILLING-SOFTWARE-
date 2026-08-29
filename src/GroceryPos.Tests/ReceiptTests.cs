using System.Collections.Generic;
using System.Text;
using GroceryPos.Domain;
using GroceryPos.Printing;
using Xunit;

namespace GroceryPos.Tests
{
    public class ReceiptTests
    {
        private static Bill SampleBill()
        {
            return new Bill
            {
                BillNo = 2451,
                CounterId = 1,
                BilledAt = new System.DateTime(2026, 8, 28, 18, 42, 0),
                Status = BillStatus.Completed,
                SubtotalPaise = 70970,
                DiscountPaise = 0,
                TaxablePaise = 70970,
                CgstPaise = 1495,
                SgstPaise = 1495,
                RoundOffPaise = 40,
                NetPaise = 77000,
                Lines = new List<BillLine>
                {
                    new BillLine { ItemId = 1, QtyUnits = 1, RatePaise = 28500, AmountPaise = 28500, HsnCode="1101" },
                    new BillLine { ItemId = 2, QtyGrams = 1240, RatePaise = 4000, AmountPaise = 4960, HsnCode="0702" }
                },
                Payments = new List<Payment>
                {
                    new Payment { Mode = PaymentMode.Cash, AmountPaise = 77000 }
                }
            };
        }

        [Fact]
        public void EveryLineIsAtMost48Chars()
        {
            var fmt = new ReceiptFormatter();
            var names = new Dictionary<long, string> { { 1, "Aashirvaad atta 5kg" }, { 2, "Tomato loose" } };
            var lines = fmt.Format(new ReceiptFormatter.StoreInfo
            {
                Name = "SRI BALAJI SUPER MARKET",
                Address1 = "No. 24, Gandhi Bazaar Main Rd",
                Address2 = "Basavanagudi, Bengaluru 560004",
                Gstin = "29ABCDE1234F1Z5",
                CounterId = 1
            }, SampleBill(), "Ravi", names);

            foreach (var l in lines)
                Assert.True(l.Length <= ReceiptFormatter.Width, "Line too long ("+l.Length+"): [" + l + "]");
        }

        [Fact]
        public void HandlesLongItemName()
        {
            var fmt = new ReceiptFormatter();
            var names = new Dictionary<long, string>
            {
                { 1, "Some Very Long Item Name That Does Not Fit In 22 Columns At All" }
            };
            var bill = SampleBill();
            bill.Lines = new List<BillLine> {
                new BillLine { ItemId=1, QtyUnits=1, RatePaise=10000, AmountPaise=10000 }
            };
            var lines = fmt.Format(new ReceiptFormatter.StoreInfo { Name="X", CounterId=1 }, bill, "U", names);
            foreach (var l in lines)
                Assert.True(l.Length <= ReceiptFormatter.Width);
        }

        [Fact]
        public void EscPosUsesCp437AndExplicitLf()
        {
            var lines = new List<string> { "Rs. 100.00", "Test line" };
            var bytes = EscPos.Build(lines, cut: true, drawerKick: false, drawerPin: 0);
            // Contains explicit LF after each line
            int lfCount = 0;
            foreach (var b in bytes) if (b == 0x0A) lfCount++;
            Assert.True(lfCount >= 2);
            // CP437 encoding: "Rs." bytes are ASCII 82,115,46
            var enc = Encoding.GetEncoding(437);
            byte[] rs = enc.GetBytes("Rs.");
            Assert.Equal(new byte[] { 82, 115, 46 }, rs);
        }

        [Fact]
        public void SafeAsciiStripsRupeeAndSmartQuotes()
        {
            Assert.Equal("Rs.100", ReceiptFormatter.SafeAscii("₹100"));
            Assert.Equal("it's a \"test\"", ReceiptFormatter.SafeAscii("it’s a “test”"));
        }
    }
}
