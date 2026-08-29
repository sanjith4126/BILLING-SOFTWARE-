using System;
using System.Data;
using System.Data.SQLite;
using System.IO;
using Dapper;
using GroceryPos.Data;
using GroceryPos.Domain;
using Xunit;

namespace GroceryPos.Tests
{
    public class LedgerTriggerTests : IDisposable
    {
        private readonly string _path;
        private readonly Db _db;

        public LedgerTriggerTests()
        {
            _path = Path.Combine(Path.GetTempPath(), "gpos_test_" + Guid.NewGuid().ToString("N") + ".sqlite");
            _db = new Db(_path);
            new Migrator(_db).Migrate();
            using (var c = _db.Open())
            {
                // Seed a user
                c.Execute("INSERT INTO users(id,name,pin_hash,role) VALUES(1,'test','x','owner')");
                c.Execute("INSERT INTO customers(id,name,phone) VALUES(1,'C','1')");
            }
        }

        public void Dispose()
        {
            SQLiteConnection.ClearAllPools();
            try { File.Delete(_path); } catch { }
        }

        [Fact]
        public void CustomerLedgerRejectsUpdate()
        {
            using (var c = _db.Open())
            {
                c.Execute(@"INSERT INTO customer_ledger(customer_id,at,type,debit_paise,credit_paise,balance_paise,user_id,created_at)
                            VALUES(1,datetime('now'),'opening',10000,0,10000,1,datetime('now'))");
                var ex = Assert.ThrowsAny<Exception>(() =>
                    c.Execute("UPDATE customer_ledger SET balance_paise=0 WHERE customer_id=1"));
                Assert.Contains("append-only", ex.Message);
            }
        }

        [Fact]
        public void CustomerLedgerRejectsDelete()
        {
            using (var c = _db.Open())
            {
                c.Execute(@"INSERT INTO customer_ledger(customer_id,at,type,debit_paise,credit_paise,balance_paise,user_id,created_at)
                            VALUES(1,datetime('now'),'opening',10000,0,10000,1,datetime('now'))");
                Assert.ThrowsAny<Exception>(() =>
                    c.Execute("DELETE FROM customer_ledger WHERE customer_id=1"));
            }
        }

        [Fact]
        public void StockLedgerRejectsUpdateAndDelete()
        {
            using (var c = _db.Open())
            {
                c.Execute("INSERT INTO items(id,name,print_name,sold_by,unit) VALUES(1,'a','a','piece','pc')");
                c.Execute(@"INSERT INTO stock_ledger(item_id,change_units,reason,user_id)
                            VALUES(1,5,'purchase',1)");
                Assert.ThrowsAny<Exception>(() =>
                    c.Execute("UPDATE stock_ledger SET change_units=0 WHERE item_id=1"));
                Assert.ThrowsAny<Exception>(() =>
                    c.Execute("DELETE FROM stock_ledger WHERE item_id=1"));
            }
        }

        [Fact]
        public void BillsRejectDelete()
        {
            using (var c = _db.Open())
            {
                c.Execute("INSERT INTO bills(bill_no,user_id) VALUES(1,1)");
                Assert.ThrowsAny<Exception>(() =>
                    c.Execute("DELETE FROM bills WHERE bill_no=1"));
            }
        }
    }
}
