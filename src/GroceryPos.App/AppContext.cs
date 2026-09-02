using System;
using System.IO.Ports;
using GroceryPos.Data;
using GroceryPos.Domain;
using GroceryPos.Hardware;
using GroceryPos.Printing;

namespace GroceryPos.App
{
    /// <summary>Simple service locator — one instance passed through forms.</summary>
    public class AppContext
    {
        public Db Db;
        public UserRepository Users;
        public SettingsRepository Settings;
        public AuditLog Audit;
        public CategoryRepository Categories;
        public SupplierRepository Suppliers;
        public ItemRepository Items;
        public BillRepository Bills;
        public CustomerRepository Customers;
        public CustomerLedgerRepository CustomerLedger;
        public CreditLimitRepository CreditLimits;
        public CreditPaymentRepository CreditPayments;
        public BatchRepository Batches;
        public StockLedgerRepository StockLedger;
        public PurchaseRepository Purchases;
        public ShiftRepository Shifts;
        public IReceiptPrinter Printer;
        public IWeightSource WeightSource;
        public User CurrentUser;

        /// <summary>
        /// Read scale settings from the DB and (re)build WeightSource accordingly.
        /// Called at startup and after Scale Setup saves changes.
        /// </summary>
        public void RebuildWeightSource()
        {
            try
            {
                var old = WeightSource;
                if (old != null) old.Dispose();
            }
            catch { /* ignore */ }

            string mode = (Settings.Get("scale.mode", "Manual") ?? "Manual").Trim().ToLowerInvariant();
            if (mode != "serial")
            {
                WeightSource = new ManualWeightSource();
                return;
            }

            try
            {
                string port = Settings.Get("scale.port", "COM1");
                int baud; if (!int.TryParse(Settings.Get("scale.baud", "9600"), out baud)) baud = 9600;
                int dataBits; if (!int.TryParse(Settings.Get("scale.data_bits", "8"), out dataBits)) dataBits = 8;
                Parity parity;
                if (!Enum.TryParse(Settings.Get("scale.parity", "None"), true, out parity)) parity = Parity.None;
                StopBits stopBits;
                string sb = Settings.Get("scale.stop_bits", "1");
                if (sb == "1") stopBits = StopBits.One;
                else if (sb == "1.5") stopBits = StopBits.OnePointFive;
                else if (sb == "2") stopBits = StopBits.Two;
                else stopBits = StopBits.One;
                string regex = Settings.Get("scale.regex", @"(?<value>\d+\.\d+)");
                string poll = Settings.Get("scale.poll_cmd", "");

                var src = new SerialWeightSource(port, baud, dataBits, parity, stopBits, regex, poll);
                src.Start();
                WeightSource = src;
            }
            catch (Exception)
            {
                // Any failure to open the port falls back to Manual so billing never stops.
                WeightSource = new ManualWeightSource();
            }
        }
    }
}
