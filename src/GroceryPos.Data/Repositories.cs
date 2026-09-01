using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Dapper;
using GroceryPos.Domain;
using Newtonsoft.Json;

namespace GroceryPos.Data
{
    public class UserRepository
    {
        private readonly Db _db;
        public UserRepository(Db db) { _db = db; }

        public User FindByName(string name)
        {
            using (var c = _db.Open())
            {
                var row = c.QueryFirstOrDefault<dynamic>(
                    "SELECT id, name, pin_hash, role, is_active FROM users WHERE name=@n AND is_active=1",
                    new { n = name });
                return row == null ? null : Map(row);
            }
        }

        public User FindById(long id)
        {
            using (var c = _db.Open())
            {
                var row = c.QueryFirstOrDefault<dynamic>(
                    "SELECT id, name, pin_hash, role, is_active FROM users WHERE id=@i", new { i = id });
                return row == null ? null : Map(row);
            }
        }

        public IList<User> All()
        {
            using (var c = _db.Open())
            {
                var rows = c.Query<dynamic>(
                    "SELECT id, name, pin_hash, role, is_active FROM users ORDER BY name");
                var list = new List<User>();
                foreach (var r in rows) list.Add(Map(r));
                return list;
            }
        }

        public long Create(string name, string pin, UserRole role)
        {
            using (var c = _db.Open())
            {
                return c.ExecuteScalar<long>(
                    "INSERT INTO users(name, pin_hash, role) VALUES(@n,@h,@r); SELECT last_insert_rowid();",
                    new { n = name, h = PinHasher.Hash(pin), r = role.ToString().ToLowerInvariant() });
            }
        }

        public bool VerifyPin(User u, string pin) { return PinHasher.Verify(pin, u.PinHash); }

        public void SetPin(long userId, string newPin)
        {
            using (var c = _db.Open())
                c.Execute("UPDATE users SET pin_hash=@h WHERE id=@i",
                    new { h = PinHasher.Hash(newPin), i = userId });
        }

        public void SetActive(long userId, bool active)
        {
            using (var c = _db.Open())
                c.Execute("UPDATE users SET is_active=@a WHERE id=@i",
                    new { a = active ? 1 : 0, i = userId });
        }

        public void SetRole(long userId, UserRole role)
        {
            using (var c = _db.Open())
                c.Execute("UPDATE users SET role=@r WHERE id=@i",
                    new { r = role.ToString().ToLowerInvariant(), i = userId });
        }

        private static User Map(dynamic r)
        {
            return new User
            {
                Id = (long)r.id,
                Name = (string)r.name,
                PinHash = (string)r.pin_hash,
                Role = ParseRole((string)r.role),
                IsActive = ((long)r.is_active) != 0
            };
        }

        private static UserRole ParseRole(string s)
        {
            switch (s)
            {
                case "owner": return UserRole.Owner;
                case "manager": return UserRole.Manager;
                default: return UserRole.Cashier;
            }
        }
    }

    public class SettingsRepository
    {
        private readonly Db _db;
        public SettingsRepository(Db db) { _db = db; }

        public string Get(string key, string def = null)
        {
            using (var c = _db.Open())
            {
                var v = c.QueryFirstOrDefault<string>(
                    "SELECT value FROM settings WHERE key=@k", new { k = key });
                return v ?? def;
            }
        }

        public void Set(string key, string value)
        {
            using (var c = _db.Open())
            {
                c.Execute(@"INSERT INTO settings(key,value) VALUES(@k,@v)
                            ON CONFLICT(key) DO UPDATE SET value=excluded.value",
                    new { k = key, v = value });
            }
        }

        public Dictionary<string, string> GetAll()
        {
            using (var c = _db.Open())
            {
                return c.Query<(string Key, string Value)>("SELECT key, value FROM settings")
                    .ToDictionary(r => r.Key, r => r.Value);
            }
        }
    }

    public class AuditLog
    {
        private readonly Db _db;
        public AuditLog(Db db) { _db = db; }

        public void Write(long userId, string action, string entity, long? entityId,
                          object before = null, object after = null)
        {
            using (var c = _db.Open())
            {
                WriteWithConnection(c, null, userId, action, entity, entityId, before, after);
            }
        }

        public void WriteWithConnection(IDbConnection c, IDbTransaction tx,
            long userId, string action, string entity, long? entityId, object before, object after)
        {
            c.Execute(@"INSERT INTO audit_log(user_id, action, entity, entity_id, before_json, after_json)
                        VALUES(@u,@a,@e,@i,@b,@af)",
                new
                {
                    u = userId,
                    a = action,
                    e = entity,
                    i = entityId,
                    b = before == null ? null : JsonConvert.SerializeObject(before),
                    af = after == null ? null : JsonConvert.SerializeObject(after)
                }, transaction: tx);
        }
    }

    public class CategoryRepository
    {
        private readonly Db _db;
        public CategoryRepository(Db db) { _db = db; }

        public IList<Category> All()
        {
            using (var c = _db.Open())
                return c.Query<Category>("SELECT id, name FROM categories ORDER BY name").ToList();
        }

        public long CreateIfMissing(string name)
        {
            using (var c = _db.Open())
            {
                var id = c.QueryFirstOrDefault<long?>("SELECT id FROM categories WHERE name=@n", new { n = name });
                if (id.HasValue) return id.Value;
                return c.ExecuteScalar<long>(
                    "INSERT INTO categories(name) VALUES(@n); SELECT last_insert_rowid();", new { n = name });
            }
        }
    }

    public class SupplierRepository
    {
        private readonly Db _db;
        public SupplierRepository(Db db) { _db = db; }

        public IList<Supplier> All()
        {
            using (var c = _db.Open())
                return c.Query<Supplier>(@"SELECT id, name, phone, gstin, address,
                    payment_terms_days AS PaymentTermsDays FROM suppliers ORDER BY name").ToList();
        }

        public long Create(Supplier s)
        {
            using (var c = _db.Open())
                return c.ExecuteScalar<long>(@"INSERT INTO suppliers(name, phone, gstin, address, payment_terms_days)
                    VALUES(@Name,@Phone,@Gstin,@Address,@PaymentTermsDays); SELECT last_insert_rowid();", s);
        }
    }

    public class ItemRepository
    {
        private readonly Db _db;
        private readonly AuditLog _audit;
        public ItemRepository(Db db, AuditLog audit) { _db = db; _audit = audit; }

        private const string SelectCols = @"id, sku, name, print_name AS PrintName,
            category_id AS CategoryId, brand, rack, sold_by AS SoldByRaw, unit,
            tax_rate_bp AS TaxRateBp, hsn_code AS HsnCode,
            reorder_level AS ReorderLevel, max_level AS MaxLevel,
            default_supplier_id AS DefaultSupplierId,
            track_batch AS TrackBatch, track_expiry AS TrackExpiry,
            allow_discount AS AllowDiscount, weigh_at_counter AS WeighAtCounter,
            tare_grams AS TareGrams, round_to_grams AS RoundToGrams,
            min_sale_grams AS MinSaleGrams, is_active AS IsActive";

        // Use a raw dynamic and map to work around sold_by enum
        public Item FindById(long id)
        {
            using (var c = _db.Open())
            {
                var r = c.QueryFirstOrDefault<dynamic>(
                    "SELECT " + SelectCols + " FROM items WHERE id=@i", new { i = id });
                return r == null ? null : Map(r);
            }
        }

        public Item FindBySku(string sku)
        {
            using (var c = _db.Open())
            {
                var r = c.QueryFirstOrDefault<dynamic>(
                    "SELECT " + SelectCols + " FROM items WHERE sku=@s", new { s = sku });
                return r == null ? null : Map(r);
            }
        }

        public Item FindByBarcode(string barcode)
        {
            using (var c = _db.Open())
            {
                var r = c.QueryFirstOrDefault<dynamic>(
                    "SELECT " + SelectCols.Replace("id,", "i.id,") +
                    " FROM items i JOIN item_barcodes b ON b.item_id=i.id WHERE b.barcode=@bc",
                    new { bc = barcode });
                return r == null ? null : Map(r);
            }
        }

        public IList<Item> Search(string term, int limit = 50)
        {
            using (var c = _db.Open())
            {
                var like = "%" + (term ?? "") + "%";
                var rows = c.Query<dynamic>(
                    "SELECT " + SelectCols + " FROM items WHERE is_active=1 AND (name LIKE @t OR print_name LIKE @t OR sku LIKE @t) ORDER BY name LIMIT @l",
                    new { t = like, l = limit });
                return rows.Select(r => (Item)Map(r)).ToList();
            }
        }

        public long Save(Item it, long userId)
        {
            if (it.SellingExceedsMrp())
                throw new InvalidOperationException("Selling price exceeds MRP");
            using (var c = _db.Open())
            using (var tx = c.BeginTransaction())
            {
                long id;
                if (it.Id == 0)
                {
                    id = c.ExecuteScalar<long>(@"
                        INSERT INTO items(sku, name, print_name, category_id, brand, rack, sold_by, unit,
                          tax_rate_bp, hsn_code, reorder_level, max_level, default_supplier_id,
                          track_batch, track_expiry, allow_discount, weigh_at_counter,
                          tare_grams, round_to_grams, min_sale_grams, is_active)
                        VALUES(@Sku,@Name,@PrintName,@CategoryId,@Brand,@Rack,@SoldBy,@Unit,
                          @TaxRateBp,@HsnCode,@ReorderLevel,@MaxLevel,@DefaultSupplierId,
                          @TrackBatch,@TrackExpiry,@AllowDiscount,@WeighAtCounter,
                          @TareGrams,@RoundToGrams,@MinSaleGrams,@IsActive);
                        SELECT last_insert_rowid();",
                        BindParams(it), transaction: tx);
                    _audit.WriteWithConnection(c, tx, userId, "create", "item", id, null, it);
                }
                else
                {
                    var before = c.QueryFirstOrDefault<dynamic>("SELECT " + SelectCols + " FROM items WHERE id=@i",
                        new { i = it.Id }, transaction: tx);
                    id = it.Id;
                    c.Execute(@"UPDATE items SET
                        sku=@Sku, name=@Name, print_name=@PrintName, category_id=@CategoryId,
                        brand=@Brand, rack=@Rack, sold_by=@SoldBy, unit=@Unit,
                        tax_rate_bp=@TaxRateBp, hsn_code=@HsnCode,
                        reorder_level=@ReorderLevel, max_level=@MaxLevel,
                        default_supplier_id=@DefaultSupplierId,
                        track_batch=@TrackBatch, track_expiry=@TrackExpiry,
                        allow_discount=@AllowDiscount, weigh_at_counter=@WeighAtCounter,
                        tare_grams=@TareGrams, round_to_grams=@RoundToGrams,
                        min_sale_grams=@MinSaleGrams, is_active=@IsActive,
                        updated_at=datetime('now') WHERE id=@Id",
                        BindParams(it, id), transaction: tx);
                    _audit.WriteWithConnection(c, tx, userId, "update", "item", id, before, it);
                }
                tx.Commit();
                return id;
            }
        }

        public void AddBarcode(long itemId, string barcode, bool isPrimary)
        {
            using (var c = _db.Open())
            {
                c.Execute(@"INSERT INTO item_barcodes(item_id, barcode, is_primary)
                            VALUES(@i,@b,@p)", new { i = itemId, b = barcode, p = isPrimary ? 1 : 0 });
            }
        }

        public IList<string> BarcodesFor(long itemId)
        {
            using (var c = _db.Open())
                return c.Query<string>("SELECT barcode FROM item_barcodes WHERE item_id=@i", new { i = itemId }).ToList();
        }

        private static object BindParams(Item it, long? id = null)
        {
            return new
            {
                Id = id ?? it.Id,
                it.Sku,
                it.Name,
                it.PrintName,
                it.CategoryId,
                it.Brand,
                it.Rack,
                SoldBy = it.SoldBy.ToString().ToLowerInvariant(),
                it.Unit,
                it.TaxRateBp,
                it.HsnCode,
                it.ReorderLevel,
                it.MaxLevel,
                it.DefaultSupplierId,
                TrackBatch = it.TrackBatch ? 1 : 0,
                TrackExpiry = it.TrackExpiry ? 1 : 0,
                AllowDiscount = it.AllowDiscount ? 1 : 0,
                WeighAtCounter = it.WeighAtCounter ? 1 : 0,
                it.TareGrams,
                it.RoundToGrams,
                it.MinSaleGrams,
                IsActive = it.IsActive ? 1 : 0
            };
        }

        private static Item Map(dynamic r)
        {
            SoldBy sb;
            var raw = (string)r.SoldByRaw;
            switch (raw)
            {
                case "weight": sb = SoldBy.Weight; break;
                case "volume": sb = SoldBy.Volume; break;
                default: sb = SoldBy.Piece; break;
            }
            return new Item
            {
                Id = (long)r.id,
                Sku = (string)r.sku,
                Name = (string)r.name,
                PrintName = (string)r.PrintName,
                CategoryId = (long?)r.CategoryId,
                Brand = (string)r.brand,
                Rack = (string)r.rack,
                SoldBy = sb,
                Unit = (string)r.unit,
                TaxRateBp = (int)(long)r.TaxRateBp,
                HsnCode = (string)r.HsnCode,
                ReorderLevel = (int)(long)r.ReorderLevel,
                MaxLevel = (int)(long)r.MaxLevel,
                DefaultSupplierId = (long?)r.DefaultSupplierId,
                TrackBatch = ((long)r.TrackBatch) != 0,
                TrackExpiry = ((long)r.TrackExpiry) != 0,
                AllowDiscount = ((long)r.AllowDiscount) != 0,
                WeighAtCounter = ((long)r.WeighAtCounter) != 0,
                TareGrams = (int)(long)r.TareGrams,
                RoundToGrams = (int)(long)r.RoundToGrams,
                MinSaleGrams = (int)(long)r.MinSaleGrams,
                IsActive = ((long)r.IsActive) != 0
            };
        }
    }

    public static class ItemGuards
    {
        // We store MRP on batches; here we allow saving item without an MRP check
        // (batch save enforces MRP >= selling elsewhere).
        public static bool SellingExceedsMrp(this Item it) { return false; }
    }
}
