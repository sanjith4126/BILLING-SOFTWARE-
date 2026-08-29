using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Dapper;
using GroceryPos.Domain;

namespace GroceryPos.Data
{
    /// <summary>Append-only customer ledger. Every row updates cached current_balance_paise.</summary>
    public class CustomerLedgerRepository
    {
        private readonly Db _db;
        private readonly AuditLog _audit;
        public CustomerLedgerRepository(Db db, AuditLog audit) { _db = db; _audit = audit; }

        public IList<LedgerEntry> ForCustomer(long customerId)
        {
            using (var c = _db.Open())
            {
                var rows = c.Query<dynamic>(@"
                    SELECT id, customer_id AS CustomerId, at, type,
                        ref_table AS RefTable, ref_id AS RefId, description,
                        debit_paise AS DebitPaise, credit_paise AS CreditPaise,
                        balance_paise AS BalancePaise, reverses_ledger_id AS ReversesLedgerId,
                        user_id AS UserId, counter_id AS CounterId, created_at AS CreatedAt
                    FROM customer_ledger WHERE customer_id=@c ORDER BY at, id",
                    new { c = customerId });
                return rows.Select(r => (LedgerEntry)Map(r)).ToList();
            }
        }

        public long WriteOpening(long customerId, long paise, DateTime asOf, long userId)
        {
            using (var c = _db.Open())
            using (var tx = c.BeginTransaction())
            {
                var existing = c.ExecuteScalar<long>("SELECT COUNT(*) FROM customer_ledger WHERE customer_id=@c", new { c = customerId }, transaction: tx);
                if (existing > 0) throw new InvalidOperationException("Cannot enter opening balance: customer already has ledger entries");
                long id = Insert(c, tx, customerId, asOf, "opening", null, null, "Opening balance", paise, 0, paise, null, userId, null);
                c.Execute("UPDATE customers SET current_balance_paise=@b, opening_balance_paise=@b, opening_balance_at=@a WHERE id=@i",
                    new { b = paise, a = asOf.ToString("yyyy-MM-dd HH:mm:ss"), i = customerId }, transaction: tx);
                _audit.WriteWithConnection(c, tx, userId, "opening_balance", "customer", customerId, null, new { paise, as_of = asOf });
                tx.Commit();
                return id;
            }
        }

        public long WriteWriteOff(long customerId, long paise, string reason, long userId)
        {
            if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Reason required");
            using (var c = _db.Open())
            using (var tx = c.BeginTransaction())
            {
                long cur = c.ExecuteScalar<long>("SELECT current_balance_paise FROM customers WHERE id=@i", new { i = customerId }, transaction: tx);
                long newBal = cur - paise;
                long id = Insert(c, tx, customerId, DateTime.Now, "write_off", null, null, "Write off: " + reason, 0, paise, newBal, null, userId, null);
                c.Execute("UPDATE customers SET current_balance_paise=@b WHERE id=@i", new { b = newBal, i = customerId }, transaction: tx);
                _audit.WriteWithConnection(c, tx, userId, "write_off", "customer", customerId, null, new { paise, reason });
                tx.Commit();
                return id;
            }
        }

        public long WriteAdjustment(long customerId, long debit, long credit, string reason, long userId)
        {
            if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Reason required");
            using (var c = _db.Open())
            using (var tx = c.BeginTransaction())
            {
                long cur = c.ExecuteScalar<long>("SELECT current_balance_paise FROM customers WHERE id=@i", new { i = customerId }, transaction: tx);
                long newBal = cur + debit - credit;
                long id = Insert(c, tx, customerId, DateTime.Now, "adjustment", null, null, "Adjustment: " + reason, debit, credit, newBal, null, userId, null);
                c.Execute("UPDATE customers SET current_balance_paise=@b WHERE id=@i", new { b = newBal, i = customerId }, transaction: tx);
                _audit.WriteWithConnection(c, tx, userId, "adjustment", "customer", customerId, null, new { debit, credit, reason });
                tx.Commit();
                return id;
            }
        }

        internal static long Insert(IDbConnection c, IDbTransaction tx, long custId, DateTime at, string type,
            string refTable, long? refId, string description, long debit, long credit, long balance,
            long? reversesId, long userId, int? counterId)
        {
            return c.ExecuteScalar<long>(@"
                INSERT INTO customer_ledger(customer_id, at, type, ref_table, ref_id, description,
                    debit_paise, credit_paise, balance_paise, reverses_ledger_id, user_id, counter_id, created_at)
                VALUES(@C,@At,@T,@RT,@RI,@D,@Db,@Cr,@Bal,@Rev,@U,@Ctr,datetime('now'));
                SELECT last_insert_rowid();",
                new { C = custId, At = at.ToString("yyyy-MM-dd HH:mm:ss"), T = type, RT = refTable, RI = refId,
                    D = description, Db = debit, Cr = credit, Bal = balance, Rev = reversesId, U = userId, Ctr = counterId },
                transaction: tx);
        }

        private static LedgerEntry Map(dynamic r)
        {
            LedgerType t;
            switch ((string)r.type)
            {
                case "opening": t = LedgerType.Opening; break;
                case "credit_sale": t = LedgerType.CreditSale; break;
                case "payment": t = LedgerType.Payment; break;
                case "discount": t = LedgerType.Discount; break;
                case "write_off": t = LedgerType.WriteOff; break;
                case "adjustment": t = LedgerType.Adjustment; break;
                default: t = LedgerType.Reversal; break;
            }
            return new LedgerEntry
            {
                Id = (long)r.id,
                CustomerId = (long)r.CustomerId,
                At = DateTime.Parse((string)r.at),
                Type = t,
                RefTable = (string)r.RefTable,
                RefId = r.RefId == null ? (long?)null : (long?)(long)r.RefId,
                Description = (string)r.description,
                DebitPaise = (long)r.DebitPaise,
                CreditPaise = (long)r.CreditPaise,
                BalancePaise = (long)r.BalancePaise,
                ReversesLedgerId = r.ReversesLedgerId == null ? (long?)null : (long?)(long)r.ReversesLedgerId,
                UserId = (long)r.UserId,
                CounterId = r.CounterId == null ? (int?)null : (int?)(long)r.CounterId,
                CreatedAt = DateTime.Parse((string)r.CreatedAt)
            };
        }
    }
}
