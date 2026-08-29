using System;
using System.Drawing;
using System.Windows.Forms;

namespace GroceryPos.App
{
    public class LoginForm : Form
    {
        private readonly AppContext _ctx;
        private TextBox _name;
        private TextBox _pin;
        private Label _err;

        public LoginForm(AppContext ctx)
        {
            _ctx = ctx;
            Text = "Grocery POS — Sign in";
            Width = 360; Height = 240;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;

            var l1 = new Label { Text = "User name", Left = 20, Top = 20, Width = 90 };
            _name = new TextBox { Left = 120, Top = 18, Width = 200 };
            var l2 = new Label { Text = "PIN", Left = 20, Top = 60, Width = 90 };
            _pin = new TextBox { Left = 120, Top = 58, Width = 200, UseSystemPasswordChar = true };
            var ok = new Button { Text = "Sign in", Left = 120, Top = 100, Width = 100 };
            ok.Click += (s, e) => Try();
            AcceptButton = ok;
            _err = new Label { Left = 20, Top = 140, Width = 300, ForeColor = Color.Red };

            Controls.AddRange(new Control[] { l1, _name, l2, _pin, ok, _err });
            _name.Text = "owner";
        }

        private void Try()
        {
            var u = _ctx.Users.FindByName(_name.Text.Trim());
            if (u == null || !_ctx.Users.VerifyPin(u, _pin.Text))
            {
                _err.Text = "Invalid credentials";
                _pin.SelectAll(); _pin.Focus();
                return;
            }
            _ctx.CurrentUser = u;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
