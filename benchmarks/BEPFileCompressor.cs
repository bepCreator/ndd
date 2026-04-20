// =============================================================================
// BEPFileCompressor — File-level compression and decompression
//
// File format:
//   [Header]
//     Magic       : 4 bytes  "BEP1"
//     ByteWidth   : 1 byte   chunk size (1–4)
//     PadBytes    : 1 byte   how many bytes were added to fill last chunk
//     OriginalLen : 8 bytes  original file length in bytes (ulong, LE)
//   [Body — bit packed]
//     For each chunk:
//       IsLiteral : 1 bit    (1 = value is 0 or 1, store raw; 0 = BEP path follows)
//       If IsLiteral = 1:
//         Raw     : byteWidth * 8 bits   original chunk value verbatim
//       If IsLiteral = 0:
//         Len     : LEN_BITS bits        path length (1 to byteWidth*8 - 1)
//         Path    : Len bits             the BEP path string
//
// LEN_BITS per byteWidth:
//   1 byte  → max path = 7  bits → 3 LEN_BITS (encodes 1–7)
//   2 bytes → max path = 15 bits → 4 LEN_BITS (encodes 1–15)
//   3 bytes → max path = 23 bits → 5 LEN_BITS (encodes 1–23)
//   4 bytes → max path = 31 bits → 5 LEN_BITS (encodes 1–31)
//
// Author:  Rich Wagner | License: Apache 2.0 | newdawndata.com
// =============================================================================

namespace BEPBenchmark.Core
{
    public static class BEPFileCompressor
    {
        private static readonly byte[] Magic = { (byte)'B', (byte)'E', (byte)'P', (byte)'1' };

        // Number of bits needed to store the path length for each byte width
        private static int LenBits(int byteWidth) => byteWidth switch
        {
            1 => 3,     // path 1–7,  fits in 3 bits
            2 => 4,     // path 1–15, fits in 4 bits
            3 => 5,     // path 1–23, fits in 5 bits
            4 => 5,     // path 1–31, fits in 5 bits
            _ => throw new ArgumentOutOfRangeException(nameof(byteWidth))
        };

        // -------------------------------------------------------------------------
        // COMPRESS
        // -------------------------------------------------------------------------

        /// <summary>
        /// Compresses a byte array using BEP, returning the compressed byte array.
        /// </summary>
        /// <param name="input">Raw input bytes.</param>
        /// <param name="byteWidth">Chunk size in bytes (1–4). Default 4.</param>
        /// <param name="order">Byte order for reading chunks.</param>
        public static byte[] Compress(byte[] input, int byteWidth = 4, ByteOrder order = ByteOrder.BigEndian)
        {
            if (byteWidth < 1 || byteWidth > 4)
                throw new ArgumentOutOfRangeException(nameof(byteWidth), "Supported: 1–4");

            int chunkBits = byteWidth * 8;
            int lenBits   = LenBits(byteWidth);

            // Pad input so it divides evenly into chunks
            int padBytes = (byteWidth - (input.Length % byteWidth)) % byteWidth;
            byte[] padded = new byte[input.Length + padBytes];
            Buffer.BlockCopy(input, 0, padded, 0, input.Length);

            var writer = new BitWriter();

            for (int i = 0; i < padded.Length; i += byteWidth)
            {
                // Extract chunk as bytes, then as a long integer
                byte[] chunk = new byte[byteWidth];
                Buffer.BlockCopy(padded, i, chunk, 0, byteWidth);

                // Respect byte order
                if (order == ByteOrder.LittleEndian)
                    Array.Reverse(chunk);   // Normalize to BE for integer conversion

                long val = BEP.ByteLongConvertBE(chunk);

                // Values 0 and 1 cannot be BEP-compressed — store literally
                if (val <= 1)
                {
                    writer.WriteBit(1);                         // IsLiteral flag
                    writer.WriteUInt((uint)val, chunkBits);     // Raw value
                    continue;
                }

                string path = BEP.ValCompressor(val);

                writer.WriteBit(0);                             // IsLiteral = 0 → path follows
                writer.WriteUInt((uint)path.Length, lenBits);  // Path length prefix
                writer.WriteBits(path);                         // Path bits
            }

            // Build header + body
            byte[] body   = writer.ToBytes();
            byte[] header = BuildHeader(byteWidth, (byte)padBytes, (ulong)input.Length);

            byte[] result = new byte[header.Length + body.Length];
            Buffer.BlockCopy(header, 0, result, 0, header.Length);
            Buffer.BlockCopy(body,   0, result, header.Length, body.Length);
            return result;
        }

        // -------------------------------------------------------------------------
        // DECOMPRESS
        // -------------------------------------------------------------------------

        /// <summary>
        /// Decompresses a BEP-compressed byte array back to the original bytes.
        /// </summary>
        public static byte[] Decompress(byte[] compressed)
        {
            // Parse header
            if (compressed.Length < 14)
                throw new InvalidDataException("File too short to be a valid BEP file.");

            for (int i = 0; i < Magic.Length; i++)
                if (compressed[i] != Magic[i])
                    throw new InvalidDataException("Invalid BEP magic bytes.");

            int    byteWidth   = compressed[4];
            int    padBytes    = compressed[5];
            ulong  originalLen = BitConverter.ToUInt64(compressed, 6);

            int chunkBits = byteWidth * 8;
            int lenBits   = LenBits(byteWidth);

            byte[] body = new byte[compressed.Length - 14];
            Buffer.BlockCopy(compressed, 14, body, 0, body.Length);

            var reader  = new BitReader(body);
            var output  = new List<byte>();

            while (!reader.EndOfStream && (ulong)output.Count < originalLen + (ulong)padBytes)
            {
                int isLiteral = reader.ReadBit();
                if (isLiteral == -1) break;

                if (isLiteral == 1)
                {
                    // Read raw literal value
                    uint rawVal = reader.ReadUInt(chunkBits);
                    byte[] rawBytes = UIntToBytes(rawVal, byteWidth);
                    output.AddRange(rawBytes);
                }
                else
                {
                    // Read path length then path
                    uint pathLen = reader.ReadUInt(lenBits);
                    string path  = reader.ReadBitString((int)pathLen);
                    long origVal = BEP.ValDecompressor(path);

                    byte[] origBytes = UIntToBytes((uint)origVal, byteWidth);
                    output.AddRange(origBytes);
                }
            }

            // Strip padding and return original length
            int totalBytes = output.Count - padBytes;
            return output.Take((int)originalLen).ToArray();
        }

        // -------------------------------------------------------------------------
        // STATISTICS (no file output — for benchmarking only)
        // -------------------------------------------------------------------------

        /// <summary>
        /// Calculates theoretical and practical BEP bit counts without writing a file.
        /// Returns (theoreticalBits, practicalBits, literalChunks, totalChunks).
        /// </summary>
        public static (long theoretical, long practical, int literals, int total)
            AnalyzeCompression(byte[] input, int byteWidth = 4, ByteOrder order = ByteOrder.BigEndian)
        {
            int  chunkBits   = byteWidth * 8;
            int  lenBits     = LenBits(byteWidth);
            int  padBytes    = (byteWidth - (input.Length % byteWidth)) % byteWidth;
            byte[] padded    = new byte[input.Length + padBytes];
            Buffer.BlockCopy(input, 0, padded, 0, input.Length);

            long theoretical = 0;   // Sum of raw path lengths
            long practical   = 0;   // Sum of (1 flag + lenBits + path) per chunk
            int  literals    = 0;
            int  total       = 0;

            for (int i = 0; i < padded.Length; i += byteWidth)
            {
                byte[] chunk = new byte[byteWidth];
                Buffer.BlockCopy(padded, i, chunk, 0, byteWidth);
                if (order == ByteOrder.LittleEndian) Array.Reverse(chunk);

                long val = BEP.ByteLongConvertBE(chunk);
                total++;

                if (val <= 1)
                {
                    // Literal: 1 flag bit + chunkBits raw bits
                    practical += 1 + chunkBits;
                    theoretical += chunkBits;
                    literals++;
                    continue;
                }

                string path   = BEP.ValCompressor(val);
                int pathLen   = path.Length;

                theoretical += pathLen;
                practical   += 1 + lenBits + pathLen;   // flag + length prefix + path
            }

            return (theoretical, practical, literals, total);
        }

        // -------------------------------------------------------------------------
        // HELPERS
        // -------------------------------------------------------------------------

        private static byte[] BuildHeader(int byteWidth, byte padBytes, ulong originalLen)
        {
            var header = new byte[14];
            Buffer.BlockCopy(Magic, 0, header, 0, 4);
            header[4] = (byte)byteWidth;
            header[5] = padBytes;
            Buffer.BlockCopy(BitConverter.GetBytes(originalLen), 0, header, 6, 8);
            return header;
        }

        private static byte[] UIntToBytes(uint value, int byteWidth)
        {
            byte[] bytes = new byte[byteWidth];
            for (int i = byteWidth - 1; i >= 0; i--)
            {
                bytes[i] = (byte)(value & 0xFF);
                value >>= 8;
            }
            return bytes;
        }
    }
}
