// =============================================================================
// Binary Equation Paths — Core Algorithm
// Author:  Rich Wagner | License: Apache 2.0 | newdawndata.com
// =============================================================================

namespace BEPBenchmark.Core
{
    public enum ByteOrder { BigEndian, LittleEndian }

    public static class BEP
    {
        // -------------------------------------------------------------------------
        // VALUE-LEVEL — endian-agnostic, operates directly on a long integer
        // -------------------------------------------------------------------------

        /// <summary>
        /// Compresses a positive integer into its Binary Equation Path bit string.
        /// Records a "primary bit" at each step, flipping it on every odd step.
        /// Terminates in exactly floor(log2(val)) steps.
        /// </summary>
        public static string ValCompressor(long val)
        {
            if (val < 2) return "";         // 0 and 1 are not compressible by this method

            string chars    = "0";          // Primary bit — flips on every odd step
            string opbinary = "";

            while (val != 1)
            {
                if (val % 2 == 1)
                {
                    chars = (chars == "0") ? "1" : "0";
                    val  -= 1;
                }
                val     /= 2;
                opbinary = chars + opbinary;
            }

            return opbinary;
        }

        /// <summary>
        /// Decompresses a BEP path string back to its original long value.
        /// Detects bit transitions to reverse each odd step.
        /// </summary>
        public static long ValDecompressor(string bts)
        {
            if (string.IsNullOrEmpty(bts)) return 1;

            long odd = Convert.ToInt32(char.GetNumericValue(bts[bts.Length - 1]));
            long val = 1;
            char lc  = bts[0];

            foreach (char c in bts)
            {
                if (c != lc) val += 1;
                val *= 2;
                lc   = c;
            }

            long origVal = val;
            if (odd == 1) origVal += 1;
            return origVal;
        }

        // -------------------------------------------------------------------------
        // UTILITY
        // -------------------------------------------------------------------------

        /// <summary>
        /// Reverses byte-chunk order of a binary string to convert between BE and LE.
        /// Preserves bit order within each 8-bit chunk.
        /// </summary>
        public static string FlipByteOrder(string binary)
        {
            var chunks = new List<string>();
            for (int i = 0; i < binary.Length; i += 8)
            {
                string chunk = binary.Substring(i, Math.Min(8, binary.Length - i));
                if (chunk.Length < 8) chunk = chunk.PadRight(8, '0');
                chunks.Add(chunk);
            }
            chunks.Reverse();
            return string.Join("", chunks);
        }

        /// <summary>
        /// Converts a Big Endian binary string to a byte array (MSB first per chunk).
        /// </summary>
        public static byte[] BinToByteArrBE(string bits)
        {
            var bytes = new List<byte>();
            int value = 0;
            for (int x = 0; x < bits.Length; x += 8)
            {
                value += (int)char.GetNumericValue(bits[x])     * 128;
                if ((x+1) < bits.Length) value += (int)char.GetNumericValue(bits[x+1]) * 64;
                if ((x+2) < bits.Length) value += (int)char.GetNumericValue(bits[x+2]) * 32;
                if ((x+3) < bits.Length) value += (int)char.GetNumericValue(bits[x+3]) * 16;
                if ((x+4) < bits.Length) value += (int)char.GetNumericValue(bits[x+4]) * 8;
                if ((x+5) < bits.Length) value += (int)char.GetNumericValue(bits[x+5]) * 4;
                if ((x+6) < bits.Length) value += (int)char.GetNumericValue(bits[x+6]) * 2;
                if ((x+7) < bits.Length) value += (int)char.GetNumericValue(bits[x+7]) * 1;
                bytes.Add((byte)value);
                value = 0;
            }
            return bytes.ToArray();
        }

        /// <summary>
        /// Converts a byte array to a Big Endian binary string (MSB first per byte).
        /// </summary>
        public static string ByteArrToBinBE(byte[] bytes)
        {
            string bin = "";
            foreach (byte b in bytes)
                bin += ByteToBin8(b);
            return bin;
        }

        /// <summary>Converts a byte array (Big Endian) to a long via base-256 positional math.</summary>
        public static long ByteLongConvertBE(byte[] n, long bse = 256)
        {
            long value    = 0;
            long exponent = (long)Math.Pow(bse, n.Length - 1);
            foreach (byte b in n)
            {
                value    += exponent * b;
                exponent /= bse;
            }
            return value;
        }

        /// <summary>Converts a single byte to its 8-character Big Endian binary string.</summary>
        public static string ByteToBin8(int n)
        {
            if (n == 255) return "11111111";
            if (n == 0)   return "00000000";
            int val    = 128;
            string bin = "";
            while (val != 1)
            {
                bin  = (n >= val ? "1" : "0") + bin;
                if (n >= val) n -= val;
                val /= 2;
            }
            bin = (n == 1 ? "1" : "0") + bin;
            return bin;
        }
    }
}
