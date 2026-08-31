using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Threading;

namespace ScaleCapture
{
    internal static class Program
    {
        private static StreamWriter _file;

        private struct SerialSetting
        {
            public int Baud;
            public int DataBits;
            public Parity Parity;
            public StopBits StopBits;

            public SerialSetting(int baud, int dataBits, Parity parity, StopBits stopBits)
            {
                Baud = baud;
                DataBits = dataBits;
                Parity = parity;
                StopBits = stopBits;
            }

            public override string ToString()
            {
                string p;
                switch (Parity)
                {
                    case Parity.None: p = "N"; break;
                    case Parity.Even: p = "E"; break;
                    case Parity.Odd: p = "O"; break;
                    default: p = Parity.ToString(); break;
                }
                string s;
                switch (StopBits)
                {
                    case StopBits.One: s = "1"; break;
                    case StopBits.Two: s = "2"; break;
                    case StopBits.OnePointFive: s = "1.5"; break;
                    default: s = StopBits.ToString(); break;
                }
                return Baud + "-" + DataBits + "-" + p + "-" + s;
            }
        }

        private static readonly SerialSetting[] Sweep = new[]
        {
            new SerialSetting(9600, 8, Parity.None, StopBits.One),
            new SerialSetting(4800, 8, Parity.None, StopBits.One),
            new SerialSetting(2400, 8, Parity.None, StopBits.One),
            new SerialSetting(9600, 7, Parity.Even, StopBits.One),
        };

        private struct Probe
        {
            public string Name;
            public byte[] Bytes;
            public Probe(string name, byte[] bytes) { Name = name; Bytes = bytes; }
        }

        private static readonly Probe[] Probes = new[]
        {
            new Probe("'P'",  new byte[] { (byte)'P' }),
            new Probe("'W'",  new byte[] { (byte)'W' }),
            new Probe("ENQ",  new byte[] { 0x05 }),
            new Probe("CR",   new byte[] { 0x0D }),
        };

        internal static void Main(string[] args)
        {
            string logPath = Path.Combine(Environment.CurrentDirectory, "capture.txt");
            _file = new StreamWriter(logPath, append: true, encoding: new UTF8Encoding(false));
            _file.AutoFlush = true;

            Log("");
            Log("================================================================");
            Log("ScaleCapture run started " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            Log("Log file: " + logPath);
            Log("================================================================");

            string port = PickPort();
            if (port == null)
            {
                Log("No port chosen. Exiting.");
                return;
            }

            var results = new Dictionary<string, int>();
            SerialSetting best = Sweep[0];
            int bestBytes = -1;

            foreach (var s in Sweep)
            {
                Log("");
                Log("---- Trying " + port + " @ " + s + " ----");

                int got = ListenOnce(port, s, TimeSpan.FromSeconds(3));
                results[s.ToString()] = got;

                if (got == 0)
                {
                    Log("  (no bytes streamed) probing...");
                    foreach (var probe in Probes)
                    {
                        int p = ProbeOnce(port, s, probe);
                        if (p > 0)
                        {
                            Log("  -> probe " + probe.Name + " produced " + p + " bytes");
                            results[s + " + " + probe.Name] = p;
                            got = Math.Max(got, p);
                        }
                        else
                        {
                            Log("  -> probe " + probe.Name + " no response");
                        }
                    }
                }

                if (got > bestBytes)
                {
                    bestBytes = got;
                    best = s;
                }
            }

            Log("");
            Log("---- Sweep summary ----");
            foreach (var kv in results) Log("  " + kv.Key + " : " + kv.Value + " bytes");
            Log("Best setting: " + best + " (" + bestBytes + " bytes)");

            Log("");
            Log("Entering LIVE mode on " + port + " @ " + best + ". Put weights on the pan.");
            Log("Press Enter to stop.");
            LiveMode(port, best);

            Log("");
            Log("ScaleCapture done " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            _file.Close();
        }

        private static string PickPort()
        {
            string[] ports = SerialPort.GetPortNames();
            Array.Sort(ports);
            Log("");
            Log("Detected COM ports:");
            if (ports.Length == 0) Log("  (none)");
            for (int i = 0; i < ports.Length; i++) Log("  [" + i + "] " + ports[i]);

            Console.Write("Enter port name [COM1]: ");
            string input = Console.ReadLine();
            if (input == null) return null;
            input = input.Trim();
            if (input.Length == 0) input = "COM1";
            Log("Using port: " + input);
            return input;
        }

        private static int ListenOnce(string portName, SerialSetting s, TimeSpan duration)
        {
            SerialPort sp = null;
            try
            {
                sp = Open(portName, s);
                if (sp == null) return 0;
                return ReadAndLog(sp, duration);
            }
            finally
            {
                Close(sp);
            }
        }

        private static int ProbeOnce(string portName, SerialSetting s, Probe probe)
        {
            SerialPort sp = null;
            try
            {
                sp = Open(portName, s);
                if (sp == null) return 0;
                try
                {
                    sp.Write(probe.Bytes, 0, probe.Bytes.Length);
                }
                catch (Exception ex)
                {
                    Log("    write failed: " + ex.Message);
                    return 0;
                }
                return ReadAndLog(sp, TimeSpan.FromSeconds(1));
            }
            finally
            {
                Close(sp);
            }
        }

        private static SerialPort Open(string portName, SerialSetting s)
        {
            try
            {
                var sp = new SerialPort(portName, s.Baud, s.Parity, s.DataBits, s.StopBits);
                sp.ReadTimeout = 100;
                sp.WriteTimeout = 500;
                sp.Handshake = Handshake.None;
                sp.DtrEnable = true;
                sp.RtsEnable = true;
                sp.Open();
                sp.DiscardInBuffer();
                return sp;
            }
            catch (UnauthorizedAccessException ex)
            {
                Log("  open failed (in use / access denied): " + ex.Message);
                return null;
            }
            catch (IOException ex)
            {
                Log("  open failed (I/O): " + ex.Message);
                return null;
            }
            catch (ArgumentException ex)
            {
                Log("  open failed (bad argument): " + ex.Message);
                return null;
            }
            catch (Exception ex)
            {
                Log("  open failed: " + ex.GetType().Name + ": " + ex.Message);
                return null;
            }
        }

        private static void Close(SerialPort sp)
        {
            if (sp == null) return;
            try { if (sp.IsOpen) sp.Close(); } catch { }
            try { sp.Dispose(); } catch { }
        }

        private static int ReadAndLog(SerialPort sp, TimeSpan duration)
        {
            var buf = new byte[512];
            var line = new List<byte>(64);
            int total = 0;
            DateTime end = DateTime.UtcNow + duration;

            while (DateTime.UtcNow < end)
            {
                int available;
                try { available = sp.BytesToRead; }
                catch (Exception ex) { Log("    read failed: " + ex.Message); break; }

                if (available <= 0)
                {
                    Thread.Sleep(20);
                    continue;
                }

                int n;
                try { n = sp.Read(buf, 0, Math.Min(buf.Length, available)); }
                catch (TimeoutException) { continue; }
                catch (Exception ex) { Log("    read failed: " + ex.Message); break; }

                for (int i = 0; i < n; i++)
                {
                    byte b = buf[i];
                    total++;
                    line.Add(b);
                    if (b == 0x0A || b == 0x0D || line.Count >= 32)
                    {
                        FlushLine(line);
                    }
                }
            }

            if (line.Count > 0) FlushLine(line);
            return total;
        }

        private static void FlushLine(List<byte> line)
        {
            var hex = new StringBuilder(line.Count * 3);
            var asc = new StringBuilder(line.Count);
            for (int i = 0; i < line.Count; i++)
            {
                byte b = line[i];
                hex.Append(b.ToString("X2"));
                hex.Append(' ');
                asc.Append(b >= 0x20 && b < 0x7F ? (char)b : '.');
            }
            string stamp = DateTime.Now.ToString("HH:mm:ss.fff");
            Log("  " + stamp + "  " + hex.ToString().PadRight(32 * 3) + " | " + asc.ToString());
            line.Clear();
        }

        private static void LiveMode(string portName, SerialSetting s)
        {
            SerialPort sp = Open(portName, s);
            if (sp == null)
            {
                Log("Could not open port for live mode.");
                return;
            }

            var stop = new ManualResetEventSlim(false);
            var thread = new Thread(() =>
            {
                var buf = new byte[512];
                var line = new List<byte>(64);
                while (!stop.IsSet)
                {
                    try
                    {
                        int available = sp.BytesToRead;
                        if (available <= 0) { Thread.Sleep(20); continue; }
                        int n = sp.Read(buf, 0, Math.Min(buf.Length, available));
                        for (int i = 0; i < n; i++)
                        {
                            byte b = buf[i];
                            line.Add(b);
                            if (b == 0x0A || b == 0x0D || line.Count >= 32) FlushLine(line);
                        }
                    }
                    catch (TimeoutException) { }
                    catch (Exception ex) { Log("  live read failed: " + ex.Message); break; }
                }
                if (line.Count > 0) FlushLine(line);
            });
            thread.IsBackground = true;
            thread.Start();

            Console.ReadLine();
            stop.Set();
            thread.Join(1000);
            Close(sp);
        }

        private static void Log(string message)
        {
            Console.WriteLine(message);
            if (_file != null) _file.WriteLine(message);
        }
    }
}
