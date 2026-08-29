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
            Width = 700; Height = 480;
            StartPosition = FormStartPosition.CenterScreen;

            var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), FlowDirection = FlowDirection.TopDown };
            flow.Controls.Add(MakeBtn("1. Billing counter (F1)", () => new BillingStubForm(_ctx).ShowDialog()));
            flow.Controls.Add(MakeBtn("2. Scale & weight setup", () => new ScaleSetupStubForm(_ctx).ShowDialog()));
            flow.Controls.Add(MakeBtn("4. Item master", () => new ItemMasterForm(_ctx).ShowDialog()));
            flow.Controls.Add(MakeBtn("5. Stock & inventory", () => Msg("Stock — Phase 4 stub")));
            flow.Controls.Add(MakeBtn("6. Purchase entry", () => Msg("Purchase — Phase 4 stub")));
            flow.Controls.Add(MakeBtn("7. Customer khata", () => Msg("Khata — Phase 5 stub")));
            flow.Controls.Add(MakeBtn("8. Day close / cash tally", () => Msg("Shift — Phase 5 stub")));
            flow.Controls.Add(MakeBtn("9. Reports & GST", () => Msg("Reports — Phase 6 stub")));
            flow.Controls.Add(MakeBtn("Settings", () => new SettingsForm(_ctx).ShowDialog()));
            flow.Controls.Add(MakeBtn("Sign out", () => { Close(); }));
            Controls.Add(flow);
        }

        private Button MakeBtn(string t, Action a)
        {
            var b = new Button { Text = t, Width = 300, Height = 40 };
            b.Click += (s, e) => a();
            return b;
        }

        private void Msg(string s) { MessageBox.Show(s); }
    }
}
