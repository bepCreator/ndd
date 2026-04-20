// =============================================================================
// BitStream — Bit-level reader and writer
// Used to pack variable-length BEP paths efficiently into byte arrays.
// Author:  Rich Wagner | License: Apache 2.0 | newdawndata.com
// =============================================================================

namespace BEPBenchmark.Core
{
    // =========================================================================
    /// <summary>
    /// Writes individual bits into a growable byte buffer.
    /// Bits are packed MSB-first within each byte.
    /// Call ToBytes() to retrieve the final packed byte array (zero-padded
    /// to the nearest byte boundary).
    /// </summary>
    // =========================================================================
    public class BitWriter
    {
        private readonly List<byte> _buffer = new();
        private byte  _current  = 0;    // Byte currently being filled
        private int   _bitIndex = 7;    // Next bit position to write (7 = MSB)

        /// <summary>Total number of bits written so far.</summary>
        public long BitCount { get; private set; }

        /// <summary>Writes a single bit (0 or 1).</summary>
        public void WriteBit(int bit)
        {
            if (bit == 1)
                _current |= (byte)(1 << _bitIndex);

            _bitIndex--;
            BitCount++;

            if (_bitIndex < 0)
            {
                _buffer.Add(_current);
                _current  = 0;
                _bitIndex = 7;
            }
        }

        /// <summary>Writes a bit string (e.g. "10110") one character at a time.</summary>
        public void WriteBits(string bits)
        {
            foreach (char c in bits)
                WriteBit(c == '1' ? 1 : 0);
        }

        /// <summary>
        /// Writes an unsigned integer value using exactly <paramref name="bitCount"/> bits,
        /// MSB first. Used for fixed-width length prefixes.
        /// </summary>
        public void WriteUInt(uint value, int bitCount)
        {
            for (int i = bitCount - 1; i >= 0; i--)
                WriteBit((int)((value >> i) & 1));
        }

        /// <summary>
        /// Flushes any remaining partial byte (zero-padded) and returns the
        /// complete packed byte array.
        /// </summary>
        public byte[] ToBytes()
        {
            if (_bitIndex < 7)
                _buffer.Add(_current);  // Flush partial byte (remaining bits = 0)
            return _buffer.ToArray();
        }
    }

    // =========================================================================
    /// <summary>
    /// Reads individual bits from a byte array.
    /// Bits are read MSB-first within each byte, matching BitWriter's convention.
    /// </summary>
    // =========================================================================
    public class BitReader
    {
        private readonly byte[] _buffer;
        private int _byteIndex = 0;
        private int _bitIndex  = 7;    // Next bit position to read (7 = MSB)

        /// <summary>Total bits consumed so far.</summary>
        public long BitsRead { get; private set; }

        /// <summary>True when all bits in the buffer have been read.</summary>
        public bool EndOfStream => _byteIndex >= _buffer.Length;

        public BitReader(byte[] buffer)
        {
            _buffer = buffer;
        }

        /// <summary>Reads a single bit. Returns -1 at end of stream.</summary>
        public int ReadBit()
        {
            if (EndOfStream) return -1;

            int bit = (_buffer[_byteIndex] >> _bitIndex) & 1;
            _bitIndex--;
            BitsRead++;

            if (_bitIndex < 0)
            {
                _byteIndex++;
                _bitIndex = 7;
            }

            return bit;
        }

        /// <summary>
        /// Reads <paramref name="bitCount"/> bits and returns them as an unsigned integer.
        /// MSB first — matches WriteUInt convention.
        /// </summary>
        public uint ReadUInt(int bitCount)
        {
            uint value = 0;
            for (int i = 0; i < bitCount; i++)
            {
                int bit = ReadBit();
                if (bit == -1) break;
                value = (value << 1) | (uint)bit;
            }
            return value;
        }

        /// <summary>Reads <paramref name="bitCount"/> bits as a binary string.</summary>
        public string ReadBitString(int bitCount)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < bitCount; i++)
            {
                int bit = ReadBit();
                if (bit == -1) break;
                sb.Append(bit == 1 ? '1' : '0');
            }
            return sb.ToString();
        }
    }
}
