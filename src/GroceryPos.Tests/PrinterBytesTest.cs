using System;
using System.Collections.Generic;
using System.Linq;
using GroceryPos.Printing;
using Xunit;

namespace GroceryPos.Tests
{
    /// <summary>
    /// What actually goes down the wire to the TVS RP 3230. The printer is not
    /// available in the test environment, so these assert the byte stream instead:
    /// wrong bytes here mean garbage on the roll at the counter.
    /// </summary>
    public class PrinterBytesTest
    {
        [Fact]
        public void EveryByteIsCp437_NoUtf8Leaks()
        {
            var lines = new List<string>
            {
                "SRI BALAJI SUPER MARKET",
                new string(ReceiptFormatter.LineChar, ReceiptFormatter.Width),
                ReceiptFormatter.PadPair("NET PAYABLE", "Rs. 349.00")
            };

            byte[] bytes = EscPos.Build(lines, true, false, 0);

            // A UTF-8 encoder emits 0xEF 0xBB 0xBF for a BOM and multi-byte
            // sequences for anything non-ASCII. CP437 is single byte throughout.
            Assert.DoesNotContain(bytes.Take(3), b => b == 0xEF);

            // The box-drawing rule must arrive as CP437 0xC4, the printer's own
            // horizontal line glyph — not as a 3-byte UTF-8 sequence.
            Assert.Contains<byte>(0xC4, bytes);
            Assert.DoesNotContain<byte>(0xE2, bytes);   // UTF-8 lead byte for U+2500
        }

        [Fact]
        public void TheStreamInitialisesAndCutsCorrectly()
        {
            byte[] bytes = EscPos.Build(new List<string> { "test" }, true, false, 0);

            // ESC @ reset must be first, or a previous job's state carries over.
            Assert.Equal(0x1B, bytes[0]);
            Assert.Equal(0x40, bytes[1]);

            // GS V 0 full cut must be present at the end.
            bool hasCut = false;
            for (int i = 0; i < bytes.Length - 2; i++)
                if (bytes[i] == 0x1D && bytes[i + 1] == 0x56 && bytes[i + 2] == 0x00)
                    hasCut = true;
            Assert.True(hasCut, "No GS V 0 cut command in the stream.");
        }

        /// <summary>
        /// Auto line feed is disabled on this printer, so nothing advances the
        /// paper unless the software emits LF itself.
        /// </summary>
        [Fact]
        public void EveryTextLineEndsWithAnExplicitLineFeed()
        {
            var lines = new List<string> { "one", "two", "three" };
            byte[] bytes = EscPos.Build(lines, false, false, 0);

            int lfCount = bytes.Count(b => b == 0x0A);
            Assert.True(lfCount >= lines.Count,
                "Only " + lfCount + " LF bytes for " + lines.Count +
                " lines. Auto line feed is off, so each line needs its own.");
        }

        /// <summary>The drawer kick must only be sent when a drawer is configured.</summary>
        [Fact]
        public void DrawerKick_IsOnlySentWhenAskedFor()
        {
            byte[] without = EscPos.Build(new List<string> { "x" }, true, false, 0);
            byte[] with = EscPos.Build(new List<string> { "x" }, true, true, 0);

            Assert.False(Contains(without, EscPos.DrawerKick0),
                "A drawer kick was sent into a port with no drawer attached.");
            Assert.True(Contains(with, EscPos.DrawerKick0));

            // Pin 5 is the documented alternative for drawers that do not open.
            byte[] pin1 = EscPos.Build(new List<string> { "x" }, true, true, 1);
            Assert.True(Contains(pin1, EscPos.DrawerKick1));
        }

        private static bool Contains(byte[] haystack, byte[] needle)
        {
            for (int i = 0; i + needle.Length <= haystack.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < needle.Length; j++)
                    if (haystack[i + j] != needle[j]) { match = false; break; }
                if (match) return true;
            }
            return false;
        }
    }
}
