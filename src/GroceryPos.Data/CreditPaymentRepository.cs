using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using GroceryPos.Domain;

namespace GroceryPos.Data
{
    /// <summary>Credit payments with FIFO allocation to open bills.</summary>
    public class CreditPaymentRepository
    {
        private readonly Db _db;
        private readonly AuditLog _audit;
        public CreditPaymentRepository(Db db, AuditLog audit) { _db = db; _audit = audit; }

        /// <summary>
        /// Record a payment and allocate against oldest unpaid credit bills first (FIFO).
        /// Optional overrideAllocations lets caller allocate to specific bills.
        /// Writes customer_ledger row and updates cached balance in one transaction.
        /// </summary>
        public long Receive(long customerId, long amountPaise, PaymentMode mode, string reference,
            long receivedBy, long? shiftId, string note,
            IList<Tuple<long, long>> overrideAllocations = null)
        {
            if (amountPaise <= 0) throw new ArgumentException("Amount must be positive");
            using (var c = _db.Open())
            using (var tx = c.BeginTransaction())
            {
                long paymentId = c.ExecuteScalar<long>(@"
                    INSERT INTO credit_payments(customer_id, amount_paise, mode, reference, received_at, received_by, shift_id, note)
                    VALUES(@C,@A,@M,@R,datetime('now'),@Rb,@S,@N);
                    SELECT last_insert_rowid();",
                    new { C = customerId, A = amountPaise, M = mode.ToString().ToLowerInvariant(),
                        R = reference, Rb = receivedBy, S = shiftId, N = note }, transaction: tx);

                // Allocate
                if (overrideAllocations != null)
                {
                    foreach (var pair in overrideAllocations)
                    {
                        c.Execute(@"INSERT INTO credit_allocations(credit_payment_id, bill_id, allocated_paise)
                            VALUES(@P,@B,@A)", new { P = paymentId, B = pair.Item1, A = pair.Item2 }, transaction: tx);
                    }
                }
                else
                {
                    long remaining = amountPaise;
                    // Find credit-sale bills for this customer, oldest first, with outstanding > 0
                    var openBills = c.Query<dynamic>(@"
                        SELECT b.id AS Id, b.net_paise AS Net,
                            COALESCE((SELECT SUM(allocated_paise) FROM credit_allocations WHERE bill_id=b.id), 0) AS Allocated
                        FROM bills b
                        WHERE b.customer_id=@c AND b.is_credit_sale=1 AND b.status='completed'
                        ORDER BY b.billed_at, b.id",
                        new { c = customerId }, transaction: tx).ToList();
                    foreach (var b in openBills)
                    {
                        if (remaining <= 0) break;
                        long outstanding = (long)b.Net - (long)b.Allocated;
                        if (outstanding <= 0) continue;
                        long apply = Math.Min(outstanding, remaining);
                        c.Execute(@"INSERT INTO credit_allocations(credit_payment_id, bill_id, allocated_paise)
                            VALUES(@P,@B,@A)", new { P = paymentId, B = (long)b.Id, A = apply }, transaction: tx);
                        remaining -= apply;
                    }
                }

                // Update balance + ledger
                long cur = c.ExecuteScalar<long>("SELECT current_balance_paise FROM customers WHERE id=@i", new { i = customerId }, transaction: tx);
                long newBal = cur - amountPaise;
                CustomerLedgerRepository.Insert(c, tx, customerId, DateTime.Now, "payment",
                    "credit_payments", paymentId, "Payment " + mode.ToString().ToUpperInvariant() +
                    (string.IsNullOrEmpty(reference) ? "" : " " + reference),
                    0, amountPaise, newBal, null, receivedBy, null);
                c.Execute("UPDATE customers SET current_balance_paise=@b, last_txn_at=datetime('now') WHERE id=@i",
                    new { b = newBal, i = customerId }, transaction: tx);
                _audit.WriteWithConnection(c, tx, receivedBy, "credit_payment", "customer", customerId, null, new { amountPaise, mode = mode.ToString(), reference });

                tx.Commit();
                return paymentId;
            }
        }

        /// <summary>Ageing analysis for a single customer using bill-level allocations.</summary>
        public IList<AgeingBucket> Ageing(long customerId, DateTime asOf)
        {
            using (var c = _db.Open())
            {
                var rows = c.Query<dynamic>(@"
                    SELECT b.id AS Id, b.net_paise AS Net, b.billed_at AS BilledAt,
                        COALESCE((SELECT SUM(allocated_paise) FROM credit_allocations WHERE bill_id=b.id), 0) AS Allocated
                    FROM bills b
                    WHERE b.customer_id=@c AND b.is_credit_sale=1 AND b.status='completed'",
                    new { c = customerId }).ToList();
                var buckets = new List<AgeingBucket>();
                foreach (var b in rows)
                {
                    long outstanding = (long)b.Net - (long)b.Allocated;
                    if (outstanding <= 0) continue;
                    DateTime billed = DateTime.Parse((string)b.BilledAt);
                    int days = (int)(asOf - billed).TotalDays;
                    string bucket = days <= 30 ? "0-30" : days <= 60 ? "31-60" : days <= 90 ? "61-90" : ">90";
                    buckets.Add(new AgeingBucket { BillId = (long)b.Id, DaysOld = days, Bucket = bucket, OutstandingPaise = outstanding });
                }
                return buckets;
            }
        }
    }

    public class AgeingBucket
    {
        public long BillId;
        public int DaysOld;
        public string Bucket;
        public long OutstandingPaise;
    }
}
