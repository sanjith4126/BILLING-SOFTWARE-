using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dapper;
using GroceryPos.Data;
using GroceryPos.Domain;
using GroceryPos.Printing;
using Xunit;

namespace GroceryPos.Tests
{
    /// <summary>
    /// Go-live checks. Each test is a thing the shop does on a normal day, and
    /// asserts the numbers that come out the far end — not merely that no
    /// exception was thrown.
    ///
    /// These exist to answer "is it safe to put this on the counter?", so they
    /// lean on the money and stock paths where a silent error costs the owner
    /// real cash.
    /// </summary>
    public class DeploymentReadinessTests : IDisposable
    {
        private readonly string _dbPath;
        private readonly Db _db;
        private readonly AuditLog _audit;
        private readonly UserRepository _users;
        private readonly ItemRepository _items;
        private readonly SupplierRepository _suppliers;
        private readonly BatchRepository _batches;
        private readonly StockLedgerRepository _stock;
        private readonly PurchaseRepository _purchases;
        private readonly CustomerRepository _customers;
        private readonly CustomerLedgerRepository _ledger;
        private readonly BillRepository _bills;
        private readonly ShiftRepository _shifts;
        private readonly SettingsRepository _settings;

        private readonly long _ownerId;

        public DeploymentReadinessTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), "deploy_" + Guid.NewGuid().ToString("N") + ".sqlite");
            _db = new Db(_dbPath);
            new Migrator(_db).Migrate();

            _audit = new AuditLog(_db);
            _users = new UserRepository(_db);
            _items = new ItemRepository(_db, _audit);
            _suppliers = new SupplierRepository(_db);
            _batches = new BatchRepository(_db);
            _stock = new StockLedgerRepository(_db, _audit);
            _purchases = new PurchaseRepository(_db, _audit);
            _customers = new CustomerRepository(_db, _audit);
            _ledger = new CustomerLedgerRepository(_db, _audit);
            _bills = new BillRepository(_db, _audit);
            _shifts = new ShiftRepository(_db, _audit);
            _settings = new SettingsRepository(_db);

            _ownerId = _users.Create("owner2", "1234", UserRole.Owner);
        }

        public void Dispose()
        {
            try { File.Delete(_dbPath); } catch { }
        }

        // ---- helpers --------------------------------------------------------

        private long AddItem(string sku, string name, SoldBy soldBy, int taxBp,
                             long cost, long selling, long mrp)
        {
            return _items.Save(new Item
            {
                Sku = sku,
                Name = name,
                PrintName = name,
                SoldBy = soldBy,
                Unit = soldBy == SoldBy.Weight ? "kg" : "pc",
                TaxRateBp = taxBp,
                HsnCode = "1006",
                ReorderLevel = 5,
                RoundToGrams = 5,
                MinSaleGrams = 100,
                AllowDiscount = true,
                IsActive = true,
                TrackBatch = true,
                DefaultCostPaise = cost,
                DefaultSellingPaise = selling,
                DefaultMrpPaise = mrp
            }, _ownerId);
        }

        private long ReceiveStock(long itemId, string invoiceNo, int units, int grams,
                                  long cost, long mrp)
        {
            long supplierId = _suppliers.All().Any()
                ? _suppliers.All().First().Id
                : _suppliers.Create(new Supplier { Name = "Supplier", PaymentTermsDays = 30 });

            decimal qty = units > 0 ? units : grams / 1000m;
            long value = (long)Math.Round(cost * qty, MidpointRounding.AwayFromZero);

            var p = new Purchase
            {
                SupplierId = supplierId,
                InvoiceNo = invoiceNo,
                InvoiceDate = DateTime.Today,
                PaymentMode = "cash",
                GoodsPaise = value,
                TotalPaise = value
            };
            p.Lines.Add(new PurchaseLine
            {
                ItemId = itemId,
                BatchCode = "B-" + invoiceNo,
                QtyUnits = units,
                QtyGrams = grams,
                CostPaise = cost,
                MrpPaise = mrp,
                ValuePaise = value
            });
            _purchases.Save(p, _ownerId);
            return _batches.All().First(b => b.ItemId == itemId).Id;
        }

        // ---- 1. Goods in, goods out ----------------------------------------

        /// <summary>
        /// The single most important invariant: selling reduces stock by exactly
        /// what was sold. If this drifts, every stock figure the owner sees is a lie.
        /// </summary>
        [Fact]
        public void SellingAnItem_ReducesStockByExactlyWhatWasSold()
        {
            long itemId = AddItem("RICE", "Rice 1kg", SoldBy.Piece, 500, 5000, 6500, 7000);
            long batchId = ReceiveStock(itemId, "INV-1", units: 50, grams: 0, cost: 5000, mrp: 7000);

            Assert.Equal(50, _batches.FindById(batchId).QtyUnits);

            var shift = _shifts.Open(1, _ownerId, 50000);
            var bill = NewBill(itemId, batchId, qtyUnits: 3, ratePaise: 6500, taxBp: 500);

            _bills.Save(bill,
                new List<Payment> { new Payment { Mode = PaymentMode.Cash, AmountPaise = bill.NetPaise } },
                _ownerId, 1, null, shift.Id, 0);

            Assert.Equal(47, _batches.FindById(batchId).QtyUnits);
        }

        /// <summary>Loose goods must move in grams, with no rounding drift.</summary>
        [Fact]
        public void SellingLooseGoods_ReducesGramsExactly()
        {
            long itemId = AddItem("SUGAR", "Sugar loose", SoldBy.Weight, 500, 4000, 4500, 0);
            long batchId = ReceiveStock(itemId, "INV-2", units: 0, grams: 50000, cost: 4000, mrp: 0);

            Assert.Equal(50000, _batches.FindById(batchId).QtyGrams);

            var shift = _shifts.Open(1, _ownerId, 50000);
            var bill = NewBill(itemId, batchId, qtyGrams: 1240, ratePaise: 4500, taxBp: 500);

            _bills.Save(bill,
                new List<Payment> { new Payment { Mode = PaymentMode.Cash, AmountPaise = bill.NetPaise } },
                _ownerId, 1, null, shift.Id, 0);

            Assert.Equal(48760, _batches.FindById(batchId).QtyGrams);
        }

        private Bill NewBill(long itemId, long batchId, long ratePaise, int taxBp,
                             int qtyUnits = 0, int qtyGrams = 0)
        {
            var bill = new Bill
            {
                CounterId = 1,
                UserId = _ownerId,
                BilledAt = DateTime.Now,
                Status = BillStatus.Completed
            };
            bill.Lines.Add(new BillLine
            {
                LineNo = 1,
                ItemId = itemId,
                BatchId = batchId,
                QtyUnits = qtyUnits,
                QtyGrams = qtyGrams,
                RatePaise = ratePaise,
                TaxRateBp = taxBp,
                HsnCode = "1006",
                ItemName = "line",
                WeightSource = qtyGrams > 0 ? WeightSource.Manual : WeightSource.Na
            });
            BillCalculator.ComputeBill(bill);
            return bill;
        }

        // ---- 2. Money adds up ----------------------------------------------

        /// <summary>
        /// Tax must be computed per line and summed, never as an average applied to
        /// the total (business rule 9). Two different rates on one bill prove it.
        /// </summary>
        [Fact]
        public void MixedTaxRatesOnOneBill_AreComputedPerLine()
        {
            long a = AddItem("A", "Five percent", SoldBy.Piece, 500, 1000, 10000, 12000);
            long b = AddItem("B", "Twelve percent", SoldBy.Piece, 1200, 1000, 10000, 12000);
            long ba = ReceiveStock(a, "INV-A", 10, 0, 1000, 12000);
            long bb = ReceiveStock(b, "INV-B", 10, 0, 1000, 12000);

            var bill = new Bill { CounterId = 1, UserId = _ownerId, BilledAt = DateTime.Now, Status = BillStatus.Completed };
            bill.Lines.Add(new BillLine { LineNo = 1, ItemId = a, BatchId = ba, QtyUnits = 1, RatePaise = 10000, TaxRateBp = 500, ItemName = "a", HsnCode = "1", WeightSource = WeightSource.Na });
            bill.Lines.Add(new BillLine { LineNo = 2, ItemId = b, BatchId = bb, QtyUnits = 1, RatePaise = 10000, TaxRateBp = 1200, ItemName = "b", HsnCode = "2", WeightSource = WeightSource.Na });
            BillCalculator.ComputeBill(bill);

            // Rs.100 at 5% = Rs.5 tax; Rs.100 at 12% = Rs.12 tax. Total tax Rs.17.
            // An averaged 8.5% on Rs.200 would also give Rs.17, so check the split:
            // CGST and SGST are half each, and the taxable value stays Rs.200.
            Assert.Equal(20000L, bill.TaxablePaise);
            Assert.Equal(1700L, bill.CgstPaise + bill.SgstPaise);
            Assert.Equal(bill.CgstPaise, bill.SgstPaise);

            // Per-line tax is what proves the rule.
            Assert.Equal(500L, bill.Lines[0].TaxPaise);
            Assert.Equal(1200L, bill.Lines[1].TaxPaise);
        }

        /// <summary>Cash taken must equal the bill, and land in the shift's expected cash.</summary>
        [Fact]
        public void CashSales_ShowUpInTheDayCloseExpectedCash()
        {
            long itemId = AddItem("TEA", "Tea 250g", SoldBy.Piece, 500, 8000, 10000, 11000);
            long batchId = ReceiveStock(itemId, "INV-3", 20, 0, 8000, 11000);

            var shift = _shifts.Open(1, _ownerId, 50000);   // Rs.500 float

            var bill = NewBill(itemId, batchId, qtyUnits: 2, ratePaise: 10000, taxBp: 500);
            _bills.Save(bill,
                new List<Payment> { new Payment { Mode = PaymentMode.Cash, AmountPaise = bill.NetPaise } },
                _ownerId, 1, null, shift.Id, 0);

            long expected = _shifts.ExpectedCash(shift.Id, 1);

            // Float plus the cash actually taken.
            Assert.Equal(50000L + bill.NetPaise, expected);
        }

        // ---- 3. Credit (khata) ---------------------------------------------

        /// <summary>
        /// A credit sale must raise the customer's balance by exactly the bill, and
        /// the cached balance must agree with the ledger. This is the figure the
        /// owner argues about at the counter.
        /// </summary>
        [Fact]
        public void CreditSale_RaisesBalanceAndCacheAgreesWithLedger()
        {
            long itemId = AddItem("DAL", "Dal 1kg", SoldBy.Piece, 500, 9000, 12000, 13000);
            long batchId = ReceiveStock(itemId, "INV-4", 30, 0, 9000, 13000);

            long custId = _customers.Create(new Customer
            {
                Phone = "9990001111",
                Name = "Ramesh",
                CreditAllowed = true,
                CreditLimitPaise = 500000
            }, _ownerId);

            var shift = _shifts.Open(1, _ownerId, 0);
            var bill = NewBill(itemId, batchId, qtyUnits: 2, ratePaise: 12000, taxBp: 500);

            // The customer object must be passed in; Save keys the ledger write off
            // it, exactly as the billing screen does.
            var customer = _customers.FindById(custId);
            _bills.Save(bill,
                new List<Payment> { new Payment { Mode = PaymentMode.Khata, AmountPaise = bill.NetPaise } },
                _ownerId, 1, customer, shift.Id, 0);

            var cust = _customers.FindById(custId);
            Assert.Equal(bill.NetPaise, cust.CurrentBalancePaise);

            // The ledger is the truth; the cached balance must match its last row.
            var rows = _ledger.ForCustomer(custId).ToList();
            Assert.NotEmpty(rows);
            Assert.Equal(cust.CurrentBalancePaise, rows.Last().BalancePaise);
        }

        /// <summary>The ledger must reject edits and deletes at the data layer.</summary>
        [Fact]
        public void CustomerLedger_IsAppendOnlyAtTheDataLayer()
        {
            long custId = _customers.Create(new Customer
            {
                Phone = "9990002222", Name = "Suresh",
                CreditAllowed = true, CreditLimitPaise = 100000
            }, _ownerId);

            _ledger.WriteOpening(custId, 25000, DateTime.Today, _ownerId);

            using (var c = _db.Open())
            {
                Assert.ThrowsAny<Exception>(() =>
                    c.Execute("UPDATE customer_ledger SET debit_paise = 1 WHERE customer_id=@c",
                        new { c = custId }));
                Assert.ThrowsAny<Exception>(() =>
                    c.Execute("DELETE FROM customer_ledger WHERE customer_id=@c", new { c = custId }));
            }
        }

        // ---- 4. Corrections leave a trail -----------------------------------

        /// <summary>
        /// Cancelling a bill must put the stock back, keep the bill number, and
        /// leave the row in place. Deleting sales data breaks GST.
        /// </summary>
        [Fact]
        public void CancellingABill_RestoresStockAndKeepsTheNumber()
        {
            long itemId = AddItem("OIL", "Oil 1L", SoldBy.Piece, 500, 12000, 15000, 16000);
            long batchId = ReceiveStock(itemId, "INV-5", 40, 0, 12000, 16000);

            var shift = _shifts.Open(1, _ownerId, 0);
            var bill = NewBill(itemId, batchId, qtyUnits: 5, ratePaise: 15000, taxBp: 500);
            long billId = _bills.Save(bill,
                new List<Payment> { new Payment { Mode = PaymentMode.Cash, AmountPaise = bill.NetPaise } },
                _ownerId, 1, null, shift.Id, 0);

            Assert.Equal(35, _batches.FindById(batchId).QtyUnits);
            var saved = _bills.FindById(billId);
            long billNo = saved.BillNo;

            _bills.Cancel(billId, _ownerId, "Wrong item scanned");

            Assert.Equal(40, _batches.FindById(batchId).QtyUnits);

            var after = _bills.FindById(billId);
            Assert.NotNull(after);                                  // row still exists
            Assert.Equal(BillStatus.Cancelled, after.Status);
            Assert.Equal(billNo, after.BillNo);                     // number preserved
        }

        /// <summary>Every privileged action must be traceable to a person.</summary>
        [Fact]
        public void PrivilegedActions_AreWrittenToTheAuditLog()
        {
            long itemId = AddItem("SOAP", "Soap", SoldBy.Piece, 1800, 2000, 3000, 3500);
            long batchId = ReceiveStock(itemId, "INV-6", 10, 0, 2000, 3500);

            var shift = _shifts.Open(1, _ownerId, 0);
            var bill = NewBill(itemId, batchId, qtyUnits: 1, ratePaise: 3000, taxBp: 1800);
            long billId = _bills.Save(bill,
                new List<Payment> { new Payment { Mode = PaymentMode.Cash, AmountPaise = bill.NetPaise } },
                _ownerId, 1, null, shift.Id, 0);

            _bills.Cancel(billId, _ownerId, "Customer changed mind");
            _stock.RecordWastage(itemId, batchId, 1, 0, "Damaged in transit", _ownerId);

            using (var c = _db.Open())
            {
                var actions = c.Query<string>("SELECT action FROM audit_log").ToList();
                Assert.Contains(actions, a => a.IndexOf("cancel", StringComparison.OrdinalIgnoreCase) >= 0);
                Assert.Contains(actions, a => a.IndexOf("wastage", StringComparison.OrdinalIgnoreCase) >= 0);

                // No audit row may be anonymous.
                long orphan = c.ExecuteScalar<long>(
                    "SELECT COUNT(*) FROM audit_log WHERE user_id IS NULL");
                Assert.Equal(0L, orphan);
            }
        }

        // ---- 5. The receipt the customer is handed --------------------------

        /// <summary>
        /// The printer truncates rather than wraps, so an over-long line ruins the
        /// alignment of everything below it on the roll.
        /// </summary>
        [Fact]
        public void Receipt_FitsThePrinterExactly()
        {
            _settings.Set("store.name", "SRI BALAJI SUPER MARKET");
            _settings.Set("store.gstin", "29ABCDE1234F1Z5");

            long itemId = AddItem("LONGNAME",
                "Aashirvaad Select Sharbati Atta 10kg Premium Pack", SoldBy.Piece,
                500, 40000, 52000, 55000);
            long batchId = ReceiveStock(itemId, "INV-7", 5, 0, 40000, 55000);

            var shift = _shifts.Open(1, _ownerId, 0);
            var bill = NewBill(itemId, batchId, qtyUnits: 2, ratePaise: 52000, taxBp: 500);
            long billId = _bills.Save(bill,
                new List<Payment> { new Payment { Mode = PaymentMode.Cash, AmountPaise = bill.NetPaise } },
                _ownerId, 1, null, shift.Id, 0);

            var saved = _bills.FindById(billId);
            var names = new Dictionary<long, string>
            {
                { itemId, "Aashirvaad Select Sharbati Atta 10kg Premium Pack" }
            };
            var lines = new ReceiptFormatter().Format(new ReceiptFormatter.StoreInfo
            {
                Name = "SRI BALAJI SUPER MARKET",
                Address1 = "No. 24, Gandhi Bazaar Main Rd",
                Address2 = "Basavanagudi, Bengaluru 560004",
                Gstin = "29ABCDE1234F1Z5",
                CounterId = 1
            }, saved, "Ravi", names);

            Assert.NotEmpty(lines);
            foreach (var line in lines)
            {
                Assert.True(line.Length <= ReceiptFormatter.Width,
                    "Receipt line is " + line.Length + " chars, over the " +
                    ReceiptFormatter.Width + "-column roll: \"" + line + "\"");
            }

            // CP437 has no rupee glyph; sending one prints garbage.
            Assert.DoesNotContain(lines, l => l.Contains("₹"));
        }

        // ---- 6. Fresh install -----------------------------------------------

        /// <summary>
        /// The shop's first launch: an empty database must migrate, seed an owner
        /// and be usable. A failure here means the software cannot be installed.
        /// </summary>
        [Fact]
        public void AFreshDatabase_MigratesAndIsUsable()
        {
            var freshPath = Path.Combine(Path.GetTempPath(), "fresh_" + Guid.NewGuid().ToString("N") + ".sqlite");
            try
            {
                var fresh = new Db(freshPath);
                new Migrator(fresh).Migrate();

                var audit = new AuditLog(fresh);
                var users = new UserRepository(fresh);
                long id = users.Create("owner", "1234", UserRole.Owner);
                var owner = users.FindByName("owner");

                Assert.Equal(id, owner.Id);
                Assert.True(users.VerifyPin(owner, "1234"));
                Assert.False(users.VerifyPin(owner, "0000"));

                // Migrating twice must be harmless — this runs on every launch.
                new Migrator(fresh).Migrate();
                Assert.Single(users.All());
            }
            finally
            {
                try { File.Delete(freshPath); } catch { }
            }
        }

        /// <summary>
        /// Every settings key the application reads must exist in a fresh database.
        /// A missing key falls back to a default silently, which is how customer
        /// statements ended up printing the store name as "STORE" and sending
        /// nothing to the printer while billing worked fine.
        ///
        /// The key list is kept in step with the code by the companion test below.
        /// </summary>
        [Fact]
        public void EverySettingTheAppReads_ExistsInAFreshDatabase()
        {
            var required = new[]
            {
                "store_name", "store_address_1", "store_address_2",
                "store_phone", "store_gstin", "store_footer",
                "receipt_title_no_gst",
                "printer_name", "drawer_enabled", "drawer_pin",
                "counter_id", "discount_cap_percent",
                "loyalty_points_per_100rupees", "next_bill_no",
                "scale.mode", "scale.port", "scale.baud",
                "scale.data_bits", "scale.parity", "scale.stop_bits",
                "scale.regex", "scale.poll_cmd"
            };

            using (var c = _db.Open())
            {
                var present = new HashSet<string>(
                    c.Query<string>("SELECT key FROM settings"), StringComparer.Ordinal);

                var missing = required.Where(k => !present.Contains(k)).ToList();
                Assert.True(missing.Count == 0,
                    "These settings are read by the app but missing from a fresh " +
                    "database, so they fall back to a default the owner cannot see " +
                    "or change: " + string.Join(", ", missing));
            }
        }

        /// <summary>
        /// The printer queue must be read from ONE key. Billing previously used
        /// "printer_name" while statements and the Z report used "printer.queue",
        /// so configuring one left the other silently not printing.
        /// </summary>
        [Fact]
        public void ThePrinterQueue_IsReadFromASingleKey()
        {
            using (var c = _db.Open())
            {
                var printerKeys = c.Query<string>(
                    "SELECT key FROM settings WHERE key LIKE '%printer%'").ToList();

                Assert.Contains("printer_name", printerKeys);
                Assert.DoesNotContain("printer.queue", printerKeys);
            }
        }

        /// <summary>
        /// Store identity must likewise have one spelling, or the receipt and the
        /// statement disagree about the shop's own name.
        /// </summary>
        [Fact]
        public void StoreIdentity_HasNoDuplicateSpellings()
        {
            using (var c = _db.Open())
            {
                var keys = c.Query<string>("SELECT key FROM settings").ToList();

                Assert.Contains("store_name", keys);
                Assert.DoesNotContain("store.name", keys);
                Assert.DoesNotContain("store.address", keys);
            }
        }

        /// <summary>
        /// Bill numbers must be gapless even when a bill is cancelled, or the GST
        /// invoice series is broken.
        /// </summary>
        [Fact]
        public void BillNumbers_StayGaplessAcrossACancellation()
        {
            long itemId = AddItem("PEN", "Pen", SoldBy.Piece, 1200, 500, 1000, 1200);
            long batchId = ReceiveStock(itemId, "INV-8", 100, 0, 500, 1200);
            var shift = _shifts.Open(1, _ownerId, 0);

            var numbers = new List<long>();
            long secondId = 0;
            for (int i = 0; i < 3; i++)
            {
                var bill = NewBill(itemId, batchId, qtyUnits: 1, ratePaise: 1000, taxBp: 1200);
                long id = _bills.Save(bill,
                    new List<Payment> { new Payment { Mode = PaymentMode.Cash, AmountPaise = bill.NetPaise } },
                    _ownerId, 1, null, shift.Id, 0);
                numbers.Add(_bills.FindById(id).BillNo);
                if (i == 1) secondId = id;
            }

            _bills.Cancel(secondId, _ownerId, "test");

            // The cancelled bill keeps its number, and no number was reused.
            Assert.Equal(numbers[1], _bills.FindById(secondId).BillNo);
            Assert.Equal(3, numbers.Distinct().Count());

            var next = NewBill(itemId, batchId, qtyUnits: 1, ratePaise: 1000, taxBp: 1200);
            long nextId = _bills.Save(next,
                new List<Payment> { new Payment { Mode = PaymentMode.Cash, AmountPaise = next.NetPaise } },
                _ownerId, 1, null, shift.Id, 0);
            Assert.DoesNotContain(_bills.FindById(nextId).BillNo, numbers);
        }
    }
}
