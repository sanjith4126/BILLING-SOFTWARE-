using System;
using System.Windows.Forms;

namespace GroceryPos.App
{
    public class MainMenuForm : Form
    {
        private readonly AppContext _ctx;

        public MainMenuForm(AppContext ctx)
        {
            _ctx = ctx;
            Text = "Grocery POS — " + ctx.CurrentUser.Name + " (" + ctx.CurrentUser.Role + ")";
            Width = 720; Height = 640;
            StartPosition = FormStartPosition.CenterScreen;

            var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), FlowDirection = FlowDirection.TopDown };
            flow.Controls.Add(Btn("1. Billing counter", () => new BillingForm(_ctx).ShowDialog()));
            flow.Controls.Add(Btn("2. Scale & weight setup", () => new ScaleSetupForm(_ctx).ShowDialog()));
            flow.Controls.Add(Btn("4. Item master", () => new ItemMasterForm(_ctx).ShowDialog()));
            flow.Controls.Add(Btn("5a. Stock summary", () => new StockSummaryForm(_ctx).ShowDialog()));
            flow.Controls.Add(Btn("5b. Stock take", () => new StockTakeForm(_ctx).ShowDialog()));
            flow.Controls.Add(Btn("5c. Damage / wastage", () => new WastageForm(_ctx).ShowDialog()));
            flow.Controls.Add(Btn("5d. Unit conversion (bag to loose)", () => new UnitConversionForm(_ctx).ShowDialog()));
            flow.Controls.Add(Btn("5e. Near-expiry report", () => new NearExpiryReportForm(_ctx).ShowDialog()));
            flow.Controls.Add(Btn("5f. Reorder report", () => new ReorderReportForm(_ctx).ShowDialog()));
            flow.Controls.Add(Btn("6a. Purchase entry", () => new PurchaseEntryForm(_ctx).ShowDialog()));
            flow.Controls.Add(Btn("6b. Purchase return", () => new PurchaseReturnForm(_ctx).ShowDialog()));
            flow.Controls.Add(Btn("7a. Customer khata (ledger)", () => new CustomerLedgerForm(_ctx).ShowDialog()));
            flow.Controls.Add(Btn("7b. Opening balance import", () => new OpeningBalanceImportForm(_ctx).ShowDialog()));
            flow.Controls.Add(Btn("7c. Ageing report", () => new AgeingReportForm(_ctx).ShowDialog()));
            flow.Controls.Add(Btn("8. Shift / day close", () => new ShiftForm(_ctx).ShowDialog()));
            flow.Controls.Add(Btn("9. Reports & GST", () => new ReportsMenuForm(_ctx).ShowDialog()));
            flow.Controls.Add(Btn("Settings", () => new SettingsForm(_ctx).ShowDialog()));
            flow.Controls.Add(Btn("Sign out", () => { Close(); }));
            Controls.Add(flow);
        }

        private Button Btn(string t, Action a)
        {
            var b = new Button { Text = t, Width = 320, Height = 36 };
            b.Click += (s, e) => a();
            return b;
        }
    }
}
