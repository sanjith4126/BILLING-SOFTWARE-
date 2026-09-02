using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace GroceryPos.App
{
    /// <summary>
    /// Simple key/value settings editor. All settings live in the settings table.
    /// Scale-specific tuning happens on ScaleSetupForm.
    /// </summary>
    public class SettingsForm : Form
    {
        private readonly AppContext _ctx;
        private readonly DataGridView _grid;

        public SettingsForm(AppContext ctx)
        {
            _ctx = ctx;
            Text = "Settings";
            Width = 700; Height = 500;
            StartPosition = FormStartPosition.CenterParent;

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false
            };
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Key", DataPropertyName = "Key", Width = 240, ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Value", DataPropertyName = "Value", Width = 400 });

            var save = new Button { Text = "Save", Dock = DockStyle.Bottom, Height = 40 };
            save.Click += (s, e) => Save();
            Controls.Add(_grid);
            Controls.Add(save);
            Load += (s, e) => Reload();
            Theme.Retrofit(this);
        }

        private void Reload()
        {
            var list = new List<KV>();
            foreach (var kv in _ctx.Settings.GetAll())
                list.Add(new KV { Key = kv.Key, Value = kv.Value });
            list.Sort((a, b) => string.Compare(a.Key, b.Key, StringComparison.Ordinal));
            _grid.DataSource = list;
        }

        private void Save()
        {
            foreach (DataGridViewRow row in _grid.Rows)
            {
                var k = row.Cells[0].Value as string;
                var v = row.Cells[1].Value as string;
                if (!string.IsNullOrEmpty(k)) _ctx.Settings.Set(k, v ?? "");
            }
            MessageBox.Show("Saved");
        }

        private class KV { public string Key { get; set; } public string Value { get; set; } }
    }
}
