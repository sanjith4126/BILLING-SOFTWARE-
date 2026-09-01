using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using Dapper;
using GroceryPos.Data;
using GroceryPos.Domain;
using Xunit;

namespace GroceryPos.Tests
{
    /// <summary>
    /// End-to-end smoke test that walks the same code paths the WinForms UI
    /// invokes when the user runs through the 9-step opening-day scenario:
    ///
    ///   1. Owner creates a cashier account, changes own PIN.
    ///   2. Cashier logs in.
    ///   3. Cashier opens a shift with a starting float.
    ///   4. Owner adds two items — one piece-sold, one weight-sold with
    ///      round_to = 5g and min_sale = 100g.
    ///   5. Owner records a purchase for both items — stock arrives.
    ///   6. Cashier attaches a customer by phone (Ctrl+K), adds both items
    ///      to a bill, weighs the loose one, takes split cash+UPI payment.
    ///   7. Owner cancels the bill — stock must be restored.
    ///   8. Owner queries a few reports — they must not throw.
    ///   9. Cashier closes the shift with denomination counts.
    ///
    /// This does not exercise the WinForms controls themselves (focus,
    /// keystrokes, message boxes) but does drive every domain and data
    /// method that those controls call. Anything that breaks here would
    /// also break in the UI at runtime.
    /// </summary>
    public class EndToEndScenarioTests : IDisposable
    {
        private readonly string _path;
        private readonly Db _db;
        private readonly AuditLog _audit;

        public EndToEndScenarioTests()
        {
            _path = Path.Combine(Path.GetTempPath(), "gpos_e2e_" + Guid.NewGuid().ToString("N") + ".sqlite");
            _db = new Db(_path);
            new Migrator(_db).Migrate();
            _audit = new AuditLog(_db);

            // Seed the default owner the same way Program.cs does on first run.
            using (var c = _db.Open())
                if (c.QueryFirstOrDefault<long>("SELECT COUNT(1) FROM users") == 0)
                    new UserRepository(_db).Create("owner", "1234", UserRole.Owner);
        }

        public void Dispose()
        {
            SQLiteConnection.ClearAllPools();
            try { File.Delete(_path); } catch { }
        }

        [Fact]
        public void FullOpeningDayScenarioRunsWithoutException()
        {
            var users = new UserRepository(_db);
            var items = new ItemRepository(_db, _audit);
            var suppliers = new SupplierRepository(_db);
            var batches = new BatchRepository(_db);
            var stock = new StockLedgerRepository(_db, _audit);
            var purchases = new PurchaseRepository(_db, _audit);
            var customers = new CustomerRepository(_db, _audit);
            var bills = new BillRepository(_db, _audit);
            var shifts = new ShiftRepository(_db, _audit);

            // ================================================================
            // Step 2: owner login, change PIN, add cashier
            // ================================================================
            var owner = users.FindByName("owner");
            Assert.NotNull(owner);
            Assert.True(users.VerifyPin(owner, "1234"), "seeded owner PIN should verify");

            users.SetPin(owner.Id, "9999");
            owner = users.FindByName("owner");
            Assert.True(users.VerifyPin(owner, "9999"), "PIN change should take effect");
            Assert.False(users.VerifyPin(owner, "1234"), "old PIN must no longer work");

            long cashierId = users.Create("test_cashier", "1111", UserRole.Cashier);
            var cashier = users.FindByName("test_cashier");
            Assert.NotNull(cashier);
            Assert.True(users.VerifyPin(cashier, "1111"));
            Assert.Equal(UserRole.Cashier, cashier.Role);

            // ================================================================
            // Step 3: cashier opens a shift with Rs.500 float
            // ================================================================
            const int counterId = 1;
            Assert.Null(shifts.OpenShiftFor(counterId));
            var shift = shifts.Open(counterId, cashier.Id, 50000L); // Rs.500 = 50000 paise
            Assert.NotNull(shift);
            Assert.Equal(50000L, shift.OpeningFloatPaise);
            Assert.Equal(shift.Id, shifts.OpenShiftFor(counterId).Id);

            // ================================================================
            // Step 4: owner creates two items
            //   A: piece-sold  (e.g. biscuit packet)
            //   B: weight-sold (e.g. tomato loose) with round_to 5g, min 100g
            // ================================================================
            long itemPieceId = items.Save(new Item
            {
                Sku = "BISCUIT1",
                Name = "Biscuit Packet",
                PrintName = "Biscuit Packet",
                SoldBy = SoldBy.Piece,
                Unit = "pc",
                TaxRateBp = 0,
                HsnCode = "1905",
                ReorderLevel = 5,
                AllowDiscount = true,
                IsActive = true
            }, owner.Id);

            long itemWeightId = items.Save(new Item
            {
                Sku = "TOMATO",
                Name = "Tomato Loose",
                PrintName = "Tomato Loose",
                SoldBy = SoldBy.Weight,
                Unit = "g",
                TaxRateBp = 0,
                HsnCode = "0702",
                ReorderLevel = 1000,
                AllowDiscount = true,
                WeighAtCounter = true,
                RoundToGrams = 5,
                MinSaleGrams = 100,
                IsActive = true
            }, owner.Id);

            var loadedWeight = items.FindById(itemWeightId);
            Assert.Equal(SoldBy.Weight, loadedWeight.SoldBy);
            Assert.Equal(5, loadedWeight.RoundToGrams);
            Assert.Equal(100, loadedWeight.MinSaleGrams);

            // ================================================================
            // Step 5: owner records a purchase — stock arrives via the ledger
            // ================================================================
            long supId = suppliers.Create(new Supplier { Name = "Test Wholesaler", Phone = "9000000000" });

            var purchase = new Purchase
            {
                SupplierId = supId,
                InvoiceNo = "SUP-INV-001",
                InvoiceDate = DateTime.Today,
                Lines = new List<PurchaseLine>
                {
                    new PurchaseLine {
                        ItemId = itemPieceId, BatchCode = "BISC-BATCH-1",
                        QtyUnits = 20, CostPaise = 1500, MrpPaise = 2500
                    },
                    new PurchaseLine {
                        ItemId = itemWeightId, BatchCode = "TOMATO-BATCH-1",
                        QtyGrams = 10000, CostPaise = 3000, MrpPaise = 5000
                    }
                }
            };
            purchases.Save(purchase, owner.Id);

            var biscuitBatches = batches.ForItemFifo(itemPieceId);
            var tomatoBatches = batches.ForItemFifo(itemWeightId);
            Assert.Single(biscuitBatches);
            Assert.Single(tomatoBatches);
            Assert.Equal(20, biscuitBatches[0].QtyUnits);
            Assert.Equal(10000, tomatoBatches[0].QtyGrams);
            long biscuitBatchId = biscuitBatches[0].Id;
            long tomatoBatchId = tomatoBatches[0].Id;

            // ================================================================
            // Step 6: cashier attaches customer, builds a mixed bill,
            //         weighs the loose one, takes split cash+UPI payment
            // ================================================================
            long custId = customers.Create(new Customer
            {
                Phone = "9876543210",
                IsActive = true  // name deliberately blank — loyalty-only customer
            }, cashier.Id);
            var cust = customers.FindById(custId);

            // Weight rounded to nearest 5g (item's round_to). Say scale reads 247g -> 245g.
            int rawGrams = 247;
            int roundedGrams = new Grams(rawGrams).RoundToStep(loadedWeight.RoundToGrams).Value;
            Assert.Equal(245, roundedGrams);
            Assert.True(roundedGrams >= loadedWeight.MinSaleGrams, "must meet 100g minimum");

            var bill = new Bill
            {
                Lines = new List<BillLine>
                {
                    new BillLine {
                        LineNo = 1, ItemId = itemPieceId, BatchId = biscuitBatchId,
                        QtyUnits = 2, RatePaise = 2500, TaxRateBp = 0,
                        WeightSource = WeightSource.Na,
                        HsnCode = "1905", ItemName = "Biscuit Packet"
                    },
                    new BillLine {
                        LineNo = 2, ItemId = itemWeightId, BatchId = tomatoBatchId,
                        QtyGrams = roundedGrams, RawGrams = rawGrams,
                        RatePaise = 5000, // per kg
                        TaxRateBp = 0,
                        WeightSource = WeightSource.Manual,
                        HsnCode = "0702", ItemName = "Tomato Loose"
                    }
                }
            };
            BillCalculator.ComputeBill(bill);

            // 2 biscuits @ Rs.25 = 5000 paise ; 245g tomato @ Rs.50/kg = 5000*245/1000 = 1225 paise
            // subtotal 6225, net rounded to nearest rupee.
            Assert.Equal(6225L, bill.SubtotalPaise);
            long expectedNetBeforeRounding = 6225L;
            Assert.InRange(bill.NetPaise, expectedNetBeforeRounding - 50, expectedNetBeforeRounding + 50);

            long netPaise = bill.NetPaise;
            long cashPaise = 5000L;                  // Rs.50 in cash
            long upiPaise = netPaise - cashPaise;    // remainder on UPI
            var payments = new List<Payment>
            {
                new Payment { Mode = PaymentMode.Cash, AmountPaise = cashPaise },
                new Payment { Mode = PaymentMode.Upi,  AmountPaise = upiPaise, Reference = "UPI-XYZ-1" }
            };

            long billId = bills.Save(bill, payments, cashier.Id, counterId, cust, shift.Id, loyaltyPointsPer100Rupees: 1);

            var saved = bills.FindById(billId);
            Assert.NotNull(saved);
            Assert.Equal(BillStatus.Completed, saved.Status);
            Assert.Equal(2, saved.Lines.Count);
            Assert.Equal(2, saved.Payments.Count);
            Assert.Equal(cashPaise + upiPaise, saved.Payments.Sum(p => p.AmountPaise));
            Assert.True(saved.BillNo >= 1, "bill number must be issued");

            // Stock should have dropped by the amounts sold.
            var biscuitAfterSale = batches.FindById(biscuitBatchId);
            var tomatoAfterSale = batches.FindById(tomatoBatchId);
            Assert.Equal(20 - 2, biscuitAfterSale.QtyUnits);
            Assert.Equal(10000 - roundedGrams, tomatoAfterSale.QtyGrams);

            // Loyalty: 1 point per Rs.100 spent, rounded down. Net ~Rs.62.25 -> 0 points.
            var custAfter = customers.FindById(custId);
            long expectedPoints = (netPaise / 10000L) * 1;
            Assert.Equal(expectedPoints, custAfter.LoyaltyPoints);

            // The receipt formatter must accept a loyalty block for this bill.
            var fmt = new GroceryPos.Printing.ReceiptFormatter();
            var receipt = fmt.Format(
                new GroceryPos.Printing.ReceiptFormatter.StoreInfo
                {
                    Name = "AKIL STORE", Phone = "9698776767",
                    Footer = "Thank you, Visit Again!!!", CounterId = counterId
                },
                saved, cashier.Name,
                new Dictionary<long, string> { { itemPieceId, "Biscuit Packet" }, { itemWeightId, "Tomato Loose" } },
                false, null, null,
                new GroceryPos.Printing.ReceiptFormatter.LoyaltyBlock
                {
                    CustomerPhone = custAfter.Phone,
                    PointsEarnedThisBill = expectedPoints,
                    PointsBalance = custAfter.LoyaltyPoints
                });
            Assert.Contains(receipt, l => l.Contains("CASH BILL")); // no GSTIN -> title switches
            Assert.Contains(receipt, l => l.Contains("Customer: 9876543210"));
            Assert.All(receipt, l => Assert.True(l.Length <= GroceryPos.Printing.ReceiptFormatter.Width,
                "receipt line > 48 chars: [" + l + "]"));

            // ================================================================
            // Step 7: cancel the bill. Stock must be restored, bill_no preserved.
            // ================================================================
            long billNoBefore = saved.BillNo;
            bills.Cancel(billId, owner.Id, "test cancel");

            var cancelled = bills.FindById(billId);
            Assert.Equal(BillStatus.Cancelled, cancelled.Status);
            Assert.Equal(billNoBefore, cancelled.BillNo); // number preserved

            var biscuitAfterCancel = batches.FindById(biscuitBatchId);
            var tomatoAfterCancel = batches.FindById(tomatoBatchId);
            Assert.Equal(20, biscuitAfterCancel.QtyUnits);        // fully restored
            Assert.Equal(10000, tomatoAfterCancel.QtyGrams);      // fully restored

            // ================================================================
            // Step 8: reports queries must not throw. We hit the SQL the
            //   report grids run, since instantiating the WinForms in a
            //   headless test is not possible.
            // ================================================================
            using (var c = _db.Open())
            {
                // Sales register — every bill, completed and cancelled
                var salesRows = c.Query<dynamic>(@"
                    SELECT bill_no, billed_at, status, net_paise
                      FROM bills ORDER BY bill_no").ToList();
                Assert.Single(salesRows);

                // Dashboard — today's totals
                var todayTotal = c.QueryFirstOrDefault<long?>(@"
                    SELECT COALESCE(SUM(net_paise),0) FROM bills
                     WHERE status='completed' AND date(billed_at) = date('now')");
                Assert.NotNull(todayTotal);

                // Cashier performance
                var byCashier = c.Query<dynamic>(@"
                    SELECT u.name AS name, COUNT(1) AS bills_ct, COALESCE(SUM(b.net_paise),0) AS net
                      FROM bills b JOIN users u ON u.id = b.user_id
                     GROUP BY u.name").ToList();
                Assert.True(byCashier.Count >= 1);

                // Item movement
                var moved = c.Query<dynamic>(@"
                    SELECT item_id, COALESCE(SUM(-change_units),0) AS units_sold
                      FROM stock_ledger WHERE reason='sale' GROUP BY item_id").ToList();
                Assert.NotNull(moved);

                // Credit ageing — no khata activity so empty. Still must not throw.
                var ageing = c.Query<dynamic>(@"
                    SELECT c.id, c.phone, c.current_balance_paise
                      FROM customers c WHERE c.current_balance_paise != 0").ToList();
                Assert.NotNull(ageing);

                // Stock valuation
                var stockVal = c.QueryFirstOrDefault<long?>(@"
                    SELECT COALESCE(SUM(cost_paise * (qty_units + qty_grams)),0) FROM batches");
                Assert.NotNull(stockVal);
            }

            // ================================================================
            // Step 9: close the shift with denomination counts
            // ================================================================
            // Denominations table: (value_paise, count). Rs.500 x 1, Rs.100 x 5,
            // Rs.10 x 3 -> Rs.500 + Rs.500 + Rs.30 = Rs.1030 counted.
            var denoms = new List<Tuple<long, int>>
            {
                Tuple.Create(50000L, 1),   // 1 x Rs.500
                Tuple.Create(10000L, 5),   // 5 x Rs.100
                Tuple.Create(1000L, 3)     // 3 x Rs.10
            };
            shifts.Close(shift.Id, denoms, cashier.Id);

            var closed = shifts.FindById(shift.Id);
            Assert.Equal(ShiftStatus.Closed, closed.Status);
            Assert.NotNull(closed.ClosedAt);

            // Any further insert against the closed shift should be blocked.
            Assert.Throws<InvalidOperationException>(() =>
                shifts.RecordPettyCash(shift.Id, 100L, "post-close petty", cashier.Id));

            // A new shift can be opened after close.
            var nextShift = shifts.Open(counterId, cashier.Id, 20000L);
            Assert.NotEqual(shift.Id, nextShift.Id);
        }
    }
}
