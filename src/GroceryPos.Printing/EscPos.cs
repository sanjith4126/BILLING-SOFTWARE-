using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GroceryPos.Printing
{
    /// <summary>Assembles ESC/POS byte streams. CP437 encoded. Every line terminated LF.</summary>
    public static class EscPos
    {
        public static readonly byte[] Init = new byte[] { 0x1B, 0x40 };
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
            var ms = new MemoryStream();
            ms.Write(Init, 0, Init.Length);
            ms.Write(AlignLeft, 0, AlignLeft.Length);
            var enc = Cp437;
            foreach (var line in textLines)
            {
                byte[] b = enc.GetBytes(line);
                ms.Write(b, 0, b.Length);
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
}
