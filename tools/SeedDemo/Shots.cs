using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using GroceryPos.Data;
using GroceryPos.Domain;
using GroceryPos.Printing;
using AppCtx = GroceryPos.App.AppContext;

namespace SeedDemo
{
    /// <summary>
    /// Renders each screen to a PNG so the layout can be reviewed without
    /// clicking through the app. Developer tool; not shipped.
    ///
    /// Run with:  SeedDemo.exe shots &lt;output directory&gt;
    /// </summary>
    internal static class Shots
    {
        public static int Run(string outDir)
        {
            Directory.CreateDirectory(outDir);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var dbDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GroceryPos");
            var db = new Db(Path.Combine(dbDir, "grocery.sqlite"));
            new Migrator(db).Migrate();

            var ctx = new AppCtx
            {
                Db = db,
                Users = new UserRepository(db),
                Settings = new SettingsRepository(db),
                Audit = new AuditLog(db),
                Categories = new CategoryRepository(db),
                Suppliers = new SupplierRepository(db)
            };
            ctx.Items = new ItemRepository(db, ctx.Audit);
            ctx.Bills = new BillRepository(db, ctx.Audit);
            ctx.Customers = new CustomerRepository(db, ctx.Audit);
            ctx.CustomerLedger = new CustomerLedgerRepository(db, ctx.Audit);
            ctx.CreditLimits = new CreditLimitRepository(db);
            ctx.CreditPayments = new CreditPaymentRepository(db, ctx.Audit);
            ctx.Batches = new BatchRepository(db);
            ctx.StockLedger = new StockLedgerRepository(db, ctx.Audit);
            ctx.Purchases = new PurchaseRepository(db, ctx.Audit);
            ctx.Shifts = new ShiftRepository(db, ctx.Audit);
            ctx.Printer = new WindowsRawPrinter();
            ctx.CurrentUser = ctx.Users.All().First();

            Capture(outDir, "purchase_entry", () => new GroceryPos.App.PurchaseEntryForm(ctx));
            Capture(outDir, "stock_take", () => new GroceryPos.App.StockTakeForm(ctx));
            Capture(outDir, "stock_summary", () => new GroceryPos.App.StockSummaryForm(ctx));
            Capture(outDir, "item_master", () => new GroceryPos.App.ItemMasterForm(ctx));
            Capture(outDir, "wastage", () => new GroceryPos.App.WastageForm(ctx));
            Capture(outDir, "unit_conversion", () => new GroceryPos.App.UnitConversionForm(ctx));
            Capture(outDir, "purchase_return", () => new GroceryPos.App.PurchaseReturnForm(ctx));
            Capture(outDir, "near_expiry", () => new GroceryPos.App.NearExpiryReportForm(ctx));
            Capture(outDir, "reorder", () => new GroceryPos.App.ReorderReportForm(ctx));
            Capture(outDir, "billing", () => new GroceryPos.App.BillingForm(ctx));
            Capture(outDir, "shift", () => new GroceryPos.App.ShiftForm(ctx));
            Capture(outDir, "settings", () => new GroceryPos.App.SettingsForm(ctx));
            Capture(outDir, "users", () => new GroceryPos.App.UsersForm(ctx));
            Capture(outDir, "customer_khata", () => new GroceryPos.App.CustomerLedgerForm(ctx));

            // A brand new WEIGHT item, to check the unit and price caption follow
            // the "Sold by" choice.
            Capture(outDir, "item_new_weight", () =>
                new GroceryPos.App.ItemEditForm(ctx, new Item
                {
                    SoldBy = SoldBy.Weight, Unit = "pc", IsActive = true,
                    RoundToGrams = 5, MinSaleGrams = 100, AllowDiscount = true
                }));
            Capture(outDir, "reports_menu", () => new GroceryPos.App.ReportsMenuForm(ctx));
            Capture(outDir, "dashboard", () => new GroceryPos.App.DashboardForm(ctx));
            Capture(outDir, "remove_qty", () =>
                new GroceryPos.App.RemoveQuantityDialog("Tata Salt 1kg", 5));
            Capture(outDir, "item_new_piece", () =>
                new GroceryPos.App.ItemEditForm(ctx, new Item
                {
                    SoldBy = SoldBy.Piece, Unit = "pc", IsActive = true,
                    RoundToGrams = 5, MinSaleGrams = 100, AllowDiscount = true
                }));

            Console.WriteLine("Screens written to " + outDir);
            return 0;
        }

        private static void Capture(string dir, string name, Func<Form> build)
        {
            try
            {
                using (var f = build())
                {
                    f.StartPosition = FormStartPosition.Manual;
                    f.Location = new Point(0, 0);
                    f.ShowInTaskbar = false;
                    f.Show();

                    // Let Load handlers, data binding and layout settle.
                    for (int i = 0; i < 8; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(60); }
                    f.PerformLayout();
                    f.Refresh();
                    Application.DoEvents();

                    using (var bmp = new Bitmap(f.Width, f.Height))
                    {
                        f.DrawToBitmap(bmp, new Rectangle(0, 0, f.Width, f.Height));
                        bmp.Save(Path.Combine(dir, name + ".png"),
                                 System.Drawing.Imaging.ImageFormat.Png);
                    }
                    Console.WriteLine("  " + name + "  " + f.Width + "x" + f.Height);
                    f.Close();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("  " + name + "  FAILED: " + ex.Message);
            }
        }
    }
}
