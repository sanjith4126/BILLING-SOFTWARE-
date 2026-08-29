using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Dapper;
using GroceryPos.Domain;

namespace GroceryPos.Data
{
    /// <summary>
    /// Bill save and cancel. Bill number is issued from settings inside the same transaction.
    /// Sale writes to stock_ledger (reason='sale'). Cancellation reverses those rows.
    /// </summary>
    public class BillRepository
    {
        private readonly Db _db;
        private readonly AuditLog _audit;
        public BillRepository(Db db, AuditLog audit) { _db = db; _audit = audit; }

        /// <summary>
        /// Save a bill in a single transaction:
        /// - claim next_bill_no from settings (gapless)
        /// - insert bills, bill_lines, payments
        /// - insert stock_ledger 'sale' rows per line
        /// - for credit sales, insert customer_ledger 'credit_sale' + update cache
        /// - award loyalty points if customer present
        /// </summary>
        public long Save(Bill bill, IList<Payment> payments, long userId, int counterId,
                         Customer customer, long? shiftId, int loyaltyPointsPer100Rupees)
        {
            using (var c = _db.Open())
            using (var tx = c.BeginTransaction())
            {
                // Claim bill number
                var nextStr = c.QueryFirstOrDefault<string>(
                    "SELECT value FROM settings WHERE key='next_bill_no'", transaction: tx);
                long nextNo = long.Parse(nextStr ?? "1");
                c.Execute("UPDATE settings SET value=@v WHERE key='next_bill_no'",
                    new { v = (nextNo + 1).ToString() }, transaction: tx);
                bill.BillNo = nextNo;
                bill.BilledAt = DateTime.Now;
                bill.Status = BillStatus.Completed;
                bill.CounterId = counterId;
                bill.UserId = userId;
                bill.CustomerId = customer == null ? (long?)null : customer.Id;
                bill.IsCreditSale = payments.Any(p => p.Mode == PaymentMode.Khata);

                long billId = c.ExecuteScalar<long>(@"
                    INSERT INTO bills(bill_no, counter_id, user_id, customer_id, billed_at, status,
                      subtotal_paise, discount_paise, taxable_paise, cgst_paise, sgst_paise,
                      round_off_paise, net_paise, is_credit_sale)
                    VALUES(@BillNo,@CounterId,@UserId,@CustomerId,@BilledAt,'completed',
                      @SubtotalPaise,@DiscountPaise,@TaxablePaise,@CgstPaise,@SgstPaise,
                      @RoundOffPaise,@NetPaise,@IsCredit);
                    SELECT last_insert_rowid();",
                    new
                    {
                        bill.BillNo, bill.CounterId, bill.UserId, bill.CustomerId,
                        BilledAt = bill.BilledAt.ToString("yyyy-MM-dd HH:mm:ss"),
                        bill.SubtotalPaise, bill.DiscountPaise, bill.TaxablePaise,
                        bill.CgstPaise, bill.SgstPaise, bill.RoundOffPaise, bill.NetPaise,
                        IsCredit = bill.IsCreditSale ? 1 : 0
                    }, transaction: tx);
                bill.Id = billId;

                int lineNo = 1;
                foreach (var l in bill.Lines)
                {
                    l.BillId = billId;
                    l.LineNo = lineNo++;
                    c.Execute(@"INSERT INTO bill_lines(bill_id, line_no, item_id, batch_id,
                        qty_units, qty_grams, weight_source, raw_grams,
                        rate_paise, discount_paise, tax_rate_bp, tax_paise, amount_paise, hsn_code)
                        VALUES(@BillId,@LineNo,@ItemId,@BatchId,@QtyUnits,@QtyGrams,@Ws,@RawGrams,
                        @RatePaise,@DiscountPaise,@TaxRateBp,@TaxPaise,@AmountPaise,@HsnCode)",
                        new
                        {
                            l.BillId, l.LineNo, l.ItemId, l.BatchId,
                            l.QtyUnits, l.QtyGrams,
                            Ws = l.WeightSource.ToString().ToLowerInvariant(),
                            l.RawGrams, l.RatePaise, l.DiscountPaise,
                            l.TaxRateBp, l.TaxPaise, l.AmountPaise, l.HsnCode
                        }, transaction: tx);

                    // Stock ledger: sale reduces stock. If qty_grams > 0, use grams; else units.
                    c.Execute(@"INSERT INTO stock_ledger(item_id, batch_id, change_units, change_grams,
                        reason, ref_table, ref_id, user_id, at)
                        VALUES(@Item,@Batch,@U,@G,'sale','bills',@Ref,@User,datetime('now'))",
                        new
                        {
                            Item = l.ItemId,
                            Batch = l.BatchId,
                            U = l.QtyUnits > 0 ? -l.QtyUnits : 0,
                            G = l.QtyGrams > 0 ? -l.QtyGrams : 0,
                            Ref = billId,
                            User = userId
                        }, transaction: tx);

                    // Reduce batch cached qty (batch qty is only a cache; ledger is truth).
                    if (l.BatchId.HasValue)
                    {
                        c.Execute(@"UPDATE batches SET qty_units = qty_units - @U, qty_grams = qty_grams - @G
                                    WHERE id=@B",
                            new { U = l.QtyUnits, G = l.QtyGrams, B = l.BatchId.Value }, transaction: tx);
                    }
                }

                foreach (var p in payments)
                {
                    p.BillId = billId;
                    c.Execute(@"INSERT INTO payments(bill_id, mode, amount_paise, reference)
                                VALUES(@BillId,@Mode,@AmountPaise,@Reference)",
                        new
                        {
                            p.BillId,
                            Mode = p.Mode.ToString().ToLowerInvariant(),
                            p.AmountPaise, p.Reference
                        }, transaction: tx);
                }
                bill.Payments = payments.ToList();

                // Credit sale: write customer_ledger, update cache
                if (bill.IsCreditSale && customer != null)
                {
                    long khata = payments.Where(p => p.Mode == PaymentMode.Khata).Sum(p => p.AmountPaise);
                    long newBal = customer.CurrentBalancePaise + khata;
                    c.Execute(@"INSERT INTO customer_ledger(customer_id, at, type, ref_table, ref_id,
                        description, debit_paise, credit_paise, balance_paise, user_id, counter_id, created_at)
                        VALUES(@Cust,datetime('now'),'credit_sale','bills',@Bill,@Desc,@D,0,@Bal,@U,@Ctr,datetime('now'))",
                        new
                        {
                            Cust = customer.Id, Bill = billId,
                            Desc = "Bill INV-" + bill.BillNo,
                            D = khata, Bal = newBal, U = userId, Ctr = counterId
                        }, transaction: tx);
                    c.Execute("UPDATE customers SET current_balance_paise=@b, last_txn_at=datetime('now') WHERE id=@i",
                        new { b = newBal, i = customer.Id }, transaction: tx);
                }

                // Loyalty: 1 point per Rs.100 of net (only if customer set)
                if (customer != null && loyaltyPointsPer100Rupees > 0)
                {
                    long pts = (bill.NetPaise / 10000L) * loyaltyPointsPer100Rupees;
                    if (pts > 0)
                    {
                        c.Execute("UPDATE customers SET loyalty_points = loyalty_points + @p WHERE id=@i",
                            new { p = pts, i = customer.Id }, transaction: tx);
                    }
                }

                tx.Commit();
                return billId;
            }
        }

        public Bill FindById(long id)
        {
            using (var c = _db.Open())
            {
                var b = c.QueryFirstOrDefault<dynamic>(
                    @"SELECT id, bill_no AS BillNo, counter_id AS CounterId, user_id AS UserId,
                             customer_id AS CustomerId, billed_at AS BilledAt, status,
                             subtotal_paise AS SubtotalPaise, discount_paise AS DiscountPaise,
                             taxable_paise AS TaxablePaise, cgst_paise AS CgstPaise, sgst_paise AS SgstPaise,
                             round_off_paise AS RoundOffPaise, net_paise AS NetPaise,
                             is_credit_sale AS IsCreditSale, cancelled_by AS CancelledBy,
                             cancelled_at AS CancelledAt, cancel_reason AS CancelReason
                      FROM bills WHERE id=@i", new { i = id });
                if (b == null) return null;
                var bill = MapBill(b);
                bill.Lines = c.Query<dynamic>(
                    @"SELECT bl.id, bl.bill_id AS BillId, bl.line_no AS LineNo, bl.item_id AS ItemId,
                             bl.batch_id AS BatchId, bl.qty_units AS QtyUnits, bl.qty_grams AS QtyGrams,
                             bl.weight_source AS Ws, bl.raw_grams AS RawGrams,
                             bl.rate_paise AS RatePaise, bl.discount_paise AS DiscountPaise,
                             bl.tax_rate_bp AS TaxRateBp, bl.tax_paise AS TaxPaise,
                             bl.amount_paise AS AmountPaise, bl.hsn_code AS HsnCode,
                             i.name AS ItemName
                      FROM bill_lines bl JOIN items i ON i.id=bl.item_id
                      WHERE bl.bill_id=@i ORDER BY bl.line_no", new { i = id })
                    .Select(MapLine).ToList();
                bill.Payments = c.Query<dynamic>(
                    "SELECT id, bill_id AS BillId, mode, amount_paise AS AmountPaise, reference FROM payments WHERE bill_id=@i",
                    new { i = id }).Select(MapPayment).ToList();
                return bill;
            }
        }

        public IList<Bill> RecentBills(int limit = 20)
        {
            using (var c = _db.Open())
            {
                return c.Query<dynamic>(
                    @"SELECT id, bill_no AS BillNo, counter_id AS CounterId, user_id AS UserId,
                             customer_id AS CustomerId, billed_at AS BilledAt, status,
                             subtotal_paise AS SubtotalPaise, discount_paise AS DiscountPaise,
                             taxable_paise AS TaxablePaise, cgst_paise AS CgstPaise, sgst_paise AS SgstPaise,
                             round_off_paise AS RoundOffPaise, net_paise AS NetPaise,
                             is_credit_sale AS IsCreditSale, cancelled_by AS CancelledBy,
                             cancelled_at AS CancelledAt, cancel_reason AS CancelReason
                      FROM bills ORDER BY id DESC LIMIT @l", new { l = limit })
                    .Select(r => (Bill)MapBill(r)).ToList();
            }
        }

        public Bill FindByBillNo(long billNo)
        {
            using (var c = _db.Open())
            {
                var id = c.QueryFirstOrDefault<long?>("SELECT id FROM bills WHERE bill_no=@n", new { n = billNo });
                return id.HasValue ? FindById(id.Value) : null;
            }
        }

        /// <summary>
        /// Cancel a bill: status='cancelled', preserve bill_no, reverse stock_ledger, reverse credit ledger,
        /// audit log. Requires manager PIN validated by caller.
        /// </summary>
        public void Cancel(long billId, long cancelledByUserId, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("Cancel reason is required");
            using (var c = _db.Open())
            using (var tx = c.BeginTransaction())
            {
                var b = c.QueryFirstOrDefault<dynamic>(
                    "SELECT status, customer_id, is_credit_sale, net_paise, bill_no FROM bills WHERE id=@i",
                    new { i = billId }, transaction: tx);
                if (b == null) throw new InvalidOperationException("Bill not found");
                if ((string)b.status == "cancelled") throw new InvalidOperationException("Bill already cancelled");

                c.Execute(@"UPDATE bills SET status='cancelled', cancelled_by=@u,
                            cancelled_at=datetime('now'), cancel_reason=@r WHERE id=@i",
                    new { u = cancelledByUserId, r = reason, i = billId }, transaction: tx);

                // Reverse stock_ledger sale rows: insert opposite entries.
                var saleRows = c.Query<dynamic>(
                    @"SELECT item_id AS ItemId, batch_id AS BatchId, change_units AS U, change_grams AS G
                      FROM stock_ledger WHERE ref_table='bills' AND ref_id=@i AND reason='sale'",
                    new { i = billId }, transaction: tx).ToList();
                foreach (var s in saleRows)
                {
                    c.Execute(@"INSERT INTO stock_ledger(item_id, batch_id, change_units, change_grams,
                        reason, ref_table, ref_id, user_id, at)
                        VALUES(@Item,@Batch,@U,@G,'sale','bills',@Ref,@User,datetime('now'))",
                        new { Item = s.ItemId, Batch = s.BatchId, U = -(long)s.U, G = -(long)s.G, Ref = billId, User = cancelledByUserId },
                        transaction: tx);
                    if (s.BatchId != null)
                    {
                        c.Execute("UPDATE batches SET qty_units=qty_units+@U, qty_grams=qty_grams+@G WHERE id=@B",
                            new { U = -(long)s.U, G = -(long)s.G, B = (long)s.BatchId }, transaction: tx);
                    }
                }

                // Reverse credit sale ledger row if any
                if (((long)b.is_credit_sale) != 0 && b.customer_id != null)
                {
                    long custId = (long)b.customer_id;
                    var ledger = c.QueryFirstOrDefault<dynamic>(
                        @"SELECT id, debit_paise AS D, credit_paise AS Cr
                          FROM customer_ledger
                          WHERE customer_id=@c AND ref_table='bills' AND ref_id=@i AND type='credit_sale'",
                        new { c = custId, i = billId }, transaction: tx);
                    if (ledger != null)
                    {
                        long amt = (long)ledger.D;
                        long curBal = c.ExecuteScalar<long>(
                            "SELECT current_balance_paise FROM customers WHERE id=@i",
                            new { i = custId }, transaction: tx);
                        long newBal = curBal - amt;
                        c.Execute(@"INSERT INTO customer_ledger(customer_id, at, type, ref_table, ref_id,
                            description, debit_paise, credit_paise, balance_paise, reverses_ledger_id,
                            user_id, created_at)
                            VALUES(@C,datetime('now'),'reversal','customer_ledger',@Ref,@Desc,
                                   0,@Amt,@Bal,@Rev,@U,datetime('now'))",
                            new { C = custId, Ref = (long)ledger.id, Desc = "Cancel INV-" + b.bill_no, Amt = amt, Bal = newBal, Rev = (long)ledger.id, U = cancelledByUserId },
                            transaction: tx);
                        c.Execute("UPDATE customers SET current_balance_paise=@b WHERE id=@i",
                            new { b = newBal, i = custId }, transaction: tx);
                    }
                }

                _audit.WriteWithConnection(c, tx, cancelledByUserId, "cancel", "bill", billId,
                    null, new { reason = reason, bill_no = (long)b.bill_no });
                tx.Commit();
            }
        }

        private static Bill MapBill(dynamic r)
        {
            return new Bill
            {
                Id = (long)r.id,
                BillNo = (long)r.BillNo,
                CounterId = (int)(long)r.CounterId,
                UserId = (long)r.UserId,
                CustomerId = r.CustomerId == null ? (long?)null : (long?)(long)r.CustomerId,
                BilledAt = DateTime.Parse((string)r.BilledAt),
                Status = ((string)r.status) == "cancelled" ? BillStatus.Cancelled : BillStatus.Completed,
                SubtotalPaise = (long)r.SubtotalPaise,
                DiscountPaise = (long)r.DiscountPaise,
                TaxablePaise = (long)r.TaxablePaise,
                CgstPaise = (long)r.CgstPaise,
                SgstPaise = (long)r.SgstPaise,
                RoundOffPaise = (long)r.RoundOffPaise,
                NetPaise = (long)r.NetPaise,
                IsCreditSale = ((long)r.IsCreditSale) != 0,
                CancelledBy = r.CancelledBy == null ? (long?)null : (long?)(long)r.CancelledBy,
                CancelledAt = r.CancelledAt == null ? (DateTime?)null : (DateTime?)DateTime.Parse((string)r.CancelledAt),
                CancelReason = (string)r.CancelReason
            };
        }

        private static BillLine MapLine(dynamic r)
        {
            WeightSource ws;
            switch ((string)r.Ws)
            {
                case "scale": ws = WeightSource.Scale; break;
                case "label": ws = WeightSource.Label; break;
                case "manual": ws = WeightSource.Manual; break;
                default: ws = WeightSource.Na; break;
            }
            return new BillLine
            {
                Id = (long)r.id,
                BillId = (long)r.BillId,
                LineNo = (int)(long)r.LineNo,
                ItemId = (long)r.ItemId,
                BatchId = r.BatchId == null ? (long?)null : (long?)(long)r.BatchId,
                QtyUnits = (int)(long)r.QtyUnits,
                QtyGrams = (int)(long)r.QtyGrams,
                WeightSource = ws,
                RawGrams = (int)(long)r.RawGrams,
                RatePaise = (long)r.RatePaise,
                DiscountPaise = (long)r.DiscountPaise,
                TaxRateBp = (int)(long)r.TaxRateBp,
                TaxPaise = (long)r.TaxPaise,
                AmountPaise = (long)r.AmountPaise,
                HsnCode = (string)r.HsnCode,
                ItemName = (string)r.ItemName
            };
        }

        private static Payment MapPayment(dynamic r)
        {
            PaymentMode m;
            switch ((string)r.mode)
            {
                case "cash": m = PaymentMode.Cash; break;
                case "upi": m = PaymentMode.Upi; break;
                case "card": m = PaymentMode.Card; break;
                default: m = PaymentMode.Khata; break;
            }
            return new Payment
            {
                Id = (long)r.id,
                BillId = (long)r.BillId,
                Mode = m,
                AmountPaise = (long)r.AmountPaise,
                Reference = (string)r.reference
            };
        }
    }
}
