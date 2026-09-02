using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GroceryPos.Printing
{
    /// <summary>Assembles ESC/POS byte streams. CP437 encoded. Every line terminated LF.</summary>
    public static class EscPos
    {
        // ESC @ (reset) + GS L 0 0 (left margin = 0 dots) + GS W (print width).
        //
        // Print width must match the text, not the paper. The layout is 48
        // characters of Font A at 12 dots each = 576 dots. Setting the width to
        // the full 640-dot paper made the printer centre 576 dots of text inside
        // a 640-dot area, which pushed every line about five characters to the
        // right and left a gap down the left edge of the slip.
        private const int CharsPerLine = 48;
        private const int FontADotsPerChar = 12;
        private const int PrintWidthDots = CharsPerLine * FontADotsPerChar;   // 576

        public static readonly byte[] Init = new byte[]
        {
            0x1B, 0x40,                                   // ESC @  reset
            0x1D, 0x4C, 0x00, 0x00,                       // GS L   left margin 0
            0x1D, 0x57,                                   // GS W   print width...
            (byte)(PrintWidthDots & 0xFF),                //   nL
            (byte)((PrintWidthDots >> 8) & 0xFF)          //   nH
        };
        public static readonly byte[] AlignLeft = new byte[] { 0x1B, 0x61, 0x00 };
        public static readonly byte[] AlignCenter = new byte[] { 0x1B, 0x61, 0x01 };
        public static readonly byte[] BoldOn = new byte[] { 0x1B, 0x45, 0x01 };
        public static readonly byte[] BoldOff = new byte[] { 0x1B, 0x45, 0x00 };
        public static readonly byte[] SizeDouble = new byte[] { 0x1D, 0x21, 0x11 };
        public static readonly byte[] SizeNormal = new byte[] { 0x1D, 0x21, 0x00 };
        public static readonly byte[] CutFull = new byte[] { 0x1D, 0x56, 0x00 };
        public static readonly byte[] LF = new byte[] { 0x0A };
        public static readonly byte[] DrawerKick0 = new byte[] { 0x1B, 0x70, 0x00, 0x19, 0xFA };
        public static readonly byte[] DrawerKick1 = new byte[] { 0x1B, 0x70, 0x01, 0x19, 0xFA };

        public static Encoding Cp437 { get { return Encoding.GetEncoding(437); } }

        public static byte[] FeedLines(int n)
        {
            if (n < 0) n = 0;
            if (n > 255) n = 255;
            return new byte[] { 0x1B, 0x64, (byte)n };
        }

        public static byte[] Build(IList<string> textLines, bool cut, bool drawerKick, int drawerPin)
        {
            var rich = new List<ReceiptLine>(textLines.Count);
            foreach (var t in textLines) rich.Add(new ReceiptLine(t));
            return Build(rich, cut, drawerKick, drawerPin);
        }

        public static byte[] Build(IList<ReceiptLine> lines, bool cut, bool drawerKick, int drawerPin)
        {
            var ms = new MemoryStream();
            ms.Write(Init, 0, Init.Length);
            // Left-align the printer's cursor. The formatter builds fixed-width
            // 48-column lines, so alignment is done in text — the printer just
            // needs to start at the left margin.
            ms.Write(AlignLeft, 0, AlignLeft.Length);
            var enc = Cp437;
            foreach (var line in lines)
            {
                if (line.Bold) ms.Write(BoldOn, 0, BoldOn.Length);
                if (line.DoubleSize) ms.Write(SizeDouble, 0, SizeDouble.Length);
                byte[] b = enc.GetBytes(line.Text ?? "");
                ms.Write(b, 0, b.Length);
                if (line.DoubleSize) ms.Write(SizeNormal, 0, SizeNormal.Length);
                if (line.Bold) ms.Write(BoldOff, 0, BoldOff.Length);
                ms.Write(LF, 0, LF.Length);
            }
            var feed = FeedLines(3);
            ms.Write(feed, 0, feed.Length);
            if (drawerKick)
            {
                var k = drawerPin == 1 ? DrawerKick1 : DrawerKick0;
                ms.Write(k, 0, k.Length);
            }
            if (cut) ms.Write(CutFull, 0, CutFull.Length);
            return ms.ToArray();
        }
    }

    /// <summary>A single printed line with optional emphasis. Text is expected
    /// pre-padded to the paper width by the formatter.</summary>
    public class ReceiptLine
    {
        public string Text;
        public bool Bold;
        public bool DoubleSize;
        public ReceiptLine() { }
        public ReceiptLine(string text) { Text = text; }
        public ReceiptLine(string text, bool bold, bool doubleSize)
        { Text = text; Bold = bold; DoubleSize = doubleSize; }
    }
}
