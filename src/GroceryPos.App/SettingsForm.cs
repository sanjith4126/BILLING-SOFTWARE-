using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Linq;
using System.Windows.Forms;

namespace GroceryPos.App
{
    /// <summary>
    /// Store setup. This is the screen the shop is configured from on day one,
    /// so it asks plain questions rather than exposing the settings table.
    ///
    /// The raw key/value grid is still available behind "Advanced" for anything
    /// not covered here. Scale tuning lives on ScaleSetupForm.
    /// </summary>
    public class SettingsForm : Form
    {
        private readonly AppContext _ctx;

        private TextBox _name, _addr1, _addr2, _phone, _gstin, _footer, _titleNoGst;
        private ComboBox _printer;
        private CheckBox _drawerEnabled;
        private ComboBox _drawerPin;
        private TextBox _counterId, _discountCap, _loyalty;
        private Label _gstinHint;

        public SettingsForm(AppContext ctx)
        {
            _ctx = ctx;
            Theme.ApplyForm(this);
            Text = "Settings - set up your shop";
            Width = 780; Height = 800;
            MinimumSize = new Size(700, 620);
            StartPosition = FormStartPosition.CenterScreen;

            var header = Theme.Header("Shop settings",
                "These details appear on every bill you print.");

            var body = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Theme.Surface,
                Padding = new Padding(Theme.Lg, Theme.Md, Theme.Lg, Theme.Md)
            };

            int y = 0;
            SectionTitle(body, "Shop details", ref y);
            _name = Field(body, "Shop name", "Printed at the top of every bill.", ref y);
            _addr1 = Field(body, "Address line 1", null, ref y);
            _addr2 = Field(body, "Address line 2", null, ref y);
            _phone = Field(body, "Phone number", null, ref y);
            _gstin = Field(body, "GSTIN", "Leave blank if you are not GST registered.", ref y);
            _gstin.TextChanged += (s, e) => UpdateGstinHint();
            _gstinHint = new Label
            {
                Font = Theme.Body, AutoSize = false, Height = 20,
                TextAlign = ContentAlignment.MiddleLeft
            };
            _gstinHint.SetBounds(0, y, 700, 20);
            body.Controls.Add(_gstinHint);
            y += 26;

            _titleNoGst = Field(body, "Bill title when no GSTIN",
                "Shown instead of TAX INVOICE. Usually CASH BILL.", ref y);
            _footer = Field(body, "Footer line on the bill", null, ref y);

            SectionTitle(body, "Receipt printer", ref y);
            var lblP = Theme.FieldLabel("Which printer prints your bills?");
            lblP.SetBounds(0, y, 460, 18);
            body.Controls.Add(lblP);
            // DropDownStyle must be set BEFORE the bounds; changing it afterwards
            // makes WinForms re-measure the control and collapse its width.
            _printer = new ComboBox
            {
                Font = Theme.Body,
                DropDownStyle = ComboBoxStyle.DropDown,   // allow a queue not installed yet
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.Surface
            };
            _printer.SetBounds(0, y + 19, 460, Theme.FieldHeight);
            body.Controls.Add(_printer);
            var testBtn = Theme.SecondaryButton("Print a test slip");
            testBtn.SetBounds(476, y + 19, 160, Theme.ButtonHeight);
            testBtn.Click += (s, e) => TestPrint();
            body.Controls.Add(testBtn);
            y += 52;
            Hint(body, "Pick the TVS RP 3230 from the list. Leave blank to turn printing off.", ref y);

            SectionTitle(body, "Cash drawer", ref y);
            _drawerEnabled = new CheckBox
            {
                Text = "A cash drawer is connected to the printer",
                AutoSize = true, Font = Theme.Body
            };
            _drawerEnabled.SetBounds(0, y, 460, 24);
            body.Controls.Add(_drawerEnabled);
            y += 30;
            var lblD = Theme.FieldLabel("Drawer pin");
            lblD.SetBounds(0, y, 200, 18);
            body.Controls.Add(lblD);
            _drawerPin = Theme.DropDown(200);
            _drawerPin.Items.AddRange(new object[] { "2", "5" });
            _drawerPin.SetBounds(0, y + 19, 200, Theme.FieldHeight);
            body.Controls.Add(_drawerPin);
            y += 52;
            Hint(body, "If the drawer does not open, change this from 2 to 5 and try again.", ref y);

            SectionTitle(body, "Counter rules", ref y);
            _counterId = Field(body, "Counter number",
                "Use 1 unless you run more than one billing machine.", ref y, 160);
            _discountCap = Field(body, "Discount a cashier may give without approval (%)",
                "Above this, a manager PIN is asked for.", ref y, 160);
            _loyalty = Field(body, "Loyalty points per Rs. 100 spent",
                "Set to 0 to turn loyalty points off.", ref y, 160);

            var footer = new Panel
            {
                Dock = DockStyle.Bottom, Height = 64, BackColor = Theme.Surface,
                Padding = new Padding(Theme.Md, Theme.Sm, Theme.Md, Theme.Sm)
            };
            var advanced = Theme.SecondaryButton("Advanced (all settings)");
            advanced.Width = 190; advanced.Height = 44; advanced.Dock = DockStyle.Left;
            advanced.Click += (s, e) => new RawSettingsForm(_ctx).ShowDialog(this);
            var cancel = Theme.SecondaryButton("Close");
            cancel.Width = 110; cancel.Height = 44; cancel.Dock = DockStyle.Right;
            cancel.Click += (s, e) => Close();
            var save = Theme.PrimaryButton("Save settings");
            save.Width = 170; save.Height = 44; save.Dock = DockStyle.Right;
            save.Click += (s, e) => Save();
            footer.Controls.Add(advanced);
            footer.Controls.Add(cancel);
            footer.Controls.Add(save);

            Controls.Add(body);
            Controls.Add(footer);
            Controls.Add(header);
            CancelButton = cancel;

            Load += (s, e) => Reload();
        }

        // ---- layout helpers -------------------------------------------------

        private static void SectionTitle(Panel host, string text, ref int y)
        {
            y += Theme.Md;
            var l = new Label
            {
                Text = text, Font = Theme.Headline, ForeColor = Theme.OnSurface,
                AutoSize = false, TextAlign = ContentAlignment.BottomLeft
            };
            l.SetBounds(0, y, 700, 26);
            host.Controls.Add(l);
            y += 28;
            var rule = new Panel { BackColor = Theme.Outline };
            rule.SetBounds(0, y, 700, 1);
            host.Controls.Add(rule);
            y += Theme.Sm + Theme.Xs;
        }

        private static void Hint(Panel host, string text, ref int y)
        {
            var l = new Label
            {
                Text = text, Font = Theme.Body, ForeColor = Theme.Muted,
                AutoSize = false, TextAlign = ContentAlignment.TopLeft
            };
            l.SetBounds(0, y, 700, 20);
            host.Controls.Add(l);
            y += 24;
        }

        private static TextBox Field(Panel host, string caption, string hint,
                                     ref int y, int width = 460)
        {
            var l = Theme.FieldLabel(caption);
            l.SetBounds(0, y, 700, 18);
            host.Controls.Add(l);

            var t = Theme.TextField(width);
            t.SetBounds(0, y + 19, width, Theme.FieldHeight);
            host.Controls.Add(t);
            y += 52;

            if (hint != null) Hint(host, hint, ref y);
            return t;
        }

        // ---- data -----------------------------------------------------------

        private void Reload()
        {
            _name.Text = _ctx.Settings.Get("store_name", "");
            _addr1.Text = _ctx.Settings.Get("store_address_1", "");
            _addr2.Text = _ctx.Settings.Get("store_address_2", "");
            _phone.Text = _ctx.Settings.Get("store_phone", "");
            _gstin.Text = _ctx.Settings.Get("store_gstin", "");
            _footer.Text = _ctx.Settings.Get("store_footer", "");
            _titleNoGst.Text = _ctx.Settings.Get("receipt_title_no_gst", "CASH BILL");

            LoadPrinters();
            _printer.Text = _ctx.Settings.Get("printer_name", "");

            _drawerEnabled.Checked = _ctx.Settings.Get("drawer_enabled", "0") == "1";
            _drawerPin.Text = _ctx.Settings.Get("drawer_pin", "2");

            _counterId.Text = _ctx.Settings.Get("counter_id", "1");
            _discountCap.Text = _ctx.Settings.Get("discount_cap_percent", "0");
            _loyalty.Text = _ctx.Settings.Get("loyalty_points_per_100rupees", "0");

            UpdateGstinHint();
        }

        /// <summary>
        /// Reads the installed Windows print queues. The queue name must never be
        /// hardcoded, and asking the owner to type it exactly is a support call
        /// waiting to happen.
        /// </summary>
        private void LoadPrinters()
        {
            _printer.Items.Clear();
            try
            {
                foreach (string p in PrinterSettings.InstalledPrinters)
                    _printer.Items.Add(p);
            }
            catch (Exception ex)
            {
                // Not fatal: the name can still be typed in.
                Theme.Warn("The list of installed printers could not be read.\r\n\r\n" +
                           "You can still type the printer name.\r\n\r\nDetails: " + ex.Message);
            }
        }

        private void UpdateGstinHint()
        {
            string g = (_gstin.Text ?? "").Trim();
            if (g.Length == 0)
            {
                _gstinHint.Text = "Bills will be titled \"" +
                    (_titleNoGst == null ? "CASH BILL" : _titleNoGst.Text) + "\".";
                _gstinHint.ForeColor = Theme.Muted;
            }
            else if (g.Length == 15)
            {
                _gstinHint.Text = "Bills will be titled \"TAX INVOICE\".";
                _gstinHint.ForeColor = Theme.Success;
            }
            else
            {
                _gstinHint.Text = "A GSTIN is 15 characters. This one has " + g.Length + ".";
                _gstinHint.ForeColor = Theme.Danger;
            }
        }

        private void TestPrint()
        {
            string queue = (_printer.Text ?? "").Trim();
            if (queue.Length == 0)
            {
                Theme.Warn("Choose a printer first.");
                return;
            }
            try
            {
                var lines = new List<string>
                {
                    GroceryPos.Printing.ReceiptFormatter.Center(
                        string.IsNullOrWhiteSpace(_name.Text) ? "TEST" : _name.Text),
                    new string('-', GroceryPos.Printing.ReceiptFormatter.Width),
                    "123456789012345678901234567890123456789012345678",
                    "If the line above fits exactly, the width is right.",
                    new string('-', GroceryPos.Printing.ReceiptFormatter.Width),
                    GroceryPos.Printing.ReceiptFormatter.PadPair("Test amount", "Rs. 1234.50"),
                    ""
                };
                _ctx.Printer.Print(queue, GroceryPos.Printing.EscPos.Build(lines, true, false, 0));
                Theme.Info(
                    "A test slip was sent to \"" + queue + "\".\r\n\r\n" +
                    "Check that the row of digits fits the paper exactly with no wrap.",
                    "Test slip sent");
            }
            catch (Exception ex)
            {
                Theme.Error("The test slip could not be printed.\r\n\r\nDetails: " + ex.Message);
            }
        }

        private void Save()
        {
            int counter, cap, loyalty;
            if (!int.TryParse((_counterId.Text ?? "1").Trim(), out counter) || counter < 1)
            {
                Theme.Warn("Counter number must be 1 or more.");
                _counterId.Focus();
                return;
            }
            if (!int.TryParse((_discountCap.Text ?? "0").Trim(), out cap) || cap < 0 || cap > 100)
            {
                Theme.Warn("The discount limit must be between 0 and 100 percent.");
                _discountCap.Focus();
                return;
            }
            if (!int.TryParse((_loyalty.Text ?? "0").Trim(), out loyalty) || loyalty < 0)
            {
                Theme.Warn("Loyalty points must be 0 or more.");
                _loyalty.Focus();
                return;
            }

            string gstin = (_gstin.Text ?? "").Trim();
            if (gstin.Length > 0 && gstin.Length != 15)
            {
                if (!Theme.Confirm(
                    "A GSTIN is normally 15 characters and this one has " + gstin.Length + ".\r\n\r\n" +
                    "An incorrect GSTIN on a tax invoice is a compliance problem. Save it anyway?",
                    "Check the GSTIN"))
                    return;
            }

            _ctx.Settings.Set("store_name", (_name.Text ?? "").Trim());
            _ctx.Settings.Set("store_address_1", (_addr1.Text ?? "").Trim());
            _ctx.Settings.Set("store_address_2", (_addr2.Text ?? "").Trim());
            _ctx.Settings.Set("store_phone", (_phone.Text ?? "").Trim());
            _ctx.Settings.Set("store_gstin", gstin);
            _ctx.Settings.Set("store_footer", (_footer.Text ?? "").Trim());
            _ctx.Settings.Set("receipt_title_no_gst",
                string.IsNullOrWhiteSpace(_titleNoGst.Text) ? "CASH BILL" : _titleNoGst.Text.Trim());

            _ctx.Settings.Set("printer_name", (_printer.Text ?? "").Trim());
            _ctx.Settings.Set("drawer_enabled", _drawerEnabled.Checked ? "1" : "0");
            _ctx.Settings.Set("drawer_pin", string.IsNullOrWhiteSpace(_drawerPin.Text) ? "2" : _drawerPin.Text);

            _ctx.Settings.Set("counter_id", counter.ToString());
            _ctx.Settings.Set("discount_cap_percent", cap.ToString());
            _ctx.Settings.Set("loyalty_points_per_100rupees", loyalty.ToString());

            Theme.Info("Settings saved.\r\n\r\nThe new details appear on the next bill you print.",
                       "Saved");
            Close();
        }
    }

    /// <summary>
    /// The original raw settings table, kept for anything the guided screen does
    /// not cover. Not the first thing a shop owner should meet.
    /// </summary>
    public class RawSettingsForm : Form
    {
        private readonly AppContext _ctx;
        private readonly DataGridView _grid;

        public RawSettingsForm(AppContext ctx)
        {
            _ctx = ctx;
            Theme.ApplyForm(this);
            Text = "All settings";
            Width = 760; Height = 560;
            MinimumSize = new Size(600, 400);
            StartPosition = FormStartPosition.CenterParent;

            var header = Theme.Header("All settings",
                "Every stored value. Change these only if you know what they do.");

            var body = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(Theme.Md),
                BackColor = Theme.Background
            };
            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false
            };
            Theme.ApplyGrid(_grid);
            var keyCol = Theme.TextColumn("Key", "Setting", 260);
            keyCol.ReadOnly = true;
            _grid.Columns.Add(keyCol);
            var valCol = Theme.TextColumn("Value", "Value", 380);
            valCol.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            Theme.MarkEditable(valCol);
            _grid.Columns.Add(valCol);
            body.Controls.Add(_grid);

            var footer = new Panel
            {
                Dock = DockStyle.Bottom, Height = 60, BackColor = Theme.Surface,
                Padding = new Padding(Theme.Md, Theme.Sm, Theme.Md, Theme.Sm)
            };
            var cancel = Theme.SecondaryButton("Close");
            cancel.Width = 110; cancel.Height = 40; cancel.Dock = DockStyle.Right;
            cancel.Click += (s, e) => Close();
            var save = Theme.PrimaryButton("Save");
            save.Width = 140; save.Height = 40; save.Dock = DockStyle.Right;
            save.Click += (s, e) => Save();
            footer.Controls.Add(cancel);
            footer.Controls.Add(save);

            Controls.Add(body);
            Controls.Add(footer);
            Controls.Add(header);
            CancelButton = cancel;

            Load += (s, e) => Reload();
        }

        private void Reload()
        {
            var list = _ctx.Settings.GetAll()
                .Select(kv => new KV { Key = kv.Key, Value = kv.Value })
                .OrderBy(k => k.Key, StringComparer.Ordinal)
                .ToList();
            _grid.DataSource = new System.ComponentModel.BindingList<KV>(list);
        }

        private void Save()
        {
            _grid.EndEdit();
            foreach (DataGridViewRow row in _grid.Rows)
            {
                var kv = row.DataBoundItem as KV;
                if (kv == null || string.IsNullOrEmpty(kv.Key)) continue;
                _ctx.Settings.Set(kv.Key, kv.Value ?? "");
            }
            Theme.Info("Settings saved.", "Saved");
            Close();
        }

        private class KV
        {
            public string Key { get; set; }
            public string Value { get; set; }
        }
    }
}
