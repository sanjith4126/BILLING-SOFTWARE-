using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Dapper;
using GroceryPos.Data;
using GroceryPos.Domain;
using GroceryPos.Printing;
using Xunit;
using AppCtx = GroceryPos.App.AppContext;

namespace GroceryPos.Tests
{
    /// <summary>
    /// Checks the SQL the application screens embed, against a real database.
    ///
    /// A broken query inside a screen compiles perfectly well and only fails when
    /// a cashier scans something. That is exactly how a syntax error in the batch
    /// lookup reached the counter and stopped every sale. Compiling is not enough.
    /// </summary>
    public class BillingSqlTests : IDisposable
    {
        private readonly string _dbPath;
        private readonly AppCtx _ctx;
        private readonly long _pieceItem;

        public BillingSqlTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), "billsql_" + Guid.NewGuid().ToString("N") + ".sqlite");
            var db = new Db(_dbPath);
            new Migrator(db).Migrate();

            _ctx = new AppCtx
            {
                Db = db,
                Users = new UserRepository(db),
                Settings = new SettingsRepository(db),
                Audit = new AuditLog(db),
                Categories = new CategoryRepository(db),
                Suppliers = new SupplierRepository(db)
            };
            _ctx.Items = new ItemRepository(db, _ctx.Audit);
            _ctx.Bills = new BillRepository(db, _ctx.Audit);
            _ctx.Customers = new CustomerRepository(db, _ctx.Audit);
            _ctx.CustomerLedger = new CustomerLedgerRepository(db, _ctx.Audit);
            _ctx.CreditLimits = new CreditLimitRepository(db);
            _ctx.CreditPayments = new CreditPaymentRepository(db, _ctx.Audit);
            _ctx.Batches = new BatchRepository(db);
            _ctx.StockLedger = new StockLedgerRepository(db, _ctx.Audit);
            _ctx.Purchases = new PurchaseRepository(db, _ctx.Audit);
            _ctx.Shifts = new ShiftRepository(db, _ctx.Audit);
            _ctx.Printer = new WindowsRawPrinter();

            long uid = _ctx.Users.Create("t", "1234", UserRole.Owner);
            _ctx.CurrentUser = _ctx.Users.All().First(u => u.Id == uid);

            _pieceItem = _ctx.Items.Save(new Item
            {
                Sku = "PKT",
                Name = "Salt 1kg",
                PrintName = "Salt 1kg",
                SoldBy = SoldBy.Piece,
                Unit = "pc",
                TaxRateBp = 500,
                HsnCode = "2501",
                RoundToGrams = 5,
                MinSaleGrams = 100,
                IsActive = true,
                AllowDiscount = true,
                DefaultCostPaise = 1900,
                DefaultSellingPaise = 2400,
                DefaultMrpPaise = 2500
            }, _ctx.CurrentUser.Id);

            // Real stock, so the batch lookup has something to find.
            long supplierId = _ctx.Suppliers.Create(new Supplier { Name = "S", PaymentTermsDays = 0 });
            var p = new Purchase
            {
                SupplierId = supplierId,
                InvoiceNo = "B1",
                InvoiceDate = DateTime.Today,
                PaymentMode = "cash",
                GoodsPaise = 95000,
                TotalPaise = 95000
            };
            p.Lines.Add(new PurchaseLine
            {
                ItemId = _pieceItem,
                BatchCode = "P1",
                QtyUnits = 50,
                CostPaise = 1900,
                MrpPaise = 2500,
                ValuePaise = 95000
            });
            _ctx.Purchases.Save(p, _ctx.CurrentUser.Id);
        }

        public void Dispose()
        {
            try { File.Delete(_dbPath); } catch { }
        }

        /// <summary>
        /// The exact query the billing screen runs when an item is added. A syntax
        /// error here stops every sale in the shop, so it is run rather than
        /// merely compiled.
        /// </summary>
        [Fact]
        public void TheBatchLookupUsedOnEverySale_Runs()
        {
            const string batchLookup =
                "SELECT id AS Id, selling_paise AS SellingPaise, mrp_paise AS MrpPaise, " +
                "batch_code AS BatchCode, expiry_date AS ExpiryDate " +
                "FROM batches " +
                "WHERE item_id=@i AND (qty_units>0 OR qty_grams>0) " +
                "ORDER BY (expiry_date IS NULL) ASC, expiry_date ASC, mrp_paise ASC " +
                "LIMIT 1";

            using (var c = _ctx.Db.Open())
            {
                var row = c.QueryFirstOrDefault<dynamic>(batchLookup, new { i = _pieceItem });

                Assert.NotNull(row);
                Assert.Equal("P1", (string)row.BatchCode);
                // A purchase seeds the batch selling price from the MRP on the
                // supplier bill, so this is the MRP rather than the item default.
                Assert.Equal(2500L, (long)row.SellingPaise);
            }
        }

        /// <summary>
        /// Every SQL statement the screens embed must actually compile.
        ///
        /// SQLiteCommand.Prepare() is a no-op in this provider and reports nothing,
        /// so each statement is handed to the engine wrapped in EXPLAIN: that
        /// compiles it and raises syntax errors without reading, writing or
        /// changing a single row.
        /// </summary>
        [Fact]
        public void EverySqlStatementInTheScreens_Compiles()
        {
            string appDir = FindAppSourceDirectory();
            Assert.True(appDir != null, "Could not locate the application source to scan.");

            var statements = new List<Tuple<string, string>>();
            var finder = new Regex(
                "@\"\\s*(SELECT|INSERT|UPDATE|DELETE|WITH)(?:[^\"]|\"\")*\"",
                RegexOptions.IgnoreCase);

            foreach (string file in Directory.GetFiles(appDir, "*.cs"))
            {
                string source = File.ReadAllText(file);
                foreach (Match m in finder.Matches(source))
                {
                    string sql = m.Value.Substring(2, m.Value.Length - 3).Replace("\"\"", "\"");
                    statements.Add(Tuple.Create(Path.GetFileName(file), sql));
                }
            }

            Assert.True(statements.Count > 0,
                "No SQL was found to check, so this test is not proving anything.");

            var failures = new List<string>();
            using (var c = _ctx.Db.Open())
            {
                foreach (var s in statements)
                {
                    try
                    {
                        using (var cmd = c.CreateCommand())
                        {
                            cmd.CommandText = "EXPLAIN " + s.Item2;
                            foreach (string name in ParameterNames(s.Item2))
                            {
                                var prm = cmd.CreateParameter();
                                prm.ParameterName = name;
                                prm.Value = 1;
                                cmd.Parameters.Add(prm);
                            }
                            using (var r = cmd.ExecuteReader()) { /* compiled */ }
                        }
                    }
                    catch (Exception ex)
                    {
                        failures.Add(s.Item1 + ": " + ex.Message + Environment.NewLine +
                                     "    " + Squash(s.Item2));
                    }
                }
            }

            Assert.True(failures.Count == 0,
                "Broken SQL in the application (" + failures.Count + " of " +
                statements.Count + " statements):" + Environment.NewLine +
                string.Join(Environment.NewLine, failures));
        }

        /// <summary>Every @name the statement refers to, so it can be compiled.</summary>
        private static IEnumerable<string> ParameterNames(string sql)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match m in Regex.Matches(sql, "@([A-Za-z_][A-Za-z0-9_]*)"))
            {
                if (seen.Add(m.Groups[1].Value)) yield return "@" + m.Groups[1].Value;
            }
        }

        private static string Squash(string s)
        {
            s = Regex.Replace(s, "\\s+", " ").Trim();
            return s.Length > 140 ? s.Substring(0, 140) + "..." : s;
        }

        /// <summary>Walks up from the test binaries to the App project sources.</summary>
        private static string FindAppSourceDirectory()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null)
            {
                string candidate = Path.Combine(dir.FullName, "src", "GroceryPos.App");
                if (Directory.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
            return null;
        }
    }
}
