using System;
using System.IO;
using System.Linq;
using GroceryPos.Data;
using GroceryPos.Domain;

namespace SeedDemo
{
    /// <summary>
    /// Fills the local database with a handful of realistic products, a supplier
    /// and one received purchase, so the screens can be reviewed with content in
    /// them. Safe to re-run: it does nothing if items already exist.
    ///
    /// This is a developer tool, not part of the shipped application.
    /// </summary>
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length >= 1 && args[0] == "shots")
                return Shots.Run(args.Length >= 2 ? args[1] : ".");

            var dbDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GroceryPos");
            Directory.CreateDirectory(dbDir);
            var db = new Db(Path.Combine(dbDir, "grocery.sqlite"));
            new Migrator(db).Migrate();

            var audit = new AuditLog(db);
            var users = new UserRepository(db);
            var items = new ItemRepository(db, audit);
            var suppliers = new SupplierRepository(db);
            var purchases = new PurchaseRepository(db, audit);

            var owner = users.All().FirstOrDefault();
            if (owner == null)
            {
                Console.WriteLine("No users; run the app once first.");
                return 1;
            }

            if (items.Search("").Count > 0)
            {
                Console.WriteLine("Items already present; nothing seeded.");
                return 0;
            }

            long supplierId = suppliers.Create(new Supplier
            {
                Name = "Agrawal Distributors",
                Phone = "9880012345",
                Gstin = "29ABCDE1234F1Z5",
                Address = "Gandhi Bazaar, Bengaluru",
                PaymentTermsDays = 30
            });

            long atta = NewItem(items, owner.Id, "ATTA5", "Aashirvaad Atta 5kg", SoldBy.Piece, "pc",
                500, "1101", 18550, 21000, 22000, reorder: 10);
            long salt = NewItem(items, owner.Id, "SALT1", "Tata Salt 1kg", SoldBy.Piece, "pc",
                500, "2501", 1900, 2400, 2500, reorder: 20);
            long sugar = NewItem(items, owner.Id, "SUGARL", "Sugar loose", SoldBy.Weight, "kg",
                500, "1701", 4000, 4500, 0, reorder: 5, weigh: true);
            long maggi = NewItem(items, owner.Id, "MAGGI", "Maggi Noodles 70g", SoldBy.Piece, "pc",
                1200, "1902", 1100, 1400, 1400, reorder: 30);
            long tomato = NewItem(items, owner.Id, "TOMATO", "Tomato loose", SoldBy.Weight, "kg",
                0, "0702", 2500, 4000, 0, reorder: 3, weigh: true);

            items.AddBarcode(atta, "8901030865278", true);
            items.AddBarcode(salt, "8901030823459", true);
            items.AddBarcode(maggi, "8901030710148", true);

            // One received invoice, which is what creates the batches that stock
            // take and the stock summary read from.
            var p = new Purchase
            {
                SupplierId = supplierId,
                InvoiceNo = "INV-2023-8942",
                InvoiceDate = DateTime.Today.AddDays(-3),
                FreightPaise = 15000,
                DiscountPaise = 0,
                PaymentMode = "credit",
                DueDate = DateTime.Today.AddDays(27)
            };
            AddLine(p, atta, "B-4029", DateTime.Today.AddMonths(9), 45, 0, 18550, 22000);
            AddLine(p, salt, "TS-992", DateTime.Today.AddMonths(18), 120, 0, 1900, 2500);
            AddLine(p, sugar, "SG-01", null, 0, 48000, 4000, 0);
            AddLine(p, maggi, "MG-045", DateTime.Today.AddDays(18), 86, 0, 1100, 1400);
            AddLine(p, tomato, "TM-01", null, 0, 12500, 2500, 0);

            p.GoodsPaise = p.Lines.Sum(l => l.ValuePaise);
            p.TotalPaise = p.GoodsPaise + p.FreightPaise - p.DiscountPaise;
            long purchaseId = purchases.Save(p, owner.Id);

            Console.WriteLine("Seeded 5 items and purchase #" + purchaseId +
                              " (total Rs. " + new Money(p.TotalPaise) + ").");
            return 0;
        }

        private static long NewItem(ItemRepository items, long userId, string sku, string name,
            SoldBy soldBy, string unit, int taxBp, string hsn,
            long cost, long selling, long mrp, int reorder, bool weigh = false)
        {
            return items.Save(new Item
            {
                Sku = sku,
                Name = name,
                PrintName = name,
                SoldBy = soldBy,
                Unit = unit,
                TaxRateBp = taxBp,
                HsnCode = hsn,
                ReorderLevel = reorder,
                MaxLevel = reorder * 10,
                TrackBatch = true,
                TrackExpiry = soldBy == SoldBy.Piece,
                AllowDiscount = true,
                WeighAtCounter = weigh,
                TareGrams = 0,
                RoundToGrams = 5,
                MinSaleGrams = 100,
                IsActive = true,
                DefaultCostPaise = cost,
                DefaultSellingPaise = selling,
                DefaultMrpPaise = mrp
            }, userId);
        }

        private static void AddLine(Purchase p, long itemId, string batch, DateTime? expiry,
            int units, int grams, long cost, long mrp)
        {
            decimal qty = units > 0 ? units : grams / 1000m;
            p.Lines.Add(new PurchaseLine
            {
                ItemId = itemId,
                BatchCode = batch,
                ExpiryDate = expiry,
                QtyUnits = units,
                QtyGrams = grams,
                CostPaise = cost,
                MrpPaise = mrp,
                ValuePaise = (long)Math.Round(cost * qty, MidpointRounding.AwayFromZero)
            });
        }
    }
}
