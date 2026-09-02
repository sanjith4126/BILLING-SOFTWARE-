using System;
using System.Collections.Generic;
using System.IO;
using GroceryPos.Domain;
using GroceryPos.Printing;
using Xunit;
using Xunit.Abstractions;

namespace GroceryPos.Tests
{
    /// <summary>
    /// Prints a representative receipt to the test output so the exact 48-column
    /// layout can be eyeballed before the printer is ever connected.
    /// </summary>
    public class ReceiptPreviewTest
    {
        private readonly ITestOutputHelper _out;
        public ReceiptPreviewTest(ITestOutputHelper o) { _out = o; }

        [Fact]
        public void ShowARealReceipt()
        {
            var bill = new Bill
            {
                BillNo = 2451,
                CounterId = 1,
                BilledAt = new DateTime(2026, 9, 2, 18, 42, 0),
                Status = BillStatus.Completed
            };
            bill.Lines.Add(new BillLine
            {
                LineNo = 1, ItemId = 1, QtyUnits = 1, RatePaise = 28500,
                TaxRateBp = 500, HsnCode = "1101", ItemName = "Aashirvaad atta 5kg",
                WeightSource = WeightSource.Na
            });
            bill.Lines.Add(new BillLine
            {
                LineNo = 2, ItemId = 2, QtyGrams = 1240, RatePaise = 4000,
                TaxRateBp = 0, HsnCode = "0702", ItemName = "Tomato loose",
                WeightSource = WeightSource.Scale
            });
            BillCalculator.ComputeBill(bill);
            bill.Payments.Add(new Payment { Mode = PaymentMode.Cash, AmountPaise = bill.NetPaise });
            // multiple qty so Items vs Total Qty differ
            bill.Lines[0].QtyUnits = 3;
            BillCalculator.ComputeBill(bill);

            var names = new Dictionary<long, string>
            {
                { 1, "Aashirvaad atta 5kg" }, { 2, "Tomato loose" }
            };

            var lines = new ReceiptFormatter().Format(new ReceiptFormatter.StoreInfo
            {
                Name = "SRI BALAJI SUPER MARKET",
                Address1 = "No. 24, Gandhi Bazaar Main Rd",
                Address2 = "Basavanagudi, Bengaluru 560004",
                Phone = "9698776767",
                Gstin = "29ABCDE1234F1Z5",
                Footer = "Thank you, Visit Again!!!",
                CounterId = 1
            }, bill, "Ravi", names);

            var dump = Path.Combine(Path.GetTempPath(), "receipt_preview.txt");
            using (var w = new StreamWriter(dump))
            {
                w.WriteLine("         1         2         3         4        ");
                w.WriteLine("123456789012345678901234567890123456789012345678");
                w.WriteLine(new string('=', 48));
                foreach (var l in lines) w.WriteLine(l);
                w.WriteLine(new string('=', 48));
            }

            _out.WriteLine("         1         2         3         4        ");
            _out.WriteLine("123456789012345678901234567890123456789012345678");
            _out.WriteLine(new string('=', 48));
            foreach (var l in lines) _out.WriteLine(l);
            _out.WriteLine(new string('=', 48));

            foreach (var l in lines)
                Assert.True(l.Length <= 48, "over 48: " + l);
        }
    }
}
