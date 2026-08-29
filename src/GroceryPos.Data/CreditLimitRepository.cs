using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;

namespace GroceryPos.Data
{
    /// <summary>Records every credit limit override attempt (allowed or refused).</summary>
    public class CreditLimitRepository
    {
        private readonly Db _db;
        public CreditLimitRepository(Db db) { _db = db; }

        public void LogEvent(long customerId, string eventType,
            long? oldLimit, long? newLimit, long? billId,
            long? attempted, long? balanceAtTime,
            string reason, long? authorisedBy, long? requestedBy)
        {
            using (var c = _db.Open())
            {
                c.Execute(@"INSERT INTO credit_limit_events(customer_id, event_type, old_limit_paise, new_limit_paise,
                    bill_id, attempted_paise, balance_at_time_paise, reason, authorised_by, requested_by)
                    VALUES(@C,@E,@Old,@New,@B,@A,@Bal,@R,@Au,@Rq)",
                    new { C = customerId, E = eventType, Old = oldLimit, New = newLimit, B = billId,
                        A = attempted, Bal = balanceAtTime, R = reason, Au = authorisedBy, Rq = requestedBy });
            }
        }

        public IList<dynamic> RecentEvents(int limit = 100)
        {
            using (var c = _db.Open())
            {
                return c.Query<dynamic>(@"SELECT e.*, cu.name AS CustomerName FROM credit_limit_events e
                    LEFT JOIN customers cu ON cu.id=e.customer_id ORDER BY e.at DESC LIMIT @l",
                    new { l = limit }).ToList();
            }
        }
    }
}
