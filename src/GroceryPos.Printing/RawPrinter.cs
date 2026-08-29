using System;
using System.Runtime.InteropServices;

namespace GroceryPos.Printing
{
    public interface IReceiptPrinter
    {
        void Print(string queueName, byte[] bytes);
    }

    /// <summary>Sends raw bytes to a Windows print queue via winspool.drv.</summary>
    public class WindowsRawPrinter : IReceiptPrinter
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private class DOCINFOW
        {
            [MarshalAs(UnmanagedType.LPWStr)] public string pDocName;
            [MarshalAs(UnmanagedType.LPWStr)] public string pOutputFile;
            [MarshalAs(UnmanagedType.LPWStr)] public string pDataType;
        }

        [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "OpenPrinterW")]
        private static extern bool OpenPrinter(string src, out IntPtr hPrinter, IntPtr pd);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool ClosePrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "StartDocPrinterW")]
        private static extern int StartDocPrinter(IntPtr hPrinter, int level, [In] DOCINFOW di);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool EndDocPrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool StartPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool EndPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, int dwCount, out int written);

        public void Print(string queueName, byte[] bytes)
        {
            if (string.IsNullOrWhiteSpace(queueName)) throw new InvalidOperationException("Printer queue not configured");
            IntPtr h;
            if (!OpenPrinter(queueName, out h, IntPtr.Zero))
                throw new InvalidOperationException("OpenPrinter failed: " + Marshal.GetLastWin32Error());
            try
            {
                var di = new DOCINFOW { pDocName = "GroceryPOS Receipt", pDataType = "RAW" };
                if (StartDocPrinter(h, 1, di) == 0)
                    throw new InvalidOperationException("StartDocPrinter failed: " + Marshal.GetLastWin32Error());
                try
                {
                    if (!StartPagePrinter(h)) throw new InvalidOperationException("StartPagePrinter failed");
                    IntPtr p = Marshal.AllocCoTaskMem(bytes.Length);
                    try
                    {
                        Marshal.Copy(bytes, 0, p, bytes.Length);
                        int written;
                        if (!WritePrinter(h, p, bytes.Length, out written))
                            throw new InvalidOperationException("WritePrinter failed: " + Marshal.GetLastWin32Error());
                    }
                    finally { Marshal.FreeCoTaskMem(p); }
                    EndPagePrinter(h);
                }
                finally { EndDocPrinter(h); }
            }
            finally { ClosePrinter(h); }
        }
    }

    /// <summary>Test double: swallows bytes. Used when no queue is configured.</summary>
    public class NullPrinter : IReceiptPrinter
    {
        public byte[] LastBytes { get; private set; }
        public void Print(string queueName, byte[] bytes) { LastBytes = bytes; }
    }
}
