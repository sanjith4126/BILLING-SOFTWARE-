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
    public class CreditAndStockTests : IDisposable
    {
        private readonly string _path;
        private readonly Db _db;
        private readonly AuditLog _audit;

        public CreditAndStockTests()
        {
            _path = Path.Combine(Path.GetTempPath(), "gpos_cs_" + Guid.NewGuid().ToString("N") + ".sqlite");
            _db = new Db(_path);
            new Migrator(_db).Migrate();
            _audit = new AuditLog(_db);
            using (var c = _db.Open())
            {
                c.Execute("INSERT INTO users(id,name,pin_hash,role) VALUES(1,'test','x','owner')");
                c.Execute("INSERT INTO items(id,name,print_name,sold_by,unit) VALUES(1,'atta','atta','piece','pc')");
                c.Execute("INSERT INTO items(id,name,print_name,sold_by,unit) VALUES(2,'atta_loose','atta_loose','weight','g')");
                c.Execute(@"INSERT INTO batches(id,item_id,batch_code,cost_paise,mrp_paise,selling_paise,qty_units,qty_grams)
                            VALUES(11,1,'BAG',400000,500000,500000,2,0)");
                c.Execute(@"INSERT INTO batches(id,item_id,batch_code,cost_paise,mrp_paise,selling_paise,qty_units,qty_grams)
                            VALUES(12,2,'LOOSE',8,10,10,0,0)");
            }
        }

        public void Dispose()
        {
            SQLiteConnection.ClearAllPools();
            try { File.Delete(_path); } catch { }
        }

        [Fact]
        public void CreditPaymentAllocatesFifo()
        {
            var custRepo = new CustomerRepository(_db, _audit);
            var bills = new BillRepository(_db, _audit);
            var pay = new CreditPaymentRepository(_db, _audit);

            long cid = custRepo.Create(new Customer { Name = "K", Phone = "9999", CreditAllowed = true, CreditLimitPaise = 10000000, IsActive = true }, 1);
            var cust = custRepo.FindById(cid);

            // Two credit sales
            var b1 = new Bill { Lines = new List<BillLine> { new BillLine { ItemId = 1, BatchId = 11, QtyUnits = 1, RatePaise = 500000, TaxRateBp = 0 } } };
            Domain.BillCalculator.ComputeBill(b1);
            bills.Save(b1, new List<Payment> { new Payment { Mode = PaymentMode.Khata, AmountPaise = b1.NetPaise } }, 1, 1, cust, null, 0);
            cust = custRepo.FindById(cid);

            var b2 = new Bill { Lines = new List<BillLine> { new BillLine { ItemId = 1, BatchId = 11, QtyUnits = 1, RatePaise = 500000, TaxRateBp = 0 } } };
            Domain.BillCalculator.ComputeBill(b2);
            bills.Save(b2, new List<Payment> { new Payment { Mode = PaymentMode.Khata, AmountPaise = b2.NetPaise } }, 1, 1, cust, null, 0);

            // Pay amount that covers exactly bill 1
            pay.Receive(cid, b1.NetPaise, PaymentMode.Cash, "", 1, null, null);

            using (var c = _db.Open())
            {
                long alloc1 = c.ExecuteScalar<long>("SELECT COALESCE(SUM(allocated_paise),0) FROM credit_allocations WHERE bill_id=@i", new { i = b1.Id });
                long alloc2 = c.ExecuteScalar<long>("SELECT COALESCE(SUM(allocated_paise),0) FROM credit_allocations WHERE bill_id=@i", new { i = b2.Id });
                Assert.Equal(b1.NetPaise, alloc1);
                Assert.Equal(0, alloc2);
            }

            var final = custRepo.FindById(cid);
            Assert.Equal(b2.NetPaise, final.CurrentBalancePaise);
        }

        [Fact]
        public void StockConversionBalances()
        {
            var stock = new StockLedgerRepository(_db, _audit);
            // Convert 1 unit of item 1 (5kg bag) into 5000g of item 2 loose (but track under same item for simplicity)
            // Use same item id 1 for both source and target to keep it simple; ledger records both rows.
            // Actually source item and target item can differ; here we only assert ledger balances.
            stock.RecordConversion(1, 11, 12, unitsRemoved: 1, gramsAdded: 5000, userId: 1);

            using (var c = _db.Open())
            {
                long srcUnits = c.ExecuteScalar<long>("SELECT qty_units FROM batches WHERE id=11");
                long tgtGrams = c.ExecuteScalar<long>("SELECT qty_grams FROM batches WHERE id=12");
                Assert.Equal(1, srcUnits);
                Assert.Equal(5000, tgtGrams);
                int rows = c.ExecuteScalar<int>("SELECT COUNT(*) FROM stock_ledger WHERE reason='conversion'");
                Assert.Equal(2, rows);
            }
        }

        [Fact]
        public void DuplicateSupplierInvoiceRejected()
        {
            using (var c = _db.Open())
                c.Execute("INSERT INTO suppliers(id,name) VALUES(1,'S')");
            var pr = new PurchaseRepository(_db, _audit);
            var p1 = new Purchase { SupplierId = 1, InvoiceNo = "INV1", InvoiceDate = DateTime.Today, Lines = new List<PurchaseLine>() };
            pr.Save(p1, 1);
            var p2 = new Purchase { SupplierId = 1, InvoiceNo = "INV1", InvoiceDate = DateTime.Today, Lines = new List<PurchaseLine>() };
            Assert.Throws<InvalidOperationException>(() => pr.Save(p2, 1));
        }

        [Fact]
        public void ClosedShiftBlocksPettyCash()
        {
            var sh = new ShiftRepository(_db, _audit);
            var s = sh.Open(1, 1, 100000);
            sh.Close(s.Id, new List<Tuple<long,int>>(), 1);
            Assert.Throws<InvalidOperationException>(() => sh.RecordPettyCash(s.Id, 100, "note", 1));
        }

        [Fact]
        public void CustomerCanBeCreatedWithoutNameForLoyaltyOnly()
        {
            var repo = new CustomerRepository(_db, _audit);
            long id = repo.Create(new Customer { Phone = "9876543210", CreditAllowed = false, IsActive = true }, 1);
            Assert.True(id > 0);
            var found = repo.FindByPhone("9876543210");
            Assert.NotNull(found);
            Assert.True(string.IsNullOrEmpty(found.Name));
        }

        [Fact]
        public void CreditCustomerCreateRequiresName()
        {
            var repo = new CustomerRepository(_db, _audit);
            Assert.Throws<ArgumentException>(() =>
                repo.Create(new Customer { Phone = "9000000000", CreditAllowed = true, IsActive = true }, 1));
        }

        [Fact]
        public void CustomerCreateRequiresPhone()
        {
            var repo = new CustomerRepository(_db, _audit);
            Assert.Throws<ArgumentException>(() =>
                repo.Create(new Customer { Name = "Nameless", IsActive = true }, 1));
        }

        [Fact]
        public void EnablingCreditOnNamelessCustomerIsBlocked()
        {
            var repo = new CustomerRepository(_db, _audit);
            long id = repo.Create(new Customer { Phone = "9111111111", IsActive = true }, 1);
            Assert.Throws<InvalidOperationException>(() => repo.SetCreditAllowed(id, true, 1));
        }
    }
}
