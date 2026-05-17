// =============================================================================
// Binary Equation Paths
// Author:  Rich Wagner
// License: Apache 2.0 — https://www.apache.org/licenses/LICENSE-2.0
// Repo:    https://github.com/bepCreator/ndd
// Site:    https://newdawndata.com
//
// A lossless binary compression algorithm derived from a provably convergent
// modification of the Collatz Conjecture. Every integer >= 2 has a shorter
// binary path representation, recoverable through pure arithmetic with no
// dictionaries, indexes, or stored mappings.
//
// Core formula:
//   f(n) = n / 2          if n is even  (record current primary bit)
//   f(n) = (n - 1) / 2   if n is odd   (flip primary bit, then record)
//
// Convergence is guaranteed in exactly floor(log2(n)) steps.
// See PROOF_OF_CONVERGENCE.md for the formal mathematical proof.
//
// Endianness note:
//   Big Endian    — most significant byte first. Standard network/readable order.
//   Little Endian — least significant byte first. Standard x86/memory order.
//   Both incoming binary strings and output binary strings respect the chosen
//   convention. Use the BE methods (default) or LE methods explicitly.
//   The ValCompressor/ValDecompressor methods operate on raw integers and are
//   endian-agnostic — byte order only applies to binary string I/O.
// =============================================================================

using System;
using System.Collections.Generic;

namespace BinaryEquationPaths
{
    // =========================================================================
    /// <summary>
    /// Byte order convention to apply when reading or writing binary strings.
    /// BigEndian    : most significant byte first  (e.g. 11001010 00110001)
    /// LittleEndian : least significant byte first (e.g. 00110001 11001010)
    /// </summary>
    // =========================================================================
    public enum ByteOrder { BigEndian, LittleEndian }


    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=============================================================");
            Console.WriteLine(" Binary Equation Paths — Baseline Demonstration             ");
            Console.WriteLine("=============================================================\n");

            // --- Value-level round trip (endian-agnostic) ---
            Console.WriteLine("[ Value Compression — endian-agnostic ]");
            long testVal = 3123733177;
            string compressed = BEP.ValCompressor(testVal);
            long decompressed = BEP.ValDecompressor(compressed);
            Console.WriteLine($"  Original value  : {testVal}");
            Console.WriteLine($"  Compressed path : {compressed}  ({compressed.Length} bits)");
            Console.WriteLine($"  Decompressed    : {decompressed}");
            Console.WriteLine($"  Lossless        : {(testVal == decompressed ? "YES" : "NO")}\n");

            // --- 4-byte Big Endian round trip ---
            Console.WriteLine("[ 4-Byte Compression — Big Endian ]");
            RoundTrip("10011101010001100000110001011101", 4, ByteOrder.BigEndian);

            // --- 4-byte Little Endian round trip ---
            Console.WriteLine("[ 4-Byte Compression — Little Endian ]");
            RoundTrip("10111010000011000100011010011101", 4, ByteOrder.LittleEndian);

            // --- 1-byte both orders ---
            Console.WriteLine("[ 1-Byte Compression — Big Endian ]");
            RoundTrip("11001010", 1, ByteOrder.BigEndian);
            Console.WriteLine("[ 1-Byte Compression — Little Endian ]");
            RoundTrip("01010011", 1, ByteOrder.LittleEndian);

            // --- 2-byte both orders ---
            Console.WriteLine("[ 2-Byte Compression — Big Endian ]");
            RoundTrip("1100101000110001", 2, ByteOrder.BigEndian);
            Console.WriteLine("[ 2-Byte Compression — Little Endian ]");
            RoundTrip("0011000101010011", 2, ByteOrder.LittleEndian);

            // --- 3-byte both orders ---
            Console.WriteLine("[ 3-Byte Compression — Big Endian ]");
            RoundTrip("110010100011000101001101", 3, ByteOrder.BigEndian);
            Console.WriteLine("[ 3-Byte Compression — Little Endian ]");
            RoundTrip("101001010011000101010011", 3, ByteOrder.LittleEndian);

            Console.WriteLine("=============================================================");
            Console.WriteLine(" Apache 2.0 — newdawndata.com");
            Console.WriteLine("=============================================================");
            Console.ReadLine();
        }

        /// <summary>
        /// Helper: compresses and decompresses a binary string, then prints the result.
        /// </summary>
        static void RoundTrip(string origin, int byteWidth, ByteOrder order)
        {
            string result = BEP.Compress(origin, byteWidth, order);
            string restored = BEP.Decompress(result, byteWidth, order);
            string tag = order == ByteOrder.BigEndian ? "BE" : "LE";
            Console.WriteLine($"  Origin  ({origin.Length,2} bits) [{tag}]: {origin}");
            Console.WriteLine($"  Result  ({(result == "error" ? "err" : result.Length.ToString()),2} bits) [{tag}]: {result}");
            Console.WriteLine($"  Restored          [{tag}]: {restored}");
            Console.WriteLine($"  Lossless              : {(origin == restored ? "YES" : "NO")}\n");
        }
    }


    // =========================================================================
    /// <summary>
    /// Core Binary Equation Paths algorithm.
    ///
    /// Compression records a "primary bit" (chars) at each step:
    ///   Even step : keep current primary bit, divide by 2.
    ///   Odd step  : flip primary bit, subtract 1, divide by 2.
    /// The resulting bit string is the compressed representation.
    ///
    /// Decompression reconstructs the original value by reversing the walk:
    ///   Each character in the path doubles val.
    ///   Each bit-flip (transition) adds 1 before doubling, reversing the subtract.
    ///
    /// Endianness applies only to binary string I/O — the integer walk itself
    /// is the same regardless of byte order. The FlipByteOrder helper reverses
    /// the byte-chunk ordering of a binary string to convert between conventions.
    /// </summary>
    // =========================================================================
    public static class BEP
    {
        // =====================================================================
        // UNIFIED COMPRESS / DECOMPRESS ENTRY POINTS
        // These are the recommended public API. Pass byteWidth (1–4) and
        // ByteOrder to select the appropriate variant automatically.
        // =====================================================================

        /// <summary>
        /// Compresses a binary string using the BEP algorithm.
        /// </summary>
        /// <param name="binary">Input binary string.</param>
        /// <param name="byteWidth">Number of bytes the input represents (1, 2, 3, or 4).</param>
        /// <param name="order">Byte order of the input string (BigEndian or LittleEndian).</param>
        /// <returns>Compressed binary path string in the same byte order, or "error" on overflow.</returns>
        public static string Compress(string binary, int byteWidth, ByteOrder order)
        {
            switch (byteWidth)
            {
                case 1: return order == ByteOrder.BigEndian ? Compressor1BE(binary) : Compressor1LE(binary);
                case 2: return order == ByteOrder.BigEndian ? Compressor2BE(binary) : Compressor2LE(binary);
                case 3: return order == ByteOrder.BigEndian ? Compressor3BE(binary) : Compressor3LE(binary);
                case 4: return order == ByteOrder.BigEndian ? Compressor4BE(binary) : Compressor4LE(binary);
                default: throw new ArgumentOutOfRangeException(nameof(byteWidth), "Supported widths: 1, 2, 3, 4.");
            }
        }

        /// <summary>
        /// Decompresses a BEP path string back to its original binary representation.
        /// </summary>
        /// <param name="path">Compressed binary path string.</param>
        /// <param name="byteWidth">Number of bytes the original value occupied (1, 2, 3, or 4).</param>
        /// <param name="order">Byte order to use for the output binary string.</param>
        /// <returns>Original binary string in the specified byte order.</returns>
        public static string Decompress(string path, int byteWidth, ByteOrder order)
        {
            switch (byteWidth)
            {
                case 1: return order == ByteOrder.BigEndian ? Decompressor1BE(path) : Decompressor1LE(path);
                case 2: return order == ByteOrder.BigEndian ? Decompressor2BE(path) : Decompressor2LE(path);
                case 3: return order == ByteOrder.BigEndian ? Decompressor3BE(path) : Decompressor3LE(path);
                case 4: return order == ByteOrder.BigEndian ? Decompressor4BE(path) : Decompressor4LE(path);
                default: throw new ArgumentOutOfRangeException(nameof(byteWidth), "Supported widths: 1, 2, 3, 4.");
            }
        }


        // =====================================================================
        // VALUE-LEVEL COMPRESSION — endian-agnostic
        // Operates directly on a long integer. No byte conversion, no byte order.
        // =====================================================================

        /// <summary>
        /// Compresses a long integer into its Binary Equation Path bit string.
        /// Terminates in exactly floor(log2(val)) steps.
        /// </summary>
        /// <param name="val">Any positive integer >= 2.</param>
        /// <returns>Compressed binary path string.</returns>
        public static string ValCompressor(long val)
        {
            string chars = "0";  // Primary bit — flips on every odd step
            string opbinary = "";   // Path built right-to-left via prepend

            while (val != 1)
            {
                if (val % 2 == 1)   // Odd: flip primary bit and subtract 1
                {
                    chars = (chars == "0") ? "1" : "0";
                    val -= 1;
                }
                val /= 2;                       // Right-shift (floor divide by 2)
                opbinary = chars + opbinary;    // Prepend current primary bit
            }

            return opbinary;
        }

        /// <summary>
        /// Decompresses a BEP path string back to its original long integer value.
        /// Detects bit transitions (flips) in the path to reverse each odd step.
        /// </summary>
        /// <param name="bts">Compressed binary path string from ValCompressor.</param>
        /// <returns>Original long integer.</returns>
        public static long ValDecompressor(string bts)
        {
            // Last character indicates whether the original value was odd (1) or even (0)
            long odd = Convert.ToInt32(char.GetNumericValue(bts[bts.Length - 1]));

            long val = 1;       // Reconstruction starts from the convergence target
            char lc = bts[0]; // Tracks previous character to detect flips

            foreach (char c in bts)
            {
                if (c != lc)
                    val += 1;   // Flip detected — reverse the odd step's subtract-1
                val *= 2;       // Reverse the divide-by-2
                lc = c;
            }

            long origVal = val;
            if (odd == 1)
                origVal += 1;   // Restore final odd-step subtraction if needed

            return origVal;
        }


        // =====================================================================
        // ENDIANNESS UTILITY
        // =====================================================================

        /// <summary>
        /// Reverses the byte-chunk order of a binary string to convert between
        /// Big Endian and Little Endian representations.
        ///
        /// Example (16-bit):
        ///   BE input : "11001010 00110001"  →  LE output : "00110001 11001010"
        ///
        /// The bit order within each 8-bit chunk is preserved — only the chunk
        /// positions are swapped. Pads the final chunk to 8 bits if needed.
        /// </summary>
        /// <param name="binary">Binary string (any multiple of 1 bit; chunks of 8).</param>
        /// <returns>Binary string with byte-chunk order reversed.</returns>
        public static string FlipByteOrder(string binary)
        {
            List<string> chunks = new List<string>();

            // Split into 8-bit chunks, padding the last if the string isn't byte-aligned
            for (int i = 0; i < binary.Length; i += 8)
            {
                int remaining = Math.Min(8, binary.Length - i);
                string chunk = binary.Substring(i, remaining);
                if (chunk.Length < 8)
                    chunk = chunk.PadRight(8, '0'); // Pad incomplete trailing byte
                chunks.Add(chunk);
            }

            chunks.Reverse();                   // Reverse byte order
            return string.Join("", chunks);     // Rejoin into a single string
        }


        // =====================================================================
        // INTERNAL WALK — shared core logic used by all Compressor variants
        // =====================================================================

        /// <summary>
        /// Runs the BEP compression walk on a pre-converted long value.
        /// Called by all byte-width variants after their input conversion.
        /// </summary>
        /// <param name="val">Integer value to compress.</param>
        /// <param name="stepLimit">Maximum allowed steps before returning "error".</param>
        /// <returns>Compressed binary path string, or "error" on overflow.</returns>
        private static string RunCompression(long val, int stepLimit)
        {
            string chars = "0";
            string opbinary = "";

            while (val != 1)
            {
                if (val % 2 == 1)
                {
                    chars = (chars == "0") ? "1" : "0";
                    val -= 1;
                }
                val /= 2;
                if (opbinary.Length >= stepLimit)
                    return "error";
                opbinary = chars + opbinary;
            }

            return opbinary;
        }

        /// <summary>
        /// Runs the BEP decompression walk on a path string, returning the long value.
        /// Called by all byte-width variants before their output conversion.
        /// </summary>
        /// <param name="bts">Compressed binary path string.</param>
        /// <returns>Reconstructed long integer.</returns>
        private static long RunDecompression(string bts)
        {
            long odd = Convert.ToInt32(char.GetNumericValue(bts[bts.Length - 1]));
            long val = 1;
            char lc = bts[0];

            foreach (char c in bts)
            {
                if (c != lc) val += 1;
                val *= 2;
                lc = c;
            }

            long origVal = val;
            if (odd == 1) origVal += 1;

            return origVal;
        }


        // =====================================================================
        // 1-BYTE COMPRESSORS / DECOMPRESSORS (8-bit, values 0–255)
        // =====================================================================

        /// <summary>Compresses a Big Endian 8-bit binary string.</summary>
        public static string Compressor1BE(string used)
        {
            // BE input: read as-is — MSB first, convert to integer, compress
            byte[] bin = BinToByteArrBE(used);
            long val = ByteLongConvert(bin, 256);
            return RunCompression(val, 8);
        }

        /// <summary>Decompresses a BEP path back to a Big Endian 8-bit binary string.</summary>
        public static string Decompressor1BE(string bts)
        {
            long origVal = RunDecompression(bts);
            byte[] origBytes = IntByteConvert1(origVal);
            return ByteArrToBinBE(origBytes);
        }

        /// <summary>Compresses a Little Endian 8-bit binary string.</summary>
        public static string Compressor1LE(string used)
        {
            // LE input: flip byte order to BE before integer conversion
            byte[] bin = BinToByteArrBE(FlipByteOrder(used));
            long val = ByteLongConvert(bin, 256);
            return RunCompression(val, 8);
        }

        /// <summary>Decompresses a BEP path back to a Little Endian 8-bit binary string.</summary>
        public static string Decompressor1LE(string bts)
        {
            long origVal = RunDecompression(bts);
            byte[] origBytes = IntByteConvert1(origVal);
            // Flip the output binary string to Little Endian byte order
            return FlipByteOrder(ByteArrToBinBE(origBytes));
        }


        // =====================================================================
        // 2-BYTE COMPRESSORS / DECOMPRESSORS (16-bit, values 0–65,535)
        // =====================================================================

        /// <summary>Compresses a Big Endian 16-bit binary string.</summary>
        public static string Compressor2BE(string used)
        {
            byte[] bin = BinToByteArrBE(used);
            long val = ByteLongConvert(bin, 256);
            return RunCompression(val, 16);
        }

        /// <summary>Decompresses a BEP path back to a Big Endian 16-bit binary string.</summary>
        public static string Decompressor2BE(string bts)
        {
            long origVal = RunDecompression(bts);
            byte[] origBytes = IntByteConvert2(origVal);
            return ByteArrToBinBE(origBytes);
        }

        /// <summary>Compresses a Little Endian 16-bit binary string.</summary>
        public static string Compressor2LE(string used)
        {
            byte[] bin = BinToByteArrBE(FlipByteOrder(used));
            long val = ByteLongConvert(bin, 256);
            return RunCompression(val, 16);
        }

        /// <summary>Decompresses a BEP path back to a Little Endian 16-bit binary string.</summary>
        public static string Decompressor2LE(string bts)
        {
            long origVal = RunDecompression(bts);
            byte[] origBytes = IntByteConvert2(origVal);
            return FlipByteOrder(ByteArrToBinBE(origBytes));
        }


        // =====================================================================
        // 3-BYTE COMPRESSORS / DECOMPRESSORS (24-bit, values 0–16,777,215)
        // =====================================================================

        /// <summary>Compresses a Big Endian 24-bit binary string.</summary>
        public static string Compressor3BE(string used)
        {
            byte[] bin = BinToByteArrBE(used);
            long val = ByteLongConvert(bin, 256);
            return RunCompression(val, 24);
        }

        /// <summary>Decompresses a BEP path back to a Big Endian 24-bit binary string.</summary>
        public static string Decompressor3BE(string bts)
        {
            long origVal = RunDecompression(bts);
            byte[] origBytes = IntByteConvert3(origVal);
            return ByteArrToBinBE(origBytes);
        }

        /// <summary>Compresses a Little Endian 24-bit binary string.</summary>
        public static string Compressor3LE(string used)
        {
            byte[] bin = BinToByteArrBE(FlipByteOrder(used));
            long val = ByteLongConvert(bin, 256);
            return RunCompression(val, 24);
        }

        /// <summary>Decompresses a BEP path back to a Little Endian 24-bit binary string.</summary>
        public static string Decompressor3LE(string bts)
        {
            long origVal = RunDecompression(bts);
            byte[] origBytes = IntByteConvert3(origVal);
            return FlipByteOrder(ByteArrToBinBE(origBytes));
        }


        // =====================================================================
        // 4-BYTE COMPRESSORS / DECOMPRESSORS (32-bit, values 0–4,294,967,295)
        // =====================================================================

        /// <summary>Compresses a Big Endian 32-bit binary string.</summary>
        public static string Compressor4BE(string used)
        {
            byte[] bin = BinToByteArrBE(used);
            long val = ByteLongConvert(bin, 256);
            return RunCompression(val, 32);
        }

        /// <summary>Decompresses a BEP path back to a Big Endian 32-bit binary string.</summary>
        public static string Decompressor4BE(string bts)
        {
            long origVal = RunDecompression(bts);
            byte[] origBytes = IntByteConvert4(origVal);
            return ByteArrToBinBE(origBytes);
        }

        /// <summary>Compresses a Little Endian 32-bit binary string.</summary>
        public static string Compressor4LE(string used)
        {
            byte[] bin = BinToByteArrBE(FlipByteOrder(used));
            long val = ByteLongConvert(bin, 256);
            return RunCompression(val, 32);
        }

        /// <summary>Decompresses a BEP path back to a Little Endian 32-bit binary string.</summary>
        public static string Decompressor4LE(string bts)
        {
            long origVal = RunDecompression(bts);
            byte[] origBytes = IntByteConvert4(origVal);
            return FlipByteOrder(ByteArrToBinBE(origBytes));
        }


        // =====================================================================
        // BYTE / BINARY CONVERSION UTILITIES
        // =====================================================================

        /// <summary>
        /// Converts a Big Endian binary string to a byte array.
        /// Reads each 8-bit chunk MSB-first (bit[0] = weight 128, bit[7] = weight 1).
        /// Returned array is in Big Endian byte order (index 0 = most significant byte).
        /// </summary>
        public static byte[] BinToByteArrBE(string bits)
        {
            List<byte> bytes = new List<byte>();
            int value = 0;

            for (int x = 0; x < bits.Length; x += 8)
            {
                // MSB-first: bit[0] carries weight 128, bit[7] carries weight 1
                value += (int)char.GetNumericValue(bits[x]) * 128;
                if ((x + 1) < bits.Length) value += (int)char.GetNumericValue(bits[x + 1]) * 64;
                if ((x + 2) < bits.Length) value += (int)char.GetNumericValue(bits[x + 2]) * 32;
                if ((x + 3) < bits.Length) value += (int)char.GetNumericValue(bits[x + 3]) * 16;
                if ((x + 4) < bits.Length) value += (int)char.GetNumericValue(bits[x + 4]) * 8;
                if ((x + 5) < bits.Length) value += (int)char.GetNumericValue(bits[x + 5]) * 4;
                if ((x + 6) < bits.Length) value += (int)char.GetNumericValue(bits[x + 6]) * 2;
                if ((x + 7) < bits.Length) value += (int)char.GetNumericValue(bits[x + 7]) * 1;

                bytes.Add((byte)value);
                value = 0;
            }

            return bytes.ToArray();
        }

        /// <summary>
        /// Converts a byte array to a Big Endian binary string.
        /// Each byte is written MSB-first; bytes appear in array order (index 0 first).
        /// </summary>
        public static string ByteArrToBinBE(byte[] bytes)
        {
            string bin = "";
            foreach (byte b in bytes)
                bin += Length8Convert(b);
            return bin;
        }

        /// <summary>
        /// Converts a byte array to a long integer using positional base-256 arithmetic.
        /// Treats the array as Big Endian — index 0 is the most significant byte.
        /// Example: bytes [202, 49] → (202 × 256¹) + (49 × 256⁰) = 51,762.
        /// </summary>
        /// <param name="n">Byte array (Big Endian — most significant byte at index 0).</param>
        /// <param name="bse">Numeric base (pass 256 for standard byte conversion).</param>
        public static long ByteLongConvert(byte[] n, long bse)
        {
            long value = 0;
            long exponent = (long)Math.Pow(bse, n.Length - 1); // Start at highest positional power

            foreach (byte b in n)
            {
                value += exponent * b;
                exponent /= bse;
            }

            return value;
        }

        // --- Fixed-width IntByteConvert variants ---
        // Each converts a long back to a fixed-size Big Endian byte array
        // by greedily extracting digits from the most significant position down.

        /// <summary>Converts a long to a 1-byte Big Endian array (0–255).</summary>
        public static byte[] IntByteConvert1(long value)
        {
            return ExtractBytes(value, new long[] { 1 });
        }

        /// <summary>Converts a long to a 2-byte Big Endian array (0–65,535).</summary>
        public static byte[] IntByteConvert2(long value)
        {
            return ExtractBytes(value, new long[] { 256, 1 });
        }

        /// <summary>Converts a long to a 3-byte Big Endian array (0–16,777,215).</summary>
        public static byte[] IntByteConvert3(long value)
        {
            return ExtractBytes(value, new long[] { 65536, 256, 1 });
        }

        /// <summary>Converts a long to a 4-byte Big Endian array (0–4,294,967,295).</summary>
        public static byte[] IntByteConvert4(long value)
        {
            return ExtractBytes(value, new long[] { 16777216, 65536, 256, 1 });
        }

        /// <summary>
        /// Shared byte extraction logic for all IntByteConvert variants.
        /// Iterates through positional base values (descending, i.e. most significant first),
        /// greedily extracting each digit and accumulating the remainder.
        /// Returns a Big Endian byte array (most significant byte at index 0).
        /// </summary>
        private static byte[] ExtractBytes(long value, long[] bases)
        {
            List<byte> bytes = new List<byte>();

            foreach (long aBs in bases)
            {
                if (aBs <= value)
                {
                    long digit = (value - (value % aBs)) / aBs;    // How many times does this base fit?
                    bytes.Add((byte)digit);
                    value -= aBs * digit;                           // Subtract the extracted portion
                }
                else
                    bytes.Add(0);   // This positional digit is zero
            }

            return bytes.ToArray(); // Already Big Endian — no reversal needed
        }

        /// <summary>
        /// Converts a single byte value (0–255) to its 8-character Big Endian binary string.
        /// MSB is written first (leftmost). Handles 255 as a fast-path special case.
        /// </summary>
        public static string Length8Convert(int n)
        {
            if (n == 255)
                return "11111111";  // Fast path for max byte value

            int val = 128;       // Start from MSB
            string bin = "";

            while (val != 1)
            {
                bin = bin + (n >= val ? "1" : "0");
                if (n >= val) n -= val;
                val /= 2;
            }

            bin = bin + (n == 1 ? "1" : "0");  // Final LSB
            return bin;
        }
    }
}