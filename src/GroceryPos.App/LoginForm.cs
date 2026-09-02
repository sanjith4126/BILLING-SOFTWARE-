using System;
using System.Drawing;
using System.Windows.Forms;

namespace GroceryPos.App
{
    /// <summary>
    /// Sign-in. Deliberately plain: a name and a PIN, with the PIN focused so a
    /// cashier can start typing the moment the machine is switched on.
    /// </summary>
    public class LoginForm : Form
    {
        private readonly AppContext _ctx;
        private ComboBox _name;
        private TextBox _pin;
        private Label _err;

        public LoginForm(AppContext ctx)
        {
            _ctx = ctx;
            Theme.ApplyForm(this);
            Text = "Grocery POS - Sign in";
            Width = 460; Height = 380;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;

            var storeName = ctx.Settings != null ? ctx.Settings.Get("store_name", "GROCERY STORE") : "GROCERY STORE";
            var header = Theme.Header(storeName, "Sign in to start");

            var body = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface, Padding = new Padding(Theme.Lg) };

            var l1 = Theme.FieldLabel("Who is signing in?");
            l1.SetBounds(Theme.Lg + 12, 20, 360, 18);
            // A dropdown of real accounts removes the chance of a typo in a name.
            _name = Theme.DropDown(360);
            _name.SetBounds(Theme.Lg + 12, 40, 360, Theme.FieldHeight);

            var l2 = Theme.FieldLabel("PIN");
            l2.SetBounds(Theme.Lg + 12, 84, 360, 18);
            _pin = Theme.TextField(360);
            _pin.SetBounds(Theme.Lg + 12, 104, 360, 34);
            _pin.UseSystemPasswordChar = true;
            _pin.Font = new Font(Theme.Data.FontFamily, 14f);

            var ok = Theme.PrimaryButton("Sign in");
            ok.SetBounds(Theme.Lg + 12, 152, 360, 44);
            ok.Click += (s, e) => Try();
            AcceptButton = ok;

            _err = new Label
            {
                ForeColor = Theme.Danger,
                Font = Theme.BodyBold,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter
            };
            _err.SetBounds(Theme.Lg + 12, 204, 360, 40);

            body.Controls.AddRange(new Control[] { l1, _name, l2, _pin, ok, _err });

            Controls.Add(body);
            Controls.Add(header);

            Load += (s, e) => LoadUsers();
            Shown += (s, e) => _pin.Focus();
        }

        private void LoadUsers()
        {
            try
            {
                var users = _ctx.Users.All();
                _name.DisplayMember = "Name";
                _name.ValueMember = "Id";
                _name.DataSource = users;
                if (users.Count > 0) _name.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                _err.Text = "Could not read the user list.";
                Theme.Error("The database could not be opened.\r\n\r\nDetails: " + ex.Message);
            }
        }

        private void Try()
        {
            var picked = _name.SelectedItem as GroceryPos.Domain.User;
            if (picked == null)
            {
                _err.Text = "Choose who is signing in.";
                return;
            }
            if (string.IsNullOrEmpty(_pin.Text))
            {
                _err.Text = "Enter your PIN.";
                _pin.Focus();
                return;
            }

            var u = _ctx.Users.FindByName(picked.Name);
            if (u == null || !_ctx.Users.VerifyPin(u, _pin.Text))
            {
                _err.Text = "That PIN is not correct. Try again.";
                _pin.SelectAll();
                _pin.Focus();
                return;
            }

            _ctx.CurrentUser = u;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
