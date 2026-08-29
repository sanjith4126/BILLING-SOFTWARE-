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
    public class BillRepositoryTests : IDisposable
    {
        private readonly string _path;
        private readonly Db _db;
        private readonly BillRepository _bills;
        private readonly AuditLog _audit;

        public BillRepositoryTests()
        {
            _path = Path.Combine(Path.GetTempPath(), "gpos_bills_" + Guid.NewGuid().ToString("N") + ".sqlite");
            _db = new Db(_path);
            new Migrator(_db).Migrate();
            _audit = new AuditLog(_db);
            _bills = new BillRepository(_db, _audit);
            using (var c = _db.Open())
            {
                c.Execute("INSERT INTO users(id,name,pin_hash,role) VALUES(1,'test','x','cashier')");
                c.Execute("INSERT INTO items(id,name,print_name,sold_by,unit,tax_rate_bp) VALUES(1,'apple','apple','piece','pc',0)");
                c.Execute(@"INSERT INTO batches(id,item_id,batch_code,cost_paise,mrp_paise,selling_paise,qty_units,qty_grams)
                            VALUES(10,1,'B1',5000,10000,10000,100,0)");
            }
        }

        public void Dispose()
        {
            SQLiteConnection.ClearAllPools();
            try { File.Delete(_path); } catch { }
        }

        private Bill MakeBill(int qty = 2)
        {
            return new Bill
            {
                Lines = new List<BillLine>
                {
                    new BillLine { ItemId = 1, BatchId = 10, QtyUnits = qty, RatePaise = 10000, TaxRateBp = 0 }
                }
            };
        }

        [Fact]
        public void BillNumbersAreSequential()
        {
            var b1 = MakeBill(); Domain.BillCalculator.ComputeBill(b1);
            _bills.Save(b1, new List<Payment> { new Payment { Mode = PaymentMode.Cash, AmountPaise = b1.NetPaise } }, 1, 1, null, null, 0);
            var b2 = MakeBill(); Domain.BillCalculator.ComputeBill(b2);
            _bills.Save(b2, new List<Payment> { new Payment { Mode = PaymentMode.Cash, AmountPaise = b2.NetPaise } }, 1, 1, null, null, 0);
            Assert.Equal(b1.BillNo + 1, b2.BillNo);
        }

        [Fact]
        public void CancellationReversesStockAndKeepsBillNo()
        {
            var b = MakeBill(3);
            Domain.BillCalculator.ComputeBill(b);
            _bills.Save(b, new List<Payment> { new Payment { Mode = PaymentMode.Cash, AmountPaise = b.NetPaise } }, 1, 1, null, null, 0);

            using (var c = _db.Open())
            {
                var qty = c.ExecuteScalar<long>("SELECT qty_units FROM batches WHERE id=10");
                Assert.Equal(97, qty);
            }

            long billNo = b.BillNo;
            _bills.Cancel(b.Id, 1, "test cancel");

            using (var c = _db.Open())
            {
                var qty = c.ExecuteScalar<long>("SELECT qty_units FROM batches WHERE id=10");
                Assert.Equal(100, qty);
                var status = c.ExecuteScalar<string>("SELECT status FROM bills WHERE id=@i", new { i = b.Id });
                Assert.Equal("cancelled", status);
                var preserved = c.ExecuteScalar<long>("SELECT bill_no FROM bills WHERE id=@i", new { i = b.Id });
                Assert.Equal(billNo, preserved);
            }
        }

        [Fact]
        public void SplitPaymentsSumToBillNet()
        {
            var b = MakeBill(5); Domain.BillCalculator.ComputeBill(b);
            long half = b.NetPaise / 2;
            long rest = b.NetPaise - half;
            var pays = new List<Payment>
            {
                new Payment { Mode = PaymentMode.Cash, AmountPaise = half },
                new Payment { Mode = PaymentMode.Upi, AmountPaise = rest, Reference = "ref1" }
            };
            _bills.Save(b, pays, 1, 1, null, null, 0);
            using (var c = _db.Open())
            {
                long sum = c.ExecuteScalar<long>("SELECT SUM(amount_paise) FROM payments WHERE bill_id=@i", new { i = b.Id });
                Assert.Equal(b.NetPaise, sum);
            }
        }
    }
}
