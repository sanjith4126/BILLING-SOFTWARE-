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
    }
}
