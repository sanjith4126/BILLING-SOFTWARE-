using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Dapper;
using GroceryPos.Domain;

namespace GroceryPos.Data
{
    /// <summary>Batches — cache of quantities. Truth is stock_ledger.</summary>
    public class BatchRepository
    {
        private readonly Db _db;
        public BatchRepository(Db db) { _db = db; }

        private const string Cols = @"id, item_id AS ItemId, batch_code AS BatchCode, expiry_date AS ExpiryDate,
            cost_paise AS CostPaise, mrp_paise AS MrpPaise, selling_paise AS SellingPaise,
            qty_grams AS QtyGrams, qty_units AS QtyUnits, supplier_id AS SupplierId,
            purchase_line_id AS PurchaseLineId, received_at AS ReceivedAt";

        public Batch FindById(long id)
        {
            using (var c = _db.Open())
            {
                var r = c.QueryFirstOrDefault<dynamic>("SELECT " + Cols + " FROM batches WHERE id=@i", new { i = id });
                return r == null ? null : Map(r);
            }
        }

        public IList<Batch> ForItemFifo(long itemId)
        {
            using (var c = _db.Open())
            {
                var rows = c.Query<dynamic>(@"SELECT " + Cols + @" FROM batches
                    WHERE item_id=@i AND (qty_units>0 OR qty_grams>0)
                    ORDER BY (expiry_date IS NULL) ASC, expiry_date ASC, id ASC",
                    new { i = itemId });
                return rows.Select(r => (Batch)Map(r)).ToList();
            }
        }

        public IList<Batch> All()
        {
            using (var c = _db.Open())
            {
                var rows = c.Query<dynamic>("SELECT " + Cols + " FROM batches ORDER BY item_id, expiry_date");
                return rows.Select(r => (Batch)Map(r)).ToList();
            }
        }

        public IList<Batch> NearExpiry(int days)
        {
            using (var c = _db.Open())
            {
                var cutoff = DateTime.Today.AddDays(days).ToString("yyyy-MM-dd");
                var rows = c.Query<dynamic>("SELECT " + Cols + " FROM batches WHERE expiry_date IS NOT NULL AND expiry_date <= @d AND (qty_units>0 OR qty_grams>0) ORDER BY expiry_date",
                    new { d = cutoff });
                return rows.Select(r => (Batch)Map(r)).ToList();
            }
        }

        public long StockValuePaise()
        {
            using (var c = _db.Open())
            {
                return c.ExecuteScalar<long>(@"SELECT COALESCE(SUM(cost_paise * qty_units + cost_paise * qty_grams / 1000), 0) FROM batches");
            }
        }

        private static Batch Map(dynamic r)
        {
            return new Batch
            {
                Id = (long)r.id,
                ItemId = (long)r.ItemId,
                BatchCode = (string)r.BatchCode,
                ExpiryDate = r.ExpiryDate == null ? (DateTime?)null : DateTime.Parse((string)r.ExpiryDate),
                CostPaise = (long)r.CostPaise,
                MrpPaise = (long)r.MrpPaise,
                SellingPaise = (long)r.SellingPaise,
                QtyGrams = (int)(long)r.QtyGrams,
                QtyUnits = (int)(long)r.QtyUnits,
                SupplierId = r.SupplierId == null ? (long?)null : (long?)(long)r.SupplierId,
                PurchaseLineId = r.PurchaseLineId == null ? (long?)null : (long?)(long)r.PurchaseLineId,
                ReceivedAt = DateTime.Parse((string)r.ReceivedAt)
            };
        }
    }

    /// <summary>Stock ledger operations (append-only). Never writes batches directly.</summary>
    public class StockLedgerRepository
    {
        private readonly Db _db;
        private readonly AuditLog _audit;
        public StockLedgerRepository(Db db, AuditLog audit) { _db = db; _audit = audit; }

        public void RecordWastage(long itemId, long? batchId, int units, int grams, string reason, long userId)
        {
            if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Reason required");
            using (var c = _db.Open())
            using (var tx = c.BeginTransaction())
            {
                Insert(c, tx, itemId, batchId, -units, -grams, "wastage", "audit_log", null, userId);
                UpdateBatchCache(c, tx, batchId, -units, -grams);
                _audit.WriteWithConnection(c, tx, userId, "wastage", "batch", batchId, null, new { units, grams, reason });
                tx.Commit();
            }
        }

        public void RecordDamage(long itemId, long? batchId, int units, int grams, string reason, long userId)
        {
            if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Reason required");
            using (var c = _db.Open())
            using (var tx = c.BeginTransaction())
            {
                Insert(c, tx, itemId, batchId, -units, -grams, "damage", "audit_log", null, userId);
                UpdateBatchCache(c, tx, batchId, -units, -grams);
                _audit.WriteWithConnection(c, tx, userId, "damage", "batch", batchId, null, new { units, grams, reason });
                tx.Commit();
            }
        }

        public void RecordStockTake(long itemId, long? batchId, int diffUnits, int diffGrams, string reason, long userId)
        {
            using (var c = _db.Open())
            using (var tx = c.BeginTransaction())
            {
                Insert(c, tx, itemId, batchId, diffUnits, diffGrams, "stock_take", "audit_log", null, userId);
                UpdateBatchCache(c, tx, batchId, diffUnits, diffGrams);
                _audit.WriteWithConnection(c, tx, userId, "stock_take", "batch", batchId, null, new { diffUnits, diffGrams, reason });
                tx.Commit();
            }
        }

        public void RecordReturnToSupplier(long itemId, long? batchId, int units, int grams, string reason, long userId)
        {
            using (var c = _db.Open())
            using (var tx = c.BeginTransaction())
            {
                Insert(c, tx, itemId, batchId, -units, -grams, "return_to_supplier", "audit_log", null, userId);
                UpdateBatchCache(c, tx, batchId, -units, -grams);
                _audit.WriteWithConnection(c, tx, userId, "return_to_supplier", "batch", batchId, null, new { units, grams, reason });
                tx.Commit();
            }
        }

        /// <summary>
        /// Convert bag to loose. Reduces source batch units, increases target batch grams.
        /// Two conversion ledger rows written.
        /// </summary>
        public void RecordConversion(long itemId, long sourceBatchId, long targetBatchId,
            int unitsRemoved, int gramsAdded, long userId)
        {
            using (var c = _db.Open())
            using (var tx = c.BeginTransaction())
            {
                Insert(c, tx, itemId, sourceBatchId, -unitsRemoved, 0, "conversion", "audit_log", null, userId);
                Insert(c, tx, itemId, targetBatchId, 0, gramsAdded, "conversion", "audit_log", null, userId);
                c.Execute("UPDATE batches SET qty_units=qty_units-@U WHERE id=@B",
                    new { U = unitsRemoved, B = sourceBatchId }, transaction: tx);
                c.Execute("UPDATE batches SET qty_grams=qty_grams+@G WHERE id=@B",
                    new { G = gramsAdded, B = targetBatchId }, transaction: tx);
                _audit.WriteWithConnection(c, tx, userId, "conversion", "batch", sourceBatchId, null,
                    new { source = sourceBatchId, target = targetBatchId, unitsRemoved, gramsAdded });
                tx.Commit();
            }
        }

        internal static void Insert(IDbConnection c, IDbTransaction tx, long itemId, long? batchId,
            int changeUnits, int changeGrams, string reason, string refTable, long? refId, long userId)
        {
            c.Execute(@"INSERT INTO stock_ledger(item_id, batch_id, change_units, change_grams, reason, ref_table, ref_id, user_id)
                VALUES(@I,@B,@U,@G,@R,@Rt,@Ri,@U2)",
                new { I = itemId, B = batchId, U = changeUnits, G = changeGrams, R = reason, Rt = refTable, Ri = refId, U2 = userId },
                transaction: tx);
        }

        internal static void UpdateBatchCache(IDbConnection c, IDbTransaction tx, long? batchId, int units, int grams)
        {
            if (!batchId.HasValue) return;
            c.Execute("UPDATE batches SET qty_units=qty_units+@U, qty_grams=qty_grams+@G WHERE id=@B",
                new { U = units, G = grams, B = batchId.Value }, transaction: tx);
        }

        public IList<Tuple<long, int, int>> LedgerTotalsByBatch()
        {
            using (var c = _db.Open())
            {
                var rows = c.Query<dynamic>(@"SELECT batch_id AS B,
                    SUM(change_units) AS U, SUM(change_grams) AS G
                    FROM stock_ledger WHERE batch_id IS NOT NULL GROUP BY batch_id");
                return rows.Select(r => Tuple.Create((long)r.B, (int)(long)r.U, (int)(long)r.G)).ToList();
            }
        }
    }

    /// <summary>Purchase entry — creates batches and writes stock_ledger 'purchase' rows.</summary>
    public class PurchaseRepository
    {
        private readonly Db _db;
        private readonly AuditLog _audit;
        public PurchaseRepository(Db db, AuditLog audit) { _db = db; _audit = audit; }

        public long Save(Purchase p, long userId)
        {
            using (var c = _db.Open())
            using (var tx = c.BeginTransaction())
            {
                // Duplicate invoice check enforced by UNIQUE(supplier_id, invoice_no).
                long purchaseId;
                try
                {
                    purchaseId = c.ExecuteScalar<long>(@"
                        INSERT INTO purchases(supplier_id, invoice_no, invoice_date, goods_paise, tax_paise, freight_paise, discount_paise, total_paise, payment_mode, due_date)
                        VALUES(@S,@I,@D,@G,@T,@F,@Dc,@Tt,@Pm,@Du);
                        SELECT last_insert_rowid();",
                        new { S = p.SupplierId, I = p.InvoiceNo, D = p.InvoiceDate.ToString("yyyy-MM-dd"),
                            G = p.GoodsPaise, T = p.TaxPaise, F = p.FreightPaise, Dc = p.DiscountPaise,
                            Tt = p.TotalPaise, Pm = p.PaymentMode,
                            Du = p.DueDate.HasValue ? p.DueDate.Value.ToString("yyyy-MM-dd") : null },
                        transaction: tx);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("Duplicate supplier invoice: " + p.InvoiceNo, ex);
                }
                p.Id = purchaseId;

                foreach (var line in p.Lines)
                {
                    line.PurchaseId = purchaseId;
                    long lineId = c.ExecuteScalar<long>(@"
                        INSERT INTO purchase_lines(purchase_id, item_id, batch_code, expiry_date,
                            qty_units, qty_grams, free_units, free_grams, cost_paise, mrp_paise, value_paise)
                        VALUES(@P,@I,@Bc,@E,@Qu,@Qg,@Fu,@Fg,@C,@M,@V);
                        SELECT last_insert_rowid();",
                        new { P = purchaseId, I = line.ItemId, Bc = line.BatchCode,
                            E = line.ExpiryDate.HasValue ? line.ExpiryDate.Value.ToString("yyyy-MM-dd") : null,
                            Qu = line.QtyUnits, Qg = line.QtyGrams,
                            Fu = line.FreeUnits, Fg = line.FreeGrams,
                            C = line.CostPaise, M = line.MrpPaise, V = line.ValuePaise },
                        transaction: tx);
                    line.Id = lineId;

                    int totUnits = line.QtyUnits + line.FreeUnits;
                    int totGrams = line.QtyGrams + line.FreeGrams;
                    long batchId = c.ExecuteScalar<long>(@"
                        INSERT INTO batches(item_id, batch_code, expiry_date, cost_paise, mrp_paise, selling_paise,
                            qty_grams, qty_units, supplier_id, purchase_line_id, received_at)
                        VALUES(@I,@Bc,@E,@C,@M,@Sel,@Qg,@Qu,@S,@Pl,datetime('now'));
                        SELECT last_insert_rowid();",
                        new { I = line.ItemId, Bc = line.BatchCode,
                            E = line.ExpiryDate.HasValue ? line.ExpiryDate.Value.ToString("yyyy-MM-dd") : null,
                            C = line.CostPaise, M = line.MrpPaise, Sel = line.MrpPaise,
                            Qg = totGrams, Qu = totUnits, S = p.SupplierId, Pl = lineId },
                        transaction: tx);

                    StockLedgerRepository.Insert(c, tx, line.ItemId, batchId, totUnits, totGrams,
                        "purchase", "purchases", purchaseId, userId);
                }

                _audit.WriteWithConnection(c, tx, userId, "purchase", "purchase", purchaseId, null, new { p.InvoiceNo, p.TotalPaise });
                tx.Commit();
                return purchaseId;
            }
        }

        public IList<Purchase> RecentPurchases(int limit = 50)
        {
            using (var c = _db.Open())
            {
                return c.Query<dynamic>(@"SELECT id, supplier_id AS SupplierId, invoice_no AS InvoiceNo,
                    invoice_date AS InvoiceDate, goods_paise AS GoodsPaise, tax_paise AS TaxPaise,
                    freight_paise AS FreightPaise, discount_paise AS DiscountPaise, total_paise AS TotalPaise,
                    payment_mode AS PaymentMode, due_date AS DueDate FROM purchases ORDER BY id DESC LIMIT @l",
                    new { l = limit }).Select(r => new Purchase
                    {
                        Id = (long)r.id,
                        SupplierId = (long)r.SupplierId,
                        InvoiceNo = (string)r.InvoiceNo,
                        InvoiceDate = DateTime.Parse((string)r.InvoiceDate),
                        GoodsPaise = (long)r.GoodsPaise,
                        TaxPaise = (long)r.TaxPaise,
                        FreightPaise = (long)r.FreightPaise,
                        DiscountPaise = (long)r.DiscountPaise,
                        TotalPaise = (long)r.TotalPaise,
                        PaymentMode = (string)r.PaymentMode,
                        DueDate = r.DueDate == null ? (DateTime?)null : DateTime.Parse((string)r.DueDate)
                    }).ToList();
            }
        }
    }

    /// <summary>Shift open/close. Closed shifts are locked from further inserts (checked in repo).</summary>
    public class ShiftRepository
    {
        private readonly Db _db;
        private readonly AuditLog _audit;
        public ShiftRepository(Db db, AuditLog audit) { _db = db; _audit = audit; }

        public Shift Open(int counterId, long userId, long openingFloatPaise)
        {
            using (var c = _db.Open())
            {
                var existing = c.QueryFirstOrDefault<long?>(
                    "SELECT id FROM shifts WHERE counter_id=@c AND status='open'", new { c = counterId });
                if (existing.HasValue) throw new InvalidOperationException("A shift is already open on this counter");
                long id = c.ExecuteScalar<long>(@"
                    INSERT INTO shifts(counter_id, user_id, opened_at, opening_float_paise)
                    VALUES(@C,@U,datetime('now'),@F); SELECT last_insert_rowid();",
                    new { C = counterId, U = userId, F = openingFloatPaise });
                _audit.Write(userId, "shift_open", "shift", id, null, new { counterId, openingFloatPaise });
                return FindById(id);
            }
        }

        public Shift OpenShiftFor(int counterId)
        {
            using (var c = _db.Open())
            {
                var r = c.QueryFirstOrDefault<dynamic>(
                    "SELECT id FROM shifts WHERE counter_id=@c AND status='open' ORDER BY id DESC LIMIT 1",
                    new { c = counterId });
                return r == null ? null : FindById((long)r.id);
            }
        }

        public Shift FindById(long id)
        {
            using (var c = _db.Open())
            {
                var r = c.QueryFirstOrDefault<dynamic>(@"SELECT id, counter_id AS CounterId, user_id AS UserId,
                    opened_at AS OpenedAt, closed_at AS ClosedAt,
                    opening_float_paise AS OpeningFloatPaise,
                    expected_cash_paise AS ExpectedCashPaise, counted_cash_paise AS CountedCashPaise,
                    difference_paise AS DifferencePaise, status FROM shifts WHERE id=@i", new { i = id });
                if (r == null) return null;
                return new Shift
                {
                    Id = (long)r.id,
                    CounterId = (int)(long)r.CounterId,
                    UserId = (long)r.UserId,
                    OpenedAt = DateTime.Parse((string)r.OpenedAt),
                    ClosedAt = r.ClosedAt == null ? (DateTime?)null : DateTime.Parse((string)r.ClosedAt),
                    OpeningFloatPaise = (long)r.OpeningFloatPaise,
                    ExpectedCashPaise = (long)r.ExpectedCashPaise,
                    CountedCashPaise = (long)r.CountedCashPaise,
                    DifferencePaise = (long)r.DifferencePaise,
                    Status = ((string)r.status) == "closed" ? ShiftStatus.Closed : ShiftStatus.Open
                };
            }
        }

        /// <summary>Compute expected cash: opening float + cash sales - petty cash paid.</summary>
        public long ExpectedCash(long shiftId, int counterId)
        {
            using (var c = _db.Open())
            {
                long opening = c.ExecuteScalar<long>("SELECT opening_float_paise FROM shifts WHERE id=@i", new { i = shiftId });
                DateTime opened = DateTime.Parse(c.ExecuteScalar<string>("SELECT opened_at FROM shifts WHERE id=@i", new { i = shiftId }));
                long cashSales = c.ExecuteScalar<long>(@"
                    SELECT COALESCE(SUM(p.amount_paise), 0) FROM payments p
                    JOIN bills b ON b.id=p.bill_id
                    WHERE p.mode='cash' AND b.counter_id=@c AND b.billed_at >= @from AND b.status='completed'",
                    new { c = counterId, from = opened.ToString("yyyy-MM-dd HH:mm:ss") });
                long petty = c.ExecuteScalar<long>("SELECT COALESCE(SUM(amount_paise), 0) FROM petty_cash WHERE shift_id=@i", new { i = shiftId });
                return opening + cashSales - petty;
            }
        }

        public IDictionary<string, long> NonCashTotals(long shiftId, int counterId)
        {
            using (var c = _db.Open())
            {
                DateTime opened = DateTime.Parse(c.ExecuteScalar<string>("SELECT opened_at FROM shifts WHERE id=@i", new { i = shiftId }));
                var res = new Dictionary<string, long>();
                foreach (var mode in new[] { "upi", "card", "khata" })
                {
                    long t = c.ExecuteScalar<long>(@"
                        SELECT COALESCE(SUM(p.amount_paise), 0) FROM payments p
                        JOIN bills b ON b.id=p.bill_id
                        WHERE p.mode=@m AND b.counter_id=@c AND b.billed_at >= @from AND b.status='completed'",
                        new { m = mode, c = counterId, from = opened.ToString("yyyy-MM-dd HH:mm:ss") });
                    res[mode] = t;
                }
                return res;
            }
        }

        public void RecordPettyCash(long shiftId, long amount, string note, long userId)
        {
            AssertOpen(shiftId);
            using (var c = _db.Open())
            {
                c.Execute("INSERT INTO petty_cash(shift_id, amount_paise, note, user_id) VALUES(@S,@A,@N,@U)",
                    new { S = shiftId, A = amount, N = note, U = userId });
            }
        }

        public void Close(long shiftId, IList<Tuple<long, int>> denomCounts, long userId)
        {
            AssertOpen(shiftId);
            using (var c = _db.Open())
            using (var tx = c.BeginTransaction())
            {
                long counted = 0;
                foreach (var dc in denomCounts)
                {
                    if (dc.Item2 <= 0) continue;
                    c.Execute("INSERT INTO cash_counts(shift_id, denomination_paise, count) VALUES(@S,@D,@N)",
                        new { S = shiftId, D = dc.Item1, N = dc.Item2 }, transaction: tx);
                    counted += dc.Item1 * dc.Item2;
                }
                int counterId = (int)c.ExecuteScalar<long>("SELECT counter_id FROM shifts WHERE id=@i", new { i = shiftId }, transaction: tx);
                long expected = ExpectedCash(shiftId, counterId);
                long diff = counted - expected;
                c.Execute(@"UPDATE shifts SET closed_at=datetime('now'), expected_cash_paise=@e,
                    counted_cash_paise=@c, difference_paise=@d, status='closed' WHERE id=@i",
                    new { e = expected, c = counted, d = diff, i = shiftId }, transaction: tx);
                _audit.WriteWithConnection(c, tx, userId, "shift_close", "shift", shiftId, null,
                    new { expected, counted, diff });
                tx.Commit();
            }
        }

        private void AssertOpen(long shiftId)
        {
            using (var c = _db.Open())
            {
                var s = c.QueryFirstOrDefault<string>("SELECT status FROM shifts WHERE id=@i", new { i = shiftId });
                if (s == null) throw new InvalidOperationException("Shift not found");
                if (s == "closed") throw new InvalidOperationException("Shift is closed; further inserts blocked");
            }
        }
    }
}
