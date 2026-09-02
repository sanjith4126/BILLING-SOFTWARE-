using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using GroceryPos.App;
using GroceryPos.Data;
using GroceryPos.Domain;
using GroceryPos.Printing;
using Xunit;
using AppContext = GroceryPos.App.AppContext;

namespace GroceryPos.Tests
{
    /// <summary>
    /// Constructs every screen and asserts it is laid out sanely. These catch the
    /// class of bug that only shows up on screen: a docked grid covering the
    /// toolbar above it, a label too short for the font inside it, or a grid that
    /// offers the user a phantom blank row it cannot save.
    ///
    /// The forms are shown off-screen and disposed; nothing is written to the DB.
    /// </summary>
    [Collection("UI")]
    public class FormLayoutTests : IDisposable
    {
        private readonly string _dbPath;
        private readonly AppContext _ctx;

        public FormLayoutTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), "layout_" + Guid.NewGuid().ToString("N") + ".sqlite");
            var db = new Db(_dbPath);
            new Migrator(db).Migrate();

            _ctx = new AppContext
            {
                Db = db,
                Users = new UserRepository(db),
                Settings = new SettingsRepository(db),
                Audit = new AuditLog(db),
                Categories = new CategoryRepository(db),
                Suppliers = new SupplierRepository(db)
            };
            _ctx.Items = new ItemRepository(db, _ctx.Audit);
            _ctx.Bills = new BillRepository(db, _ctx.Audit);
            _ctx.Customers = new CustomerRepository(db, _ctx.Audit);
            _ctx.CustomerLedger = new CustomerLedgerRepository(db, _ctx.Audit);
            _ctx.CreditLimits = new CreditLimitRepository(db);
            _ctx.CreditPayments = new CreditPaymentRepository(db, _ctx.Audit);
            _ctx.Batches = new BatchRepository(db);
            _ctx.StockLedger = new StockLedgerRepository(db, _ctx.Audit);
            _ctx.Purchases = new PurchaseRepository(db, _ctx.Audit);
            _ctx.Shifts = new ShiftRepository(db, _ctx.Audit);
            _ctx.Printer = new WindowsRawPrinter();

            long uid = _ctx.Users.Create("tester", "1234", UserRole.Owner);
            _ctx.CurrentUser = _ctx.Users.All().First(u => u.Id == uid);
        }

        public void Dispose()
        {
            try { File.Delete(_dbPath); } catch { }
        }

        // ---- helpers --------------------------------------------------------

        /// <summary>Builds and lays out a form without it appearing on screen.</summary>
        private static void Realise(Form f)
        {
            f.StartPosition = FormStartPosition.Manual;
            f.Location = new Point(-32000, -32000);   // off every real monitor
            f.ShowInTaskbar = false;
            f.Show();
            Application.DoEvents();
            f.PerformLayout();
        }

        private static IEnumerable<Control> Descendants(Control root)
        {
            foreach (Control c in root.Controls)
            {
                yield return c;
                foreach (var d in Descendants(c)) yield return d;
            }
        }

        // ---- the checks -----------------------------------------------------

        /// <summary>
        /// A Dock.Fill control added AFTER a Dock.Top/Bottom sibling is laid out
        /// first and swallows the whole client area, hiding the other. This is what
        /// hid the purchase-entry grid header behind its toolbar.
        /// </summary>
        private static void AssertNoDockOverlap(Form f)
        {
            foreach (var parent in new[] { (Control)f }.Concat(Descendants(f)))
            {
                var docked = parent.Controls.Cast<Control>()
                    .Where(c => c.Visible && c.Dock != DockStyle.None)
                    .ToList();
                if (docked.Count < 2) continue;

                var fills = docked.Where(c => c.Dock == DockStyle.Fill).ToList();
                var edges = docked.Where(c => c.Dock != DockStyle.Fill).ToList();

                foreach (var fill in fills)
                {
                    foreach (var edge in edges)
                    {
                        var a = fill.Bounds;
                        var b = edge.Bounds;
                        a.Intersect(b);
                        Assert.True(a.Width <= 0 || a.Height <= 0,
                            f.GetType().Name + ": docked Fill control '" + Describe(fill) +
                            "' overlaps '" + Describe(edge) + "' by " + a.Width + "x" + a.Height +
                            " px. Add the Fill child BEFORE the edge-docked ones.");
                    }
                }
            }
        }

        /// <summary>A label shorter than its own text is clipped on screen.</summary>
        private static void AssertLabelsFit(Form f)
        {
            foreach (var c in Descendants(f).OfType<Label>())
            {
                if (!c.Visible || c.AutoSize || string.IsNullOrEmpty(c.Text)) continue;
                if (c.Height <= 0 || c.Width <= 0) continue;

                // Measure against the label's own width so wrapped text counts too:
                // a multi-line explanation cut off at the bottom is a real defect,
                // and skipping multi-line labels would hide exactly that case.
                var needed = TextRenderer.MeasureText(
                    c.Text, c.Font,
                    new Size(c.Width, int.MaxValue),
                    TextFormatFlags.WordBreak);

                Assert.True(c.Height >= needed.Height,
                    f.GetType().Name + ": label \"" + Trim(c.Text) + "\" is " + c.Height +
                    "px tall but needs " + needed.Height + "px at " + c.Width +
                    "px wide. The text is clipped.");
            }
        }

        /// <summary>
        /// A read-only grid must not show the blank "new row" placeholder — it
        /// invites the user to type where nothing can be saved.
        /// </summary>
        private static void AssertNoPhantomRows(Form f)
        {
            foreach (var g in Descendants(f).OfType<DataGridView>())
            {
                if (g.ReadOnly)
                {
                    Assert.False(g.AllowUserToAddRows,
                        f.GetType().Name + ": a read-only grid still offers a blank new row.");
                }
            }
        }

        /// <summary>Every grid should use the shared navy header styling.</summary>
        private static void AssertGridsThemed(Form f)
        {
            foreach (var g in Descendants(f).OfType<DataGridView>())
            {
                Assert.True(g.EnableHeadersVisualStyles == false,
                    f.GetType().Name + ": a grid was not passed through Theme.ApplyGrid " +
                    "(EnableHeadersVisualStyles is still true, so the header stays grey).");
                Assert.Equal(Theme.Primary, g.ColumnHeadersDefaultCellStyle.BackColor);
            }
        }

        private static string Describe(Control c)
        {
            return c.GetType().Name + (string.IsNullOrEmpty(c.Name) ? "" : " '" + c.Name + "'");
        }

        private static string Trim(string s)
        {
            s = s.Replace("\r", " ").Replace("\n", " ");
            return s.Length > 40 ? s.Substring(0, 40) + "..." : s;
        }

        /// <summary>
        /// WinForms requires a single-threaded apartment; xunit runs tests on MTA
        /// worker threads, so each check runs on its own STA thread and any
        /// assertion failure is marshalled back.
        /// </summary>
        internal static void Sta(Action body)
        {
            Exception captured = null;
            var t = new System.Threading.Thread(() =>
            {
                try { body(); }
                catch (Exception ex) { captured = ex; }
            });
            t.SetApartmentState(System.Threading.ApartmentState.STA);
            t.Start();
            t.Join();
            if (captured != null)
                throw new Exception(captured.Message, captured);
        }

        private void Check(Func<Form> build)
        {
            Sta(() =>
            {
                using (var f = build())
                {
                    Realise(f);
                    AssertNoDockOverlap(f);
                    AssertLabelsFit(f);
                    AssertNoPhantomRows(f);
                    AssertGridsThemed(f);
                    f.Close();
                }
            });
        }

        // ---- one test per screen -------------------------------------------

        [Fact] public void MainMenu_LaysOutCleanly() { Check(() => new MainMenuForm(_ctx)); }
        [Fact] public void PurchaseEntry_LaysOutCleanly() { Check(() => new PurchaseEntryForm(_ctx)); }
        [Fact] public void PurchaseReturn_LaysOutCleanly() { Check(() => new PurchaseReturnForm(_ctx)); }
        [Fact] public void StockTake_LaysOutCleanly() { Check(() => new StockTakeForm(_ctx)); }
        [Fact] public void StockSummary_LaysOutCleanly() { Check(() => new StockSummaryForm(_ctx)); }
        [Fact] public void Wastage_LaysOutCleanly() { Check(() => new WastageForm(_ctx)); }
        [Fact] public void UnitConversion_LaysOutCleanly() { Check(() => new UnitConversionForm(_ctx)); }
        [Fact] public void NearExpiry_LaysOutCleanly() { Check(() => new NearExpiryReportForm(_ctx)); }
        [Fact] public void Reorder_LaysOutCleanly() { Check(() => new ReorderReportForm(_ctx)); }
        [Fact] public void ItemMaster_LaysOutCleanly() { Check(() => new ItemMasterForm(_ctx)); }
        [Fact] public void Shift_LaysOutCleanly() { Check(() => new ShiftForm(_ctx)); }
        [Fact] public void SupplierEdit_LaysOutCleanly() { Check(() => new SupplierEditForm()); }

        /// <summary>
        /// The specific crash from the screenshot: a purchase line whose item has
        /// not been chosen yet must not throw DataGridViewComboBoxCell errors.
        /// </summary>
        [Fact]
        public void PurchaseEntry_BlankItemRow_DoesNotRaiseComboBoxError()
        {
            Sta(() =>
            {
                using (var f = new PurchaseEntryForm(_ctx))
                {
                    Realise(f);
                    var grid = Descendants(f).OfType<DataGridView>().First();

                    bool errored = false;
                    grid.DataError += (s, e) => errored = true;

                    // A row is added on Load with ItemId = 0; force it to paint.
                    Assert.True(grid.Rows.Count > 0, "Purchase entry should start with one blank line.");
                    grid.Refresh();
                    Application.DoEvents();

                    Assert.False(errored,
                        "A purchase line with no item chosen still triggers a DataGridView error.");

                    // And the placeholder must be a real, selectable entry.
                    var col = (DataGridViewComboBoxColumn)grid.Columns[0];
                    var choices = (System.Collections.IEnumerable)col.DataSource;
                    Assert.Contains(choices.Cast<object>(),
                        o => (long)o.GetType().GetProperty("Id").GetValue(o, null) == 0L);

                    f.Close();
                }
            });
        }

        /// <summary>Stock take with no batches must explain itself, not show a blank grid.</summary>
        [Fact]
        public void StockTake_WithNoStock_ShowsAnExplanation()
        {
            Sta(() =>
            {
                using (var f = new StockTakeForm(_ctx))
                {
                    Realise(f);
                    var grid = Descendants(f).OfType<DataGridView>().First();

                    Assert.False(grid.AllowUserToAddRows,
                        "Stock take offered an editable blank row with no batch behind it.");

                    bool anyVisibleMessage = Descendants(f).OfType<Label>()
                        .Any(l => l.Visible &&
                                  (l.Text ?? "").IndexOf("no stock", StringComparison.OrdinalIgnoreCase) >= 0);
                    Assert.True(anyVisibleMessage,
                        "With no stock, the screen should say so rather than showing an empty table.");

                    f.Close();
                }
            });
        }
    }
}
