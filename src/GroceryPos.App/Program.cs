using System;
using System.IO;
using System.Windows.Forms;
using GroceryPos.Data;
using GroceryPos.Hardware;
using GroceryPos.Printing;

namespace GroceryPos.App
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var dbDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GroceryPos");
            Directory.CreateDirectory(dbDir);
            var dbPath = Path.Combine(dbDir, "grocery.sqlite");

            var db = new Db(dbPath);
            new Migrator(db).Migrate();

            var ctx = new AppContext
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
            ctx.RebuildWeightSource();

            // Seed default owner if empty
            if (ctx.Users.All().Count == 0)
            {
                ctx.Users.Create("owner", "1234", GroceryPos.Domain.UserRole.Owner);
            }

            using (var login = new LoginForm(ctx))
            {
                if (login.ShowDialog() != DialogResult.OK) return;
            }
            Application.Run(new MainMenuForm(ctx));
        }
    }
}
