// =============================================================================
// Binary Equation Paths — C++ port of Program.cs
// Author:  Rich Wagner (original C#);  port preserves the C# logic 1:1.
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
//   f(n) = (n - 1) / 2    if n is odd   (flip primary bit, then record)
//
// Convergence is guaranteed in exactly floor(log2(n)) steps.
// See PROOF_OF_CONVERGENCE.md for the formal mathematical proof.
//
// Endianness note:
//   BigEndian    — most significant byte first. Standard network/readable order.
//   LittleEndian — least significant byte first. Standard x86/memory order.
//   Both incoming binary strings and output binary strings respect the chosen
//   convention. Use the BE methods (default) or LE methods explicitly.
//   The ValCompressor/ValDecompressor methods operate on raw integers and are
//   endian-agnostic — byte order only applies to binary string I/O.
//
// Build (any C++17 compiler):
//   g++ -std=c++17 -O2 -o bep bep.cpp
//   clang++ -std=c++17 -O2 -o bep bep.cpp
// =============================================================================

#include <algorithm>
#include <cstdint>
#include <iomanip>
#include <iostream>
#include <stdexcept>
#include <string>
#include <vector>

namespace BinaryEquationPaths {

// =============================================================================
// Byte order convention to apply when reading or writing binary strings.
//   BigEndian    : most significant byte first  (e.g. 11001010 00110001)
//   LittleEndian : least significant byte first (e.g. 00110001 11001010)
// =============================================================================
enum class ByteOrder { BigEndian, LittleEndian };

// =============================================================================
// Core Binary Equation Paths algorithm.
//
// Compression records a "primary bit" (chars) at each step:
//   Even step : keep current primary bit, divide by 2.
//   Odd step  : flip primary bit, subtract 1, divide by 2.
// The resulting bit string is the compressed representation.
//
// Decompression reconstructs the original value by reversing the walk:
//   Each character in the path doubles val.
//   Each bit-flip (transition) adds 1 before doubling, reversing the subtract.
//
// Endianness applies only to binary string I/O — the integer walk itself
// is the same regardless of byte order. The FlipByteOrder helper reverses
// the byte-chunk ordering of a binary string to convert between conventions.
// =============================================================================
class BEP {
public:
    // -------------------------------------------------------------------------
    // UNIFIED COMPRESS / DECOMPRESS ENTRY POINTS
    // These are the recommended public API. Pass byteWidth (1–4) and
    // ByteOrder to select the appropriate variant automatically.
    // -------------------------------------------------------------------------

    /// Compress a binary string using the BEP algorithm.
    /// Returns the compressed path string, or "error" on overflow.
    static std::string Compress(const std::string& binary, int byteWidth, ByteOrder order) {
        switch (byteWidth) {
            case 1: return order == ByteOrder::BigEndian ? Compressor1BE(binary) : Compressor1LE(binary);
            case 2: return order == ByteOrder::BigEndian ? Compressor2BE(binary) : Compressor2LE(binary);
            case 3: return order == ByteOrder::BigEndian ? Compressor3BE(binary) : Compressor3LE(binary);
            case 4: return order == ByteOrder::BigEndian ? Compressor4BE(binary) : Compressor4LE(binary);
            default: throw std::out_of_range("Supported widths: 1, 2, 3, 4.");
        }
    }

    /// Decompress a BEP path string back to its original binary representation.
    static std::string Decompress(const std::string& path, int byteWidth, ByteOrder order) {
        switch (byteWidth) {
            case 1: return order == ByteOrder::BigEndian ? Decompressor1BE(path) : Decompressor1LE(path);
            case 2: return order == ByteOrder::BigEndian ? Decompressor2BE(path) : Decompressor2LE(path);
            case 3: return order == ByteOrder::BigEndian ? Decompressor3BE(path) : Decompressor3LE(path);
            case 4: return order == ByteOrder::BigEndian ? Decompressor4BE(path) : Decompressor4LE(path);
            default: throw std::out_of_range("Supported widths: 1, 2, 3, 4.");
        }
    }

    // -------------------------------------------------------------------------
    // VALUE-LEVEL COMPRESSION — endian-agnostic
    // Operates directly on a 64-bit integer. No byte conversion, no byte order.
    // -------------------------------------------------------------------------

    /// Compress a positive integer (>= 2) into its BEP bit string.
    /// Terminates in exactly floor(log2(val)) steps.
    static std::string ValCompressor(int64_t val) {
        std::string chars = "0";    // Primary bit — flips on every odd step
        std::string opbinary = "";  // Path built right-to-left via prepend

        while (val != 1) {
            if (val % 2 == 1) {                 // Odd: flip primary bit and subtract 1
                chars = (chars == "0") ? "1" : "0";
                val -= 1;
            }
            val /= 2;                           // Right-shift (floor divide by 2)
            opbinary = chars + opbinary;        // Prepend current primary bit
        }
        return opbinary;
    }

    /// Decompress a BEP path string back to its original integer value.
    /// Detects bit transitions (flips) in the path to reverse each odd step.
    static int64_t ValDecompressor(const std::string& bts) {
        // Last character indicates whether the original value was odd (1) or even (0)
        int64_t odd = bts.back() - '0';

        int64_t val = 1;        // Reconstruction starts from the convergence target
        char lc = bts.front();  // Tracks previous character to detect flips

        for (char c : bts) {
            if (c != lc) val += 1;  // Flip detected — reverse the odd step's subtract-1
            val *= 2;               // Reverse the divide-by-2
            lc = c;
        }

        int64_t origVal = val;
        if (odd == 1) origVal += 1;  // Restore final odd-step subtraction if needed
        return origVal;
    }

    // -------------------------------------------------------------------------
    // ENDIANNESS UTILITY
    // -------------------------------------------------------------------------

    /// Reverse the byte-chunk order of a binary string.
    ///
    /// Example (16-bit):
    ///   BE input : "11001010 00110001"  →  LE output : "00110001 11001010"
    ///
    /// The bit order within each 8-bit chunk is preserved — only the chunk
    /// positions are swapped. Pads the final chunk to 8 bits if needed.
    static std::string FlipByteOrder(const std::string& binary) {
        std::vector<std::string> chunks;
        // Split into 8-bit chunks, padding the last if the string isn't byte-aligned
        for (size_t i = 0; i < binary.size(); i += 8) {
            size_t remaining = std::min<size_t>(8, binary.size() - i);
            std::string chunk = binary.substr(i, remaining);
            if (chunk.size() < 8) chunk.append(8 - chunk.size(), '0'); // Pad incomplete trailing byte
            chunks.push_back(chunk);
        }
        std::reverse(chunks.begin(), chunks.end());  // Reverse byte order
        std::string result;
        for (const auto& c : chunks) result += c;    // Rejoin into a single string
        return result;
    }

    // -------------------------------------------------------------------------
    // 1-BYTE COMPRESSORS / DECOMPRESSORS (8-bit, values 0–255)
    // -------------------------------------------------------------------------

    static std::string Compressor1BE(const std::string& used) {
        // BE input: read as-is — MSB first, convert to integer, compress
        auto bin = BinToByteArrBE(used);
        int64_t val = ByteLongConvert(bin, 256);
        return RunCompression(val, 8);
    }

    static std::string Decompressor1BE(const std::string& bts) {
        int64_t origVal = RunDecompression(bts);
        auto origBytes = IntByteConvert1(origVal);
        return ByteArrToBinBE(origBytes);
    }

    static std::string Compressor1LE(const std::string& used) {
        // LE input: flip byte order to BE before integer conversion
        auto bin = BinToByteArrBE(FlipByteOrder(used));
        int64_t val = ByteLongConvert(bin, 256);
        return RunCompression(val, 8);
    }

    static std::string Decompressor1LE(const std::string& bts) {
        int64_t origVal = RunDecompression(bts);
        auto origBytes = IntByteConvert1(origVal);
        // Flip the output binary string to Little Endian byte order
        return FlipByteOrder(ByteArrToBinBE(origBytes));
    }

    // -------------------------------------------------------------------------
    // 2-BYTE COMPRESSORS / DECOMPRESSORS (16-bit, values 0–65,535)
    // -------------------------------------------------------------------------

    static std::string Compressor2BE(const std::string& used) {
        auto bin = BinToByteArrBE(used);
        int64_t val = ByteLongConvert(bin, 256);
        return RunCompression(val, 16);
    }

    static std::string Decompressor2BE(const std::string& bts) {
        int64_t origVal = RunDecompression(bts);
        auto origBytes = IntByteConvert2(origVal);
        return ByteArrToBinBE(origBytes);
    }

    static std::string Compressor2LE(const std::string& used) {
        auto bin = BinToByteArrBE(FlipByteOrder(used));
        int64_t val = ByteLongConvert(bin, 256);
        return RunCompression(val, 16);
    }

    static std::string Decompressor2LE(const std::string& bts) {
        int64_t origVal = RunDecompression(bts);
        auto origBytes = IntByteConvert2(origVal);
        return FlipByteOrder(ByteArrToBinBE(origBytes));
    }

    // -------------------------------------------------------------------------
    // 3-BYTE COMPRESSORS / DECOMPRESSORS (24-bit, values 0–16,777,215)
    // -------------------------------------------------------------------------

    static std::string Compressor3BE(const std::string& used) {
        auto bin = BinToByteArrBE(used);
        int64_t val = ByteLongConvert(bin, 256);
        return RunCompression(val, 24);
    }

    static std::string Decompressor3BE(const std::string& bts) {
        int64_t origVal = RunDecompression(bts);
        auto origBytes = IntByteConvert3(origVal);
        return ByteArrToBinBE(origBytes);
    }

    static std::string Compressor3LE(const std::string& used) {
        auto bin = BinToByteArrBE(FlipByteOrder(used));
        int64_t val = ByteLongConvert(bin, 256);
        return RunCompression(val, 24);
    }

    static std::string Decompressor3LE(const std::string& bts) {
        int64_t origVal = RunDecompression(bts);
        auto origBytes = IntByteConvert3(origVal);
        return FlipByteOrder(ByteArrToBinBE(origBytes));
    }

    // -------------------------------------------------------------------------
    // 4-BYTE COMPRESSORS / DECOMPRESSORS (32-bit, values 0–4,294,967,295)
    // -------------------------------------------------------------------------

    static std::string Compressor4BE(const std::string& used) {
        auto bin = BinToByteArrBE(used);
        int64_t val = ByteLongConvert(bin, 256);
        return RunCompression(val, 32);
    }

    static std::string Decompressor4BE(const std::string& bts) {
        int64_t origVal = RunDecompression(bts);
        auto origBytes = IntByteConvert4(origVal);
        return ByteArrToBinBE(origBytes);
    }

    static std::string Compressor4LE(const std::string& used) {
        auto bin = BinToByteArrBE(FlipByteOrder(used));
        int64_t val = ByteLongConvert(bin, 256);
        return RunCompression(val, 32);
    }

    static std::string Decompressor4LE(const std::string& bts) {
        int64_t origVal = RunDecompression(bts);
        auto origBytes = IntByteConvert4(origVal);
        return FlipByteOrder(ByteArrToBinBE(origBytes));
    }

    // -------------------------------------------------------------------------
    // BYTE / BINARY CONVERSION UTILITIES
    // -------------------------------------------------------------------------

    /// Convert a Big Endian binary string to a byte vector.
    /// Reads each 8-bit chunk MSB-first (bit[0] = weight 128, bit[7] = weight 1).
    /// Returned vector is in Big Endian byte order.
    static std::vector<uint8_t> BinToByteArrBE(const std::string& bits) {
        std::vector<uint8_t> bytes;
        for (size_t x = 0; x < bits.size(); x += 8) {
            int value = 0;
            // MSB-first: bit[0] carries weight 128, bit[7] carries weight 1
            value += (bits[x] - '0') * 128;
            if ((x + 1) < bits.size()) value += (bits[x + 1] - '0') * 64;
            if ((x + 2) < bits.size()) value += (bits[x + 2] - '0') * 32;
            if ((x + 3) < bits.size()) value += (bits[x + 3] - '0') * 16;
            if ((x + 4) < bits.size()) value += (bits[x + 4] - '0') * 8;
            if ((x + 5) < bits.size()) value += (bits[x + 5] - '0') * 4;
            if ((x + 6) < bits.size()) value += (bits[x + 6] - '0') * 2;
            if ((x + 7) < bits.size()) value += (bits[x + 7] - '0') * 1;
            bytes.push_back(static_cast<uint8_t>(value));
        }
        return bytes;
    }

    /// Convert a byte vector to a Big Endian binary string.
    /// Each byte is written MSB-first; bytes appear in vector order.
    static std::string ByteArrToBinBE(const std::vector<uint8_t>& bytes) {
        std::string bin;
        for (uint8_t b : bytes) bin += Length8Convert(b);
        return bin;
    }

    /// Convert a byte vector to an integer using positional base arithmetic.
    /// Treats the vector as Big Endian — index 0 is the most significant byte.
    /// Example: bytes [202, 49] → (202 × 256¹) + (49 × 256⁰) = 51,762.
    static int64_t ByteLongConvert(const std::vector<uint8_t>& n, int64_t bse) {
        int64_t value = 0;
        // Start at highest positional power
        int64_t exponent = 1;
        for (size_t i = 0; i < n.size() - 1; ++i) exponent *= bse;
        for (uint8_t b : n) {
            value += exponent * b;
            exponent /= bse;
        }
        return value;
    }

    // --- Fixed-width IntByteConvert variants ---
    // Each converts an integer back to a fixed-size Big Endian byte vector
    // by greedily extracting digits from the most significant position down.

    /// Convert an integer to a 1-byte Big Endian vector (0–255).
    static std::vector<uint8_t> IntByteConvert1(int64_t value) {
        return ExtractBytes(value, {1});
    }

    /// Convert an integer to a 2-byte Big Endian vector (0–65,535).
    static std::vector<uint8_t> IntByteConvert2(int64_t value) {
        return ExtractBytes(value, {256, 1});
    }

    /// Convert an integer to a 3-byte Big Endian vector (0–16,777,215).
    static std::vector<uint8_t> IntByteConvert3(int64_t value) {
        return ExtractBytes(value, {65536, 256, 1});
    }

    /// Convert an integer to a 4-byte Big Endian vector (0–4,294,967,295).
    static std::vector<uint8_t> IntByteConvert4(int64_t value) {
        return ExtractBytes(value, {16777216, 65536, 256, 1});
    }

    /// Convert a single byte value (0–255) to its 8-character Big Endian
    /// binary string. MSB is written first (leftmost). Handles 255 as a
    /// fast-path special case.
    static std::string Length8Convert(int n) {
        if (n == 255) return "11111111";

        int val = 128;          // Start from MSB
        std::string bin = "";

        while (val != 1) {
            bin += (n >= val ? '1' : '0');
            if (n >= val) n -= val;
            val /= 2;
        }
        bin += (n == 1 ? '1' : '0');  // Final LSB
        return bin;
    }

private:
    // -------------------------------------------------------------------------
    // INTERNAL WALK — shared core logic used by all Compressor variants
    // -------------------------------------------------------------------------

    /// Run the BEP compression walk on a pre-converted integer value.
    /// Returns the compressed binary path string, or "error" on overflow.
    static std::string RunCompression(int64_t val, int stepLimit) {
        std::string chars = "0";
        std::string opbinary = "";

        while (val != 1) {
            if (val % 2 == 1) {
                chars = (chars == "0") ? "1" : "0";
                val -= 1;
            }
            val /= 2;
            if (static_cast<int>(opbinary.size()) >= stepLimit) return "error";
            opbinary = chars + opbinary;
        }
        return opbinary;
    }

    /// Run the BEP decompression walk on a path string, returning the integer.
    static int64_t RunDecompression(const std::string& bts) {
        int64_t odd = bts.back() - '0';
        int64_t val = 1;
        char lc = bts.front();

        for (char c : bts) {
            if (c != lc) val += 1;
            val *= 2;
            lc = c;
        }
        int64_t origVal = val;
        if (odd == 1) origVal += 1;
        return origVal;
    }

    /// Shared byte extraction logic for all IntByteConvert variants.
    /// Iterates positional base values (descending, most significant first),
    /// greedily extracting each digit and accumulating the remainder.
    /// Returns a Big Endian byte vector.
    static std::vector<uint8_t> ExtractBytes(int64_t value, const std::vector<int64_t>& bases) {
        std::vector<uint8_t> bytes;
        for (int64_t aBs : bases) {
            if (aBs <= value) {
                int64_t digit = (value - (value % aBs)) / aBs;  // How many times does this base fit?
                bytes.push_back(static_cast<uint8_t>(digit));
                value -= aBs * digit;                            // Subtract the extracted portion
            } else {
                bytes.push_back(0);  // This positional digit is zero
            }
        }
        return bytes;  // Already Big Endian — no reversal needed
    }
};

// =============================================================================
// DEMONSTRATION (mirrors Program.cs Main)
// =============================================================================

static void RoundTrip(const std::string& origin, int byteWidth, ByteOrder order) {
    std::string result = BEP::Compress(origin, byteWidth, order);
    std::string restored = BEP::Decompress(result, byteWidth, order);
    std::string tag = (order == ByteOrder::BigEndian) ? "BE" : "LE";
    std::string resultLen = (result == "error") ? "err" : std::to_string(result.size());
    std::cout << "  Origin  (" << std::setw(2) << origin.size() << " bits) [" << tag << "]: " << origin << "\n";
    std::cout << "  Result  (" << std::setw(3) << resultLen << " bits) [" << tag << "]: " << result << "\n";
    std::cout << "  Restored          [" << tag << "]: " << restored << "\n";
    std::cout << "  Lossless              : " << (origin == restored ? "YES" : "NO") << "\n\n";
}

} // namespace BinaryEquationPaths

int main() {
    using namespace BinaryEquationPaths;

    std::cout << "=============================================================\n";
    std::cout << " Binary Equation Paths — Baseline Demonstration             \n";
    std::cout << "=============================================================\n\n";

    // --- Value-level round trip (endian-agnostic) ---
    std::cout << "[ Value Compression — endian-agnostic ]\n";
    int64_t testVal = 3123733177LL;
    std::string compressed = BEP::ValCompressor(testVal);
    int64_t decompressed = BEP::ValDecompressor(compressed);
    std::cout << "  Original value  : " << testVal << "\n";
    std::cout << "  Compressed path : " << compressed << "  (" << compressed.size() << " bits)\n";
    std::cout << "  Decompressed    : " << decompressed << "\n";
    std::cout << "  Lossless        : " << (testVal == decompressed ? "YES" : "NO") << "\n\n";

    // --- 4-byte Big Endian round trip ---
    std::cout << "[ 4-Byte Compression — Big Endian ]\n";
    RoundTrip("10011101010001100000110001011101", 4, ByteOrder::BigEndian);

    // --- 4-byte Little Endian round trip ---
    std::cout << "[ 4-Byte Compression — Little Endian ]\n";
    RoundTrip("10111010000011000100011010011101", 4, ByteOrder::LittleEndian);

    // --- 1-byte both orders ---
    std::cout << "[ 1-Byte Compression — Big Endian ]\n";
    RoundTrip("11001010", 1, ByteOrder::BigEndian);
    std::cout << "[ 1-Byte Compression — Little Endian ]\n";
    RoundTrip("01010011", 1, ByteOrder::LittleEndian);

    // --- 2-byte both orders ---
    std::cout << "[ 2-Byte Compression — Big Endian ]\n";
    RoundTrip("1100101000110001", 2, ByteOrder::BigEndian);
    std::cout << "[ 2-Byte Compression — Little Endian ]\n";
    RoundTrip("0011000101010011", 2, ByteOrder::LittleEndian);

    // --- 3-byte both orders ---
    std::cout << "[ 3-Byte Compression — Big Endian ]\n";
    RoundTrip("110010100011000101001101", 3, ByteOrder::BigEndian);
    std::cout << "[ 3-Byte Compression — Little Endian ]\n";
    RoundTrip("101001010011000101010011", 3, ByteOrder::LittleEndian);

    std::cout << "=============================================================\n";
    std::cout << " Apache 2.0 — newdawndata.com\n";
    std::cout << "=============================================================\n";
    return 0;
}
