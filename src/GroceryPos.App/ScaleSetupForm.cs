using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using GroceryPos.Domain;
using GroceryPos.Hardware;

namespace GroceryPos.App
{
    /// <summary>
    /// Scale setup: mode, port config, raw dump viewer, detect cycler, regex tester,
    /// per-item tare / round-to / min-sale.
    /// Persists mode, port, baud, parity, data, stop, regex, poll_cmd via settings table.
    /// </summary>
    public class ScaleSetupForm : Form
    {
        private readonly AppContext _ctx;

        private ComboBox _mode, _port, _baud, _parity, _data, _stop;
        private TextBox _regex, _poll;
        private TextBox _rawDump;
        private TextBox _testFrame;
        private Label _testResult;
        private Timer _rawTimer;

        private SerialWeightSource _tmpSource; // used for live raw view

        private DataGridView _itemsGrid;

        public const string KeyMode = "scale.mode";
        public const string KeyPort = "scale.port";
        public const string KeyBaud = "scale.baud";
        public const string KeyParity = "scale.parity";
        public const string KeyDataBits = "scale.data_bits";
        public const string KeyStopBits = "scale.stop_bits";
        public const string KeyRegex = "scale.regex";
        public const string KeyPoll = "scale.poll_cmd";

        // Matches the ES 510 frame format captured on-site: NNN.NNN\r\n.
        // Also matches typical continuous scales that send just the decimal weight.
        public const string DefaultRegex = @"(?<value>\d+\.\d+)";

        public ScaleSetupForm(AppContext ctx)
        {
            _ctx = ctx;
            Text = "Scale & weight setup";
            Width = 900; Height = 640;
            StartPosition = FormStartPosition.CenterParent;

            var tabs = new TabControl { Dock = DockStyle.Fill };
            tabs.TabPages.Add(BuildDeviceTab());
            tabs.TabPages.Add(BuildItemsTab());
            Controls.Add(tabs);

            FormClosed += (s, e) => { StopLiveDump(); };
            Theme.Retrofit(this);
        }

        // ---------- Device tab ----------
        private TabPage BuildDeviceTab()
        {
            var tp = new TabPage("Device");

            int y = 12;
            AddLabel(tp, "Mode", 10, y);
            _mode = new ComboBox { Left = 120, Top = y, Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
            _mode.Items.AddRange(new object[] { "Serial", "Manual" });
            tp.Controls.Add(_mode);
            y += 30;

            AddLabel(tp, "Port", 10, y);
            _port = new ComboBox { Left = 120, Top = y, Width = 120 };
            foreach (var p in SerialPort.GetPortNames()) _port.Items.Add(p);
            tp.Controls.Add(_port);
            y += 30;

            AddLabel(tp, "Baud", 10, y);
            _baud = new ComboBox { Left = 120, Top = y, Width = 100, DropDownStyle = ComboBoxStyle.DropDownList };
            _baud.Items.AddRange(new object[] { "1200", "2400", "4800", "9600", "19200", "38400", "57600", "115200" });
            tp.Controls.Add(_baud);
            AddLabel(tp, "Data bits", 240, y);
            _data = new ComboBox { Left = 320, Top = y, Width = 60, DropDownStyle = ComboBoxStyle.DropDownList };
            _data.Items.AddRange(new object[] { "7", "8" });
            tp.Controls.Add(_data);
            AddLabel(tp, "Parity", 400, y);
            _parity = new ComboBox { Left = 460, Top = y, Width = 80, DropDownStyle = ComboBoxStyle.DropDownList };
            _parity.Items.AddRange(new object[] { "None", "Even", "Odd" });
            tp.Controls.Add(_parity);
            AddLabel(tp, "Stop", 550, y);
            _stop = new ComboBox { Left = 600, Top = y, Width = 60, DropDownStyle = ComboBoxStyle.DropDownList };
            _stop.Items.AddRange(new object[] { "1", "2" });
            tp.Controls.Add(_stop);
            y += 30;

            AddLabel(tp, "Regex", 10, y);
            _regex = new TextBox { Left = 120, Top = y, Width = 600 };
            tp.Controls.Add(_regex);
            y += 30;

            AddLabel(tp, "Poll cmd", 10, y);
            _poll = new TextBox { Left = 120, Top = y, Width = 200 };
            tp.Controls.Add(_poll);
            y += 30;

            var saveBtn = new Button { Text = "Save settings", Left = 120, Top = y, Width = 130 };
            saveBtn.Click += (s, e) => SaveSettings();
            tp.Controls.Add(saveBtn);
            var detectBtn = new Button { Text = "Detect", Left = 260, Top = y, Width = 100 };
            detectBtn.Click += (s, e) => Detect();
            tp.Controls.Add(detectBtn);
            var startBtn = new Button { Text = "Start live dump", Left = 370, Top = y, Width = 130 };
            startBtn.Click += (s, e) => StartLiveDump();
            tp.Controls.Add(startBtn);
            var stopBtn = new Button { Text = "Stop", Left = 510, Top = y, Width = 80 };
            stopBtn.Click += (s, e) => StopLiveDump();
            tp.Controls.Add(stopBtn);
            y += 40;

            AddLabel(tp, "Raw dump (hex / ASCII)", 10, y);
            y += 20;
            _rawDump = new TextBox
            {
                Left = 10, Top = y, Width = 860, Height = 160,
                Multiline = true, ScrollBars = ScrollBars.Vertical, ReadOnly = true,
                Font = new System.Drawing.Font("Consolas", 9F)
            };
            tp.Controls.Add(_rawDump);
            y += 170;

            AddLabel(tp, "Test frame", 10, y);
            _testFrame = new TextBox { Left = 120, Top = y, Width = 500 };
            tp.Controls.Add(_testFrame);
            var testBtn = new Button { Text = "Parse", Left = 630, Top = y - 2, Width = 80 };
            testBtn.Click += (s, e) => ParseTest();
            tp.Controls.Add(testBtn);
            y += 26;
            _testResult = new Label { Left = 120, Top = y, Width = 700, Height = 40 };
            tp.Controls.Add(_testResult);

            Load += (s, e) => LoadSettings();
            return tp;
        }

        private void AddLabel(Control parent, string text, int x, int y)
        {
            parent.Controls.Add(new Label { Text = text, Left = x, Top = y + 3, Width = 100 });
        }

        private void LoadSettings()
        {
            _mode.Text = NonEmpty(_ctx.Settings.Get(KeyMode, ""), "Manual");
            _port.Text = NonEmpty(_ctx.Settings.Get(KeyPort, ""), "COM1");
            _baud.Text = NonEmpty(_ctx.Settings.Get(KeyBaud, ""), "9600");
            _data.Text = NonEmpty(_ctx.Settings.Get(KeyDataBits, ""), "8");
            _parity.Text = NonEmpty(_ctx.Settings.Get(KeyParity, ""), "None");
            _stop.Text = NonEmpty(_ctx.Settings.Get(KeyStopBits, ""), "1");
            _regex.Text = NonEmpty(_ctx.Settings.Get(KeyRegex, ""), DefaultRegex);
            _poll.Text = _ctx.Settings.Get(KeyPoll, "") ?? "";
        }

        private static string NonEmpty(string v, string fallback)
        {
            return string.IsNullOrWhiteSpace(v) ? fallback : v;
        }

        private void SaveSettings()
        {
            _ctx.Settings.Set(KeyMode, NonEmpty(_mode.Text, "Manual"));
            _ctx.Settings.Set(KeyPort, NonEmpty(_port.Text, "COM1"));
            _ctx.Settings.Set(KeyBaud, NonEmpty(_baud.Text, "9600"));
            _ctx.Settings.Set(KeyDataBits, NonEmpty(_data.Text, "8"));
            _ctx.Settings.Set(KeyParity, NonEmpty(_parity.Text, "None"));
            _ctx.Settings.Set(KeyStopBits, NonEmpty(_stop.Text, "1"));
            _ctx.Settings.Set(KeyRegex, NonEmpty(_regex.Text, DefaultRegex));
            _ctx.Settings.Set(KeyPoll, _poll.Text ?? "");
            StopLiveDump();
            _ctx.RebuildWeightSource();
            MessageBox.Show("Scale settings saved and applied. F4 on the billing screen will now read from the scale.", "Saved");
        }

        // ---------- Detect ----------
        private void Detect()
        {
            var attempts = new List<Tuple<int, int, Parity, StopBits>>
            {
                Tuple.Create(9600, 8, Parity.None, StopBits.One),
                Tuple.Create(4800, 8, Parity.None, StopBits.One),
                Tuple.Create(2400, 8, Parity.None, StopBits.One),
                Tuple.Create(9600, 7, Parity.Even, StopBits.One),
            };
            string port = _port.Text;
            if (string.IsNullOrWhiteSpace(port))
            {
                MessageBox.Show("Choose which socket the scale is plugged into first.",
                    "Pick a port", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Detect opens the port itself, so the running application has to let
            // go of it first, exactly as the live dump does.
            StopLiveDump();
            ReleaseAppPort();

            var sb = new StringBuilder();
            foreach (var a in attempts)
            {
                sb.AppendLine("Trying " + a.Item1 + " " + a.Item2 + a.Item3.ToString()[0] + a.Item4);
                try
                {
                    using (var sp = new SerialPort(port, a.Item1, a.Item3, a.Item2, a.Item4))
                    {
                        sp.ReadTimeout = 2000;
                        sp.Open();
                        var buf = new byte[512];
                        var end = DateTime.Now.AddSeconds(2);
                        int total = 0; int printable = 0;
                        while (DateTime.Now < end)
                        {
                            try
                            {
                                int n = sp.Read(buf, 0, buf.Length);
                                for (int i = 0; i < n; i++)
                                {
                                    total++;
                                    if (buf[i] >= 32 && buf[i] <= 126) printable++;
                                }
                            }
                            catch (TimeoutException) { }
                        }
                        sb.AppendLine("  bytes=" + total + " printable=" + printable);
                        if (printable > 0 && printable * 100 / Math.Max(1, total) > 60)
                        {
                            sb.AppendLine("  *** looks good ***");
                        }
                    }
                }
                catch (Exception ex) { sb.AppendLine("  failed: " + ex.Message); }
            }
            _rawDump.Text = sb.ToString();
            RestoreAppPort();
        }

        // ---------- Live raw dump ----------
        private void StartLiveDump()
        {
            StopLiveDump();
            try
            {
                // A COM port can only be held by one thing at a time. Once the
                // scale is saved as Serial the main application keeps the port
                // open for billing, so opening it again here fails with
                // "access denied". Let go of it first and take it back in
                // StopLiveDump.
                ReleaseAppPort();

                int baud = int.Parse(_baud.Text);
                int data = int.Parse(_data.Text);
                Parity par = (Parity)Enum.Parse(typeof(Parity), _parity.Text);
                StopBits sb = _stop.Text == "2" ? StopBits.Two : StopBits.One;
                _tmpSource = new SerialWeightSource(_port.Text, baud, data, par, sb,
                    string.IsNullOrEmpty(_regex.Text) ? DefaultRegex : _regex.Text, _poll.Text);
                _tmpSource.Start();
                _rawTimer = new Timer { Interval = 300 };
                _rawTimer.Tick += (s, e) => RefreshDump();
                _rawTimer.Start();
            }
            catch (Exception ex)
            {
                RestoreAppPort();
                ShowPortProblem(_port.Text, ex);
            }
        }

        /// <summary>
        /// Drops the running application's grip on the serial port so this screen
        /// can test it. Billing falls back to manual entry meanwhile.
        /// </summary>
        private void ReleaseAppPort()
        {
            try
            {
                if (_ctx.WeightSource != null) _ctx.WeightSource.Dispose();
                _ctx.WeightSource = new ManualWeightSource();
                // Windows does not always free the handle instantly.
                System.Threading.Thread.Sleep(250);
            }
            catch { /* already closed */ }
        }

        /// <summary>Hands the port back to the application when testing stops.</summary>
        private void RestoreAppPort()
        {
            try { _ctx.RebuildWeightSource(); } catch { /* stays manual */ }
        }

        /// <summary>Explains a failed port open in words the shop owner can act on.</summary>
        private void ShowPortProblem(string port, Exception ex)
        {
            string msg;
            if (ex is UnauthorizedAccessException)
            {
                msg = port + " is being used by something else.\r\n" +
                      "Close any other program that reads the scale - a scale " +
                      "capture tool, PuTTY, or a second copy of this software - " +
                      "and try again.";
            }
            else if (ex is System.IO.IOException)
            {
                msg = port + " could not be opened.\r\n" +
                      "Check that the scale is switched on and its cable is " +
                      "pushed firmly into the 9-pin socket on the back of the " +
                      "computer, then try again.\r\n" +
                      "If the socket is not listed at all, try COM2 or COM3.";
            }
            else if (ex is ArgumentException)
            {
                msg = "\"" + port + "\" is not a port on this computer.\r\n" +
                      "Open the Port list and pick one that is shown there.";
            }
            else
            {
                msg = "The scale could not be read on " + port + ".\r\n" +
                      "Details: " + ex.Message;
            }
            MessageBox.Show(msg, "Cannot read the scale",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void RefreshDump()
        {
            if (_tmpSource == null) return;
            var frames = _tmpSource.RawFrames();
            var sb = new StringBuilder();
            foreach (var f in frames)
            {
                sb.Append(ToHex(f)).Append("   |   ").AppendLine(f);
            }
            _rawDump.Text = sb.ToString();
            _rawDump.SelectionStart = _rawDump.Text.Length;
            _rawDump.ScrollToCaret();
        }

        private static string ToHex(string s)
        {
            var sb = new StringBuilder();
            foreach (var c in s) sb.Append(((int)c).ToString("X2")).Append(' ');
            return sb.ToString();
        }

        private void StopLiveDump()
        {
            bool wasTesting = _tmpSource != null;
            try { if (_rawTimer != null) { _rawTimer.Stop(); _rawTimer.Dispose(); _rawTimer = null; } } catch { }
            try { if (_tmpSource != null) { _tmpSource.Dispose(); _tmpSource = null; } } catch { }
            // Give the port back to billing, or the counter is stuck on manual
            // entry until the software is restarted.
            if (wasTesting) RestoreAppPort();
        }

        // ---------- Regex test ----------
        private void ParseTest()
        {
            try
            {
                var s = new SerialWeightSource("COM_TEST", 9600, 8, Parity.None, StopBits.One,
                    string.IsNullOrEmpty(_regex.Text) ? DefaultRegex : _regex.Text, "");
                var r = s.TryParse(_testFrame.Text ?? "");
                if (!r.HasValue) { _testResult.Text = "No match."; return; }
                _testResult.Text = "Grams: " + r.Value.Grams + "   Stable: " + r.Value.Stable;
                s.Dispose();
            }
            catch (Exception ex) { _testResult.Text = "Error: " + ex.Message; }
        }

        // ---------- Items tab ----------
        private class ItemRow
        {
            public long Id { get; set; }
            public string Name { get; set; }
            public string SoldBy { get; set; }
            public int TareGrams { get; set; }
            public int RoundToGrams { get; set; }
            public int MinSaleGrams { get; set; }
        }

        private TabPage BuildItemsTab()
        {
            var tp = new TabPage("Per-item weight");
            _itemsGrid = new DataGridView
            {
                Dock = DockStyle.Fill, AutoGenerateColumns = false, AllowUserToAddRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect, RowHeadersVisible = false
            };
            _itemsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "ID", DataPropertyName = "Id", Width = 50, ReadOnly = true });
            _itemsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Name", DataPropertyName = "Name", Width = 260, ReadOnly = true });
            _itemsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Sold by", DataPropertyName = "SoldBy", Width = 80, ReadOnly = true });
            _itemsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Tare g", DataPropertyName = "TareGrams", Width = 80 });
            _itemsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Round g", DataPropertyName = "RoundToGrams", Width = 80 });
            _itemsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Min sale g", DataPropertyName = "MinSaleGrams", Width = 100 });

            var save = new Button { Text = "Save changes", Dock = DockStyle.Bottom, Height = 36 };
            save.Click += (s, e) => SaveItems();
            tp.Controls.Add(_itemsGrid);
            tp.Controls.Add(save);

            tp.Enter += (s, e) => ReloadItems();
            return tp;
        }

        private List<ItemRow> _itemRows = new List<ItemRow>();

        private void ReloadItems()
        {
            var items = _ctx.Items.Search("");
            _itemRows.Clear();
            foreach (var it in items.Where(x => x.SoldBy == SoldBy.Weight))
            {
                _itemRows.Add(new ItemRow
                {
                    Id = it.Id, Name = it.Name, SoldBy = it.SoldBy.ToString(),
                    TareGrams = it.TareGrams, RoundToGrams = it.RoundToGrams, MinSaleGrams = it.MinSaleGrams
                });
            }
            _itemsGrid.DataSource = null;
            _itemsGrid.DataSource = _itemRows;
        }

        private void SaveItems()
        {
            int updated = 0;
            foreach (var r in _itemRows)
            {
                var it = _ctx.Items.FindById(r.Id);
                if (it == null) continue;
                if (it.TareGrams == r.TareGrams && it.RoundToGrams == r.RoundToGrams && it.MinSaleGrams == r.MinSaleGrams) continue;
                it.TareGrams = r.TareGrams; it.RoundToGrams = r.RoundToGrams; it.MinSaleGrams = r.MinSaleGrams;
                _ctx.Items.Save(it, _ctx.CurrentUser.Id);
                updated++;
            }
            MessageBox.Show("Saved " + updated + " items", "Weight settings");
        }
    }
}
