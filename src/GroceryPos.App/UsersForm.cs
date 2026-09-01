using System;
using System.Linq;
using System.Windows.Forms;
using GroceryPos.Domain;

namespace GroceryPos.App
{
    /// <summary>
    /// User management. Owner-only for changing roles / adding / disabling.
    /// Any user may change their own PIN.
    /// </summary>
    public class UsersForm : Form
    {
        private readonly AppContext _ctx;
        private readonly DataGridView _grid;

        public UsersForm(AppContext ctx)
        {
            _ctx = ctx;
            Text = "Users (staff logins)";
            Width = 640; Height = 480;
            StartPosition = FormStartPosition.CenterParent;

            _grid = new DataGridView
            {
                Dock = DockStyle.Top,
                Height = 340,
                AllowUserToAddRows = false,
                ReadOnly = true,
                AutoGenerateColumns = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect
            };
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Id", DataPropertyName = "Id", Width = 40 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Name", DataPropertyName = "Name", Width = 200 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Role", DataPropertyName = "Role", Width = 100 });
            _grid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Active", DataPropertyName = "IsActive", Width = 80 });
            Controls.Add(_grid);

            var panel = new Panel { Dock = DockStyle.Fill };
            var addBtn = new Button { Text = "Add new user (owner)", Left = 10, Top = 8, Width = 200 };
            addBtn.Click += (s, e) => AddUser();
            var pinBtn = new Button { Text = "Change PIN", Left = 220, Top = 8, Width = 140 };
            pinBtn.Click += (s, e) => ChangePin();
            var roleBtn = new Button { Text = "Change role (owner)", Left = 370, Top = 8, Width = 160 };
            roleBtn.Click += (s, e) => ChangeRole();
            var actBtn = new Button { Text = "Enable / Disable (owner)", Left = 10, Top = 42, Width = 200 };
            actBtn.Click += (s, e) => ToggleActive();
            var closeBtn = new Button { Text = "Close", Left = 540, Top = 8, Width = 80 };
            closeBtn.Click += (s, e) => Close();
            panel.Controls.AddRange(new Control[] { addBtn, pinBtn, roleBtn, actBtn, closeBtn });
            Controls.Add(panel);

            Load += (s, e) => Refresh();
        }

        private new void Refresh()
        {
            var rows = _ctx.Users.All()
                .Select(u => new { u.Id, u.Name, Role = u.Role.ToString(), u.IsActive })
                .ToList();
            _grid.DataSource = rows;
        }

        private void AddUser()
        {
            if (_ctx.CurrentUser.Role != UserRole.Owner)
            { MessageBox.Show("Owner only."); return; }

            string name = Microsoft.VisualBasic.Interaction.InputBox(
                "New user name (e.g. ramesh, priya):", "Add user", "");
            if (string.IsNullOrWhiteSpace(name)) return;
            name = name.Trim().ToLowerInvariant();

            if (_ctx.Users.FindByName(name) != null)
            { MessageBox.Show("A user with that name already exists."); return; }

            string pin = Microsoft.VisualBasic.Interaction.InputBox(
                "4-digit PIN for " + name + ":", "PIN", "");
            if (string.IsNullOrWhiteSpace(pin) || pin.Length < 4)
            { MessageBox.Show("PIN must be at least 4 digits."); return; }

            string roleStr = Microsoft.VisualBasic.Interaction.InputBox(
                "Role — type: cashier, manager, or owner", "Role", "cashier");
            UserRole role;
            switch ((roleStr ?? "").Trim().ToLowerInvariant())
            {
                case "owner": role = UserRole.Owner; break;
                case "manager": role = UserRole.Manager; break;
                default: role = UserRole.Cashier; break;
            }

            long id = _ctx.Users.Create(name, pin, role);
            _ctx.Audit.Write(_ctx.CurrentUser.Id, "user_create", "user", id, null, new { name, role = role.ToString() });
            MessageBox.Show("User '" + name + "' created (" + role + ").");
            Refresh();
        }

        private void ChangePin()
        {
            if (_grid.CurrentRow == null) { MessageBox.Show("Pick a user."); return; }
            long id = (long)_grid.CurrentRow.Cells["Id"].Value;
            string name = (string)_grid.CurrentRow.Cells["Name"].Value;

            // A cashier can only change their own PIN. Owner/manager can change anyone's.
            if (id != _ctx.CurrentUser.Id && _ctx.CurrentUser.Role == UserRole.Cashier)
            { MessageBox.Show("Cashiers can only change their own PIN."); return; }

            string pin = Microsoft.VisualBasic.Interaction.InputBox(
                "New PIN for " + name + " (min 4 digits):", "Change PIN", "");
            if (string.IsNullOrWhiteSpace(pin) || pin.Length < 4)
            { MessageBox.Show("PIN must be at least 4 digits."); return; }

            _ctx.Users.SetPin(id, pin);
            _ctx.Audit.Write(_ctx.CurrentUser.Id, "user_pin_change", "user", id, null, null);
            MessageBox.Show("PIN updated for " + name + ".");
        }

        private void ChangeRole()
        {
            if (_ctx.CurrentUser.Role != UserRole.Owner)
            { MessageBox.Show("Owner only."); return; }
            if (_grid.CurrentRow == null) { MessageBox.Show("Pick a user."); return; }
            long id = (long)_grid.CurrentRow.Cells["Id"].Value;
            string name = (string)_grid.CurrentRow.Cells["Name"].Value;

            string roleStr = Microsoft.VisualBasic.Interaction.InputBox(
                "New role for " + name + " — cashier, manager, or owner:", "Change role", "cashier");
            UserRole role;
            switch ((roleStr ?? "").Trim().ToLowerInvariant())
            {
                case "owner": role = UserRole.Owner; break;
                case "manager": role = UserRole.Manager; break;
                case "cashier": role = UserRole.Cashier; break;
                default: MessageBox.Show("Invalid role."); return;
            }
            _ctx.Users.SetRole(id, role);
            _ctx.Audit.Write(_ctx.CurrentUser.Id, "user_role_change", "user", id, null, new { role = role.ToString() });
            MessageBox.Show("Role updated.");
            Refresh();
        }

        private void ToggleActive()
        {
            if (_ctx.CurrentUser.Role != UserRole.Owner)
            { MessageBox.Show("Owner only."); return; }
            if (_grid.CurrentRow == null) { MessageBox.Show("Pick a user."); return; }
            long id = (long)_grid.CurrentRow.Cells["Id"].Value;
            string name = (string)_grid.CurrentRow.Cells["Name"].Value;
            bool active = (bool)_grid.CurrentRow.Cells["IsActive"].Value;

            if (id == _ctx.CurrentUser.Id)
            { MessageBox.Show("You cannot disable yourself."); return; }

            _ctx.Users.SetActive(id, !active);
            _ctx.Audit.Write(_ctx.CurrentUser.Id, active ? "user_disable" : "user_enable", "user", id, null, null);
            MessageBox.Show(name + " is now " + (active ? "disabled" : "enabled") + ".");
            Refresh();
        }
    }
}
