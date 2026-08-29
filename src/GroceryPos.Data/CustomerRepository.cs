using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using GroceryPos.Domain;

namespace GroceryPos.Data
{
    /// <summary>Customer master. Credit off by default. Ledger via CustomerLedgerRepository.</summary>
    public class CustomerRepository
    {
        private readonly Db _db;
        private readonly AuditLog _audit;
        public CustomerRepository(Db db, AuditLog audit) { _db = db; _audit = audit; }

        private const string Cols = @"id, phone, name, address,
            credit_limit_paise AS CreditLimitPaise, credit_allowed AS CreditAllowedRaw,
            opening_balance_paise AS OpeningBalancePaise, opening_balance_at AS OpeningBalanceAt,
            current_balance_paise AS CurrentBalancePaise, loyalty_points AS LoyaltyPoints,
            since, last_txn_at AS LastTxnAt, notes, is_active AS IsActiveRaw";

        public Customer FindById(long id)
        {
            using (var c = _db.Open())
            {
                var r = c.QueryFirstOrDefault<dynamic>("SELECT " + Cols + " FROM customers WHERE id=@i", new { i = id });
                return r == null ? null : Map(r);
            }
        }

        public Customer FindByPhone(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return null;
            using (var c = _db.Open())
            {
                var r = c.QueryFirstOrDefault<dynamic>("SELECT " + Cols + " FROM customers WHERE phone=@p", new { p = phone.Trim() });
                return r == null ? null : Map(r);
            }
        }

        public IList<Customer> Search(string term, int limit = 50)
        {
            using (var c = _db.Open())
            {
                var like = "%" + (term ?? "") + "%";
                var rows = c.Query<dynamic>("SELECT " + Cols + " FROM customers WHERE is_active=1 AND (name LIKE @t OR phone LIKE @t) ORDER BY name LIMIT @l",
                    new { t = like, l = limit });
                return rows.Select(r => (Customer)Map(r)).ToList();
            }
        }

        public IList<Customer> WithOutstanding()
        {
            using (var c = _db.Open())
            {
                var rows = c.Query<dynamic>("SELECT " + Cols + " FROM customers WHERE current_balance_paise != 0 ORDER BY current_balance_paise DESC");
                return rows.Select(r => (Customer)Map(r)).ToList();
            }
        }

        public IList<Customer> All()
        {
            using (var c = _db.Open())
            {
                var rows = c.Query<dynamic>("SELECT " + Cols + " FROM customers ORDER BY name");
                return rows.Select(r => (Customer)Map(r)).ToList();
            }
        }

        public long Create(Customer cust, long userId)
        {
            if (string.IsNullOrWhiteSpace(cust.Name)) throw new ArgumentException("Name required");
            if (cust.CreditAllowed && string.IsNullOrWhiteSpace(cust.Phone))
                throw new ArgumentException("Credit customers require a phone number");
            using (var c = _db.Open())
            using (var tx = c.BeginTransaction())
            {
                long id = c.ExecuteScalar<long>(@"
                    INSERT INTO customers(phone, name, address, credit_limit_paise, credit_allowed,
                        opening_balance_paise, current_balance_paise, loyalty_points, notes, is_active, created_by)
                    VALUES(@Phone,@Name,@Address,@Lim,@Allow,0,0,0,@Notes,1,@U);
                    SELECT last_insert_rowid();",
                    new { cust.Phone, cust.Name, cust.Address, Lim = cust.CreditLimitPaise, Allow = cust.CreditAllowed ? 1 : 0, cust.Notes, U = userId },
                    transaction: tx);
                _audit.WriteWithConnection(c, tx, userId, "create", "customer", id, null, cust);
                tx.Commit();
                return id;
            }
        }

        public void SetCreditLimit(long customerId, long newLimit, long userId)
        {
            using (var c = _db.Open())
            using (var tx = c.BeginTransaction())
            {
                var old = c.QueryFirstOrDefault<long>("SELECT credit_limit_paise FROM customers WHERE id=@i", new { i = customerId }, transaction: tx);
                c.Execute("UPDATE customers SET credit_limit_paise=@l, updated_at=datetime('now') WHERE id=@i",
                    new { l = newLimit, i = customerId }, transaction: tx);
                c.Execute(@"INSERT INTO credit_limit_events(customer_id, event_type, old_limit_paise, new_limit_paise, authorised_by)
                    VALUES(@C,'limit_changed',@Old,@New,@U)", new { C = customerId, Old = old, New = newLimit, U = userId }, transaction: tx);
                _audit.WriteWithConnection(c, tx, userId, "credit_limit_change", "customer", customerId, new { old_limit = old }, new { new_limit = newLimit });
                tx.Commit();
            }
        }

        public void SetCreditAllowed(long customerId, bool allowed, long userId)
        {
            using (var c = _db.Open())
            using (var tx = c.BeginTransaction())
            {
                c.Execute("UPDATE customers SET credit_allowed=@a, updated_at=datetime('now') WHERE id=@i",
                    new { a = allowed ? 1 : 0, i = customerId }, transaction: tx);
                c.Execute(@"INSERT INTO credit_limit_events(customer_id, event_type, authorised_by)
                    VALUES(@C,@E,@U)", new { C = customerId, E = allowed ? "credit_enabled" : "credit_disabled", U = userId }, transaction: tx);
                _audit.WriteWithConnection(c, tx, userId, allowed ? "credit_enable" : "credit_disable", "customer", customerId, null, null);
                tx.Commit();
            }
        }

        /// <summary>Reconcile cached balance against ledger; returns any drifts.</summary>
        public IList<Tuple<long, long, long>> Reconcile()
        {
            var drifts = new List<Tuple<long, long, long>>();
            using (var c = _db.Open())
            {
                var rows = c.Query<dynamic>(@"
                    SELECT cu.id AS Id, cu.current_balance_paise AS Cached,
                        COALESCE((SELECT SUM(debit_paise) - SUM(credit_paise) FROM customer_ledger WHERE customer_id=cu.id), 0) AS Truth
                    FROM customers cu");
                foreach (var r in rows)
                {
                    long id = (long)r.Id, cached = (long)r.Cached, truth = (long)r.Truth;
                    if (cached != truth) drifts.Add(Tuple.Create(id, cached, truth));
                }
            }
            return drifts;
        }

        private static Customer Map(dynamic r)
        {
            return new Customer
            {
                Id = (long)r.id,
                Phone = (string)r.phone,
                Name = (string)r.name,
                Address = (string)r.address,
                CreditLimitPaise = (long)r.CreditLimitPaise,
                CreditAllowed = ((long)r.CreditAllowedRaw) != 0,
                OpeningBalancePaise = (long)r.OpeningBalancePaise,
                OpeningBalanceAt = r.OpeningBalanceAt == null ? (DateTime?)null : DateTime.Parse((string)r.OpeningBalanceAt),
                CurrentBalancePaise = (long)r.CurrentBalancePaise,
                LoyaltyPoints = (long)r.LoyaltyPoints,
                Since = DateTime.Parse((string)r.since),
                LastTxnAt = r.LastTxnAt == null ? (DateTime?)null : DateTime.Parse((string)r.LastTxnAt),
                Notes = (string)r.notes,
                IsActive = ((long)r.IsActiveRaw) != 0
            };
        }
    }
}
