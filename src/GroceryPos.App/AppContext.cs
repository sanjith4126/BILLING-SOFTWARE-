using GroceryPos.Data;
using GroceryPos.Domain;

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
        public User CurrentUser;
    }
}
