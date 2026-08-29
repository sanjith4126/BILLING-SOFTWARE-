using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Dapper;

namespace GroceryPos.Data
{
    public class Migrator
    {
        private readonly Db _db;

        public Migrator(Db db) { _db = db; }

        public void Migrate()
        {
            var asm = typeof(Migrator).Assembly;
            var resources = asm.GetManifestResourceNames()
                .Where(n => n.Contains(".Migrations.") && n.EndsWith(".sql"))
                .OrderBy(n => n)
                .ToList();

            using (var c = _db.Open())
            {
                EnsureVersionTable(c);
                var applied = new HashSet<int>(c.Query<int>("SELECT version FROM schema_migrations"));

                foreach (var res in resources)
                {
                    int version = ParseVersion(res);
                    if (applied.Contains(version)) continue;
                    string sql;
                    using (var s = asm.GetManifestResourceStream(res))
                    using (var r = new StreamReader(s))
                        sql = r.ReadToEnd();

                    using (var tx = c.BeginTransaction())
                    {
                        c.Execute(sql, transaction: tx);
                        c.Execute("INSERT OR IGNORE INTO schema_migrations(version) VALUES(@v)",
                            new { v = version }, transaction: tx);
                        tx.Commit();
                    }
                }
            }
        }

        private static void EnsureVersionTable(System.Data.IDbConnection c)
        {
            c.Execute(@"CREATE TABLE IF NOT EXISTS schema_migrations(
                version INTEGER PRIMARY KEY,
                applied_at TEXT NOT NULL DEFAULT (datetime('now')))");
        }

        private static int ParseVersion(string resourceName)
        {
            // GroceryPos.Data.Migrations.001_initial.sql
            var file = resourceName.Substring(resourceName.IndexOf(".Migrations.") + ".Migrations.".Length);
            var num = new string(file.TakeWhile(char.IsDigit).ToArray());
            return int.Parse(num);
        }
    }
}
