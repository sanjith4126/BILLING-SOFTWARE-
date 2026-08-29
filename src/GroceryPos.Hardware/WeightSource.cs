using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using GroceryPos.Domain;

namespace GroceryPos.Hardware
{
    public struct WeightReading
    {
        public int Grams;
        public bool Stable;
        public DateTime At;
    }

    public class ParsedWeightBarcode
    {
        public string ItemCode;
        public int Grams;
    }

    public interface IWeightSource : IDisposable
    {
        WeightMode Mode { get; }
        event EventHandler<WeightReading> ReadingReceived;
        ParsedWeightBarcode ParseBarcode(string barcode);
        WeightReading? Latest { get; }
    }

    /// <summary>Manual weight entry. No hardware. The billing form calls SetManual().</summary>
    public class ManualWeightSource : IWeightSource
    {
        public WeightMode Mode { get { return WeightMode.Manual; } }
        public event EventHandler<WeightReading> ReadingReceived;
        public WeightReading? Latest { get; private set; }

        public void SetManual(int grams)
        {
            var r = new WeightReading { Grams = grams, Stable = true, At = DateTime.Now };
            Latest = r;
            var h = ReadingReceived;
            if (h != null) h(this, r);
        }

        public ParsedWeightBarcode ParseBarcode(string barcode) { return null; }
        public void Dispose() { }
    }

    /// <summary>
    /// Serial reader. Configurable regex; converts to integer grams immediately.
    /// Background thread; marshals via captured SynchronizationContext when available.
    /// </summary>
    public class SerialWeightSource : IWeightSource
    {
        private SerialPort _port;
        private readonly StringBuilder _buf = new StringBuilder();
        private readonly Regex _regex;
        private readonly SynchronizationContext _sync;
        private readonly LinkedList<string> _rawRing = new LinkedList<string>();
        private const int RingMax = 200;
        private readonly object _ringLock = new object();
        private int? _lastGrams;
        private DateTime _lastGramsAt;
        private WeightReading? _latest;
        private readonly string _pollCmd;

        public WeightMode Mode { get { return WeightMode.Serial; } }
        public event EventHandler<WeightReading> ReadingReceived;
        public WeightReading? Latest { get { return _latest; } }

        public SerialWeightSource(string portName, int baud, int dataBits, Parity parity, StopBits stopBits,
                                  string regex, string pollCmd)
        {
            _regex = new Regex(regex, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            _sync = SynchronizationContext.Current;
            _pollCmd = pollCmd;
            _port = new SerialPort(portName, baud, parity, dataBits, stopBits) { NewLine = "\r\n", ReadTimeout = 500 };
            _port.DataReceived += Port_DataReceived;
        }

        public void Start()
        {
            try
            {
                _port.Open();
                if (!string.IsNullOrEmpty(_pollCmd))
                {
                    // Poll on a small timer
                    var t = new Timer(_ =>
                    {
                        try { if (_port.IsOpen) _port.Write(_pollCmd); } catch { }
                    }, null, 500, 500);
                    _pollTimer = t;
                }
            }
            catch (IOException) { /* leave closed */ }
            catch (UnauthorizedAccessException) { /* leave closed */ }
        }

        private Timer _pollTimer;

        public IList<string> RawFrames()
        {
            lock (_ringLock) return new List<string>(_rawRing);
        }

        private void Port_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                string chunk = _port.ReadExisting();
                _buf.Append(chunk);
                while (true)
                {
                    string all = _buf.ToString();
                    int nl = all.IndexOfAny(new[] { '\r', '\n' });
                    if (nl < 0) break;
                    string frame = all.Substring(0, nl).Trim();
                    _buf.Remove(0, nl + 1);
                    if (string.IsNullOrEmpty(frame)) continue;
                    RecordRaw(frame);
                    var reading = TryParse(frame);
                    if (reading.HasValue)
                    {
                        _latest = reading.Value;
                        Emit(reading.Value);
                    }
                }
            }
            catch { /* swallow — serial noise is normal */ }
        }

        private void RecordRaw(string frame)
        {
            lock (_ringLock)
            {
                _rawRing.AddLast(frame);
                while (_rawRing.Count > RingMax) _rawRing.RemoveFirst();
            }
        }

        private void Emit(WeightReading r)
        {
            var h = ReadingReceived;
            if (h == null) return;
            if (_sync != null) _sync.Post(_ => h(this, r), null);
            else h(this, r);
        }

        internal WeightReading? TryParse(string frame)
        {
            var m = _regex.Match(frame);
            if (!m.Success) return null;
            string sign = m.Groups["sign"].Success ? m.Groups["sign"].Value : "";
            string val = m.Groups["value"].Success ? m.Groups["value"].Value : null;
            string unit = m.Groups["unit"].Success ? m.Groups["unit"].Value.ToLowerInvariant() : "kg";
            string status = m.Groups["status"].Success ? m.Groups["status"].Value.ToUpperInvariant() : null;
            if (val == null) return null;

            double d;
            if (!double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) return null;
            int grams;
            if (unit == "g") grams = (int)Math.Round(d);
            else grams = (int)Math.Round(d * 1000.0);
            if (sign == "-") grams = -grams;

            bool stable;
            if (status != null) stable = status.StartsWith("ST");
            else
            {
                // No stability marker: use 500ms same-value rule
                if (_lastGrams.HasValue && _lastGrams.Value == grams &&
                    (DateTime.Now - _lastGramsAt).TotalMilliseconds >= 500) stable = true;
                else stable = false;
                if (!_lastGrams.HasValue || _lastGrams.Value != grams)
                {
                    _lastGrams = grams;
                    _lastGramsAt = DateTime.Now;
                }
            }
            return new WeightReading { Grams = grams, Stable = stable, At = DateTime.Now };
        }

        public ParsedWeightBarcode ParseBarcode(string barcode) { return null; }

        public void Dispose()
        {
            try { if (_pollTimer != null) _pollTimer.Dispose(); } catch { }
            try { if (_port != null) { _port.Close(); _port.Dispose(); } } catch { }
        }
    }

    /// <summary>
    /// EAN-13 weight barcode parser. Default format: prefix(1) + item(5) + weight-grams(5) + check(1).
    /// Wraps the parser only; the source itself doesn't emit — the billing form calls ParseBarcode on scan.
    /// </summary>
    public class WeightBarcodeParser
    {
        public bool EmbedsPrice; // if true, digits 7-11 are price in paise

        public ParsedWeightBarcode Parse(string barcode)
        {
            if (string.IsNullOrEmpty(barcode) || barcode.Length != 13) return null;
            if (barcode[0] != '2') return null;
            if (barcode[1] < '0' || barcode[1] > '9') return null;
            foreach (var c in barcode) if (c < '0' || c > '9') return null;

            if (!Ean13Valid(barcode)) return null;
            string itemCode = barcode.Substring(1, 5);
            string qty = barcode.Substring(7, 5);
            int q;
            if (!int.TryParse(qty, out q)) return null;
            return new ParsedWeightBarcode { ItemCode = itemCode, Grams = EmbedsPrice ? 0 : q };
        }

        public static bool Ean13Valid(string s)
        {
            if (s.Length != 13) return false;
            int sum = 0;
            for (int i = 0; i < 12; i++)
            {
                int d = s[i] - '0';
                sum += (i % 2 == 0) ? d : d * 3;
            }
            int check = (10 - (sum % 10)) % 10;
            return check == (s[12] - '0');
        }
    }
}
