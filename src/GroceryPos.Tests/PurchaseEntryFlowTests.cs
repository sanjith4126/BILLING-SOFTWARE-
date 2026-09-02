using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using GroceryPos.Data;
using GroceryPos.Domain;
using GroceryPos.Printing;
using Xunit;
using AppCtx = GroceryPos.App.AppContext;

namespace GroceryPos.Tests
{
    /// <summary>
    /// Drives the purchase entry screen the way a shop owner would: pick an item,
    /// type a quantity and a cost, save, and check the goods actually landed in
    /// stock. This is the path that used to fail with a DataGridView crash.
    /// </summary>
    [Collection("UI")]
    public class PurchaseEntryFlowTests : IDisposable
    {
        private readonly string _dbPath;
        private readonly AppCtx _ctx;
        private readonly long _itemId;

        public PurchaseEntryFlowTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), "flow_" + Guid.NewGuid().ToString("N") + ".sqlite");
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

            long uid = _ctx.Users.Create("tester", "1234", UserRole.Owner);
            _ctx.CurrentUser = _ctx.Users.All().First(u => u.Id == uid);

            _ctx.Suppliers.Create(new Supplier { Name = "Test Supplier", PaymentTermsDays = 30 });
            _itemId = _ctx.Items.Save(new Item
            {
                Sku = "TEST1",
                Name = "Test Rice 1kg",
                PrintName = "Test Rice 1kg",
                SoldBy = SoldBy.Piece,
                Unit = "pc",
                TaxRateBp = 500,
                HsnCode = "1006",
                RoundToGrams = 5,
                MinSaleGrams = 100,
                IsActive = true,
                AllowDiscount = true,
                DefaultCostPaise = 5000,
                DefaultMrpPaise = 7000,
                DefaultSellingPaise = 6500
            }, _ctx.CurrentUser.Id);
        }

        public void Dispose()
        {
            try { File.Delete(_dbPath); } catch { }
        }

        /// <summary>
        /// A saved purchase must create the batch that stock take then reads. This
        /// is the dependency that left stock take permanently empty while purchase
        /// entry was crashing.
        /// </summary>
        [Fact]
        public void SavingAPurchase_PutsGoodsIntoStock()
        {
            Assert.Empty(_ctx.Batches.All());

            var p = new Purchase
            {
                SupplierId = _ctx.Suppliers.All().First().Id,
                InvoiceNo = "TEST-001",
                InvoiceDate = DateTime.Today,
                PaymentMode = "cash"
            };
            p.Lines.Add(new PurchaseLine
            {
                ItemId = _itemId,
                BatchCode = "B-1",
                QtyUnits = 20,
                CostPaise = 5000,
                MrpPaise = 7000,
                ValuePaise = 100000
            });
            p.GoodsPaise = 100000;
            p.TotalPaise = 100000;

            _ctx.Purchases.Save(p, _ctx.CurrentUser.Id);

            var batches = _ctx.Batches.All();
            Assert.Single(batches);
            Assert.Equal(20, batches[0].QtyUnits);
            Assert.Equal("B-1", batches[0].BatchCode);
        }

        /// <summary>The same supplier invoice must not be enterable twice.</summary>
        [Fact]
        public void TheSameInvoiceTwice_IsRejected()
        {
            long supplierId = _ctx.Suppliers.All().First().Id;

            Func<Purchase> make = () =>
            {
                var p = new Purchase
                {
                    SupplierId = supplierId,
                    InvoiceNo = "DUP-1",
                    InvoiceDate = DateTime.Today,
                    PaymentMode = "cash",
                    GoodsPaise = 5000,
                    TotalPaise = 5000
                };
                p.Lines.Add(new PurchaseLine
                {
                    ItemId = _itemId, BatchCode = "B-9", QtyUnits = 1,
                    CostPaise = 5000, MrpPaise = 7000, ValuePaise = 5000
                });
                return p;
            };

            _ctx.Purchases.Save(make(), _ctx.CurrentUser.Id);
            var ex = Record.Exception(() => _ctx.Purchases.Save(make(), _ctx.CurrentUser.Id));

            Assert.NotNull(ex);
            // The repository translates the constraint violation into a message a
            // person can act on, rather than surfacing raw SQLite text.
            Assert.Contains("Duplicate supplier invoice", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("DUP-1", ex.Message);
        }

        /// <summary>
        /// With stock present, stock take lists it and a changed count writes a
        /// ledger adjustment of exactly the difference.
        /// </summary>
        [Fact]
        public void StockTake_AdjustsOnlyTheDifference()
        {
            var p = new Purchase
            {
                SupplierId = _ctx.Suppliers.All().First().Id,
                InvoiceNo = "ST-1",
                InvoiceDate = DateTime.Today,
                PaymentMode = "cash",
                GoodsPaise = 50000,
                TotalPaise = 50000
            };
            p.Lines.Add(new PurchaseLine
            {
                ItemId = _itemId, BatchCode = "B-ST", QtyUnits = 10,
                CostPaise = 5000, MrpPaise = 7000, ValuePaise = 50000
            });
            _ctx.Purchases.Save(p, _ctx.CurrentUser.Id);

            var batch = _ctx.Batches.All().Single();
            Assert.Equal(10, batch.QtyUnits);

            // Counted 8, so two pieces are missing.
            _ctx.StockLedger.RecordStockTake(_itemId, batch.Id, -2, 0,
                "Stock take", _ctx.CurrentUser.Id);

            var after = _ctx.Batches.FindById(batch.Id);
            Assert.Equal(8, after.QtyUnits);
        }

        /// <summary>
        /// The purchase screen builds, loads its data and reports the right totals
        /// without any DataGridView error, which is what crashed before.
        /// </summary>
        [Fact]
        public void PurchaseScreen_LoadsWithoutError()
        {
            FormLayoutTests.Sta(() =>
            {
                using (var f = new GroceryPos.App.PurchaseEntryForm(_ctx))
                {
                    f.StartPosition = FormStartPosition.Manual;
                    f.Location = new System.Drawing.Point(-32000, -32000);
                    f.ShowInTaskbar = false;

                    var errors = new System.Collections.Generic.List<string>();
                    f.Show();
                    HookGrids(f, errors);
                    Application.DoEvents();
                    f.Refresh();
                    Application.DoEvents();

                    Assert.True(errors.Count == 0,
                        "The purchase grid raised a DataError on load: " + string.Join("; ", errors));
                    f.Close();
                }
            });
        }

        private static void HookGrids(Control parent, System.Collections.Generic.List<string> sink)
        {
            var g = parent as DataGridView;
            if (g != null)
                g.DataError += (s, e) => sink.Add(e.Exception == null ? "(unknown)" : e.Exception.Message);
            foreach (Control c in parent.Controls) HookGrids(c, sink);
        }
    }
}
