using System;
using System.Data;
using System.Data.SQLite;

namespace GroceryPos.Data
{
    /// <summary>Connection factory. One SQLite file, one connection at a time — WAL enabled.</summary>
    public class Db
    {
        private readonly string _connString;

        public Db(string dbPath)
        {
            _connString = new SQLiteConnectionStringBuilder
            {
                DataSource = dbPath,
                ForeignKeys = true,
                JournalMode = SQLiteJournalModeEnum.Wal,
                SyncMode = SynchronizationModes.Normal
            }.ToString();
        }

        public IDbConnection Open()
        {
            var c = new SQLiteConnection(_connString);
            c.Open();
            // Ensure FKs and pragmas each connection
            using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = "PRAGMA foreign_keys=ON; PRAGMA journal_mode=WAL;";
                cmd.ExecuteNonQuery();
            }
            return c;
        }
    }
}
