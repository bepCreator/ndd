# =============================================================================
# Binary Equation Paths — Python port of Program.cs
# Author:  Rich Wagner (original C#);  port preserves the C# logic 1:1.
# License: Apache 2.0 — https://www.apache.org/licenses/LICENSE-2.0
# Repo:    https://github.com/bepCreator/ndd
# Site:    https://newdawndata.com
#
# A lossless binary compression algorithm derived from a provably convergent
# modification of the Collatz Conjecture. Every integer >= 2 has a shorter
# binary path representation, recoverable through pure arithmetic with no
# dictionaries, indexes, or stored mappings.
#
# Core formula:
#   f(n) = n / 2          if n is even  (record current primary bit)
#   f(n) = (n - 1) / 2    if n is odd   (flip primary bit, then record)
#
# Convergence is guaranteed in exactly floor(log2(n)) steps.
# See PROOF_OF_CONVERGENCE.md for the formal mathematical proof.
#
# Endianness note:
#   BigEndian    — most significant byte first. Standard network/readable order.
#   LittleEndian — least significant byte first. Standard x86/memory order.
#   Both incoming binary strings and output binary strings respect the chosen
#   convention. Use the BE methods (default) or LE methods explicitly.
#   The val_compressor/val_decompressor methods operate on raw integers and
#   are endian-agnostic — byte order only applies to binary string I/O.
# =============================================================================

from enum import Enum
from typing import List


class ByteOrder(Enum):
    """Byte order convention to apply when reading or writing binary strings.

    BIG_ENDIAN    : most significant byte first  (e.g. 11001010 00110001)
    LITTLE_ENDIAN : least significant byte first (e.g. 00110001 11001010)
    """
    BIG_ENDIAN = 0
    LITTLE_ENDIAN = 1


# =============================================================================
# Core Binary Equation Paths algorithm.
#
# Compression records a "primary bit" (chars) at each step:
#   Even step : keep current primary bit, divide by 2.
#   Odd step  : flip primary bit, subtract 1, divide by 2.
# The resulting bit string is the compressed representation.
#
# Decompression reconstructs the original value by reversing the walk:
#   Each character in the path doubles val.
#   Each bit-flip (transition) adds 1 before doubling, reversing the subtract.
#
# Endianness applies only to binary string I/O — the integer walk itself
# is the same regardless of byte order. The flip_byte_order helper reverses
# the byte-chunk ordering of a binary string to convert between conventions.
# =============================================================================
class BEP:
    # =========================================================================
    # UNIFIED COMPRESS / DECOMPRESS ENTRY POINTS
    # These are the recommended public API. Pass byte_width (1–4) and
    # ByteOrder to select the appropriate variant automatically.
    # =========================================================================

    @staticmethod
    def compress(binary: str, byte_width: int, order: ByteOrder) -> str:
        """Compress a binary string using the BEP algorithm.

        Args:
            binary: Input binary string.
            byte_width: Number of bytes the input represents (1, 2, 3, or 4).
            order: Byte order of the input string.

        Returns:
            Compressed binary path string in the same byte order, or "error"
            on overflow.
        """
        if byte_width == 1:
            return BEP.compressor1_be(binary) if order == ByteOrder.BIG_ENDIAN else BEP.compressor1_le(binary)
        if byte_width == 2:
            return BEP.compressor2_be(binary) if order == ByteOrder.BIG_ENDIAN else BEP.compressor2_le(binary)
        if byte_width == 3:
            return BEP.compressor3_be(binary) if order == ByteOrder.BIG_ENDIAN else BEP.compressor3_le(binary)
        if byte_width == 4:
            return BEP.compressor4_be(binary) if order == ByteOrder.BIG_ENDIAN else BEP.compressor4_le(binary)
        raise ValueError("Supported widths: 1, 2, 3, 4.")

    @staticmethod
    def decompress(path: str, byte_width: int, order: ByteOrder) -> str:
        """Decompress a BEP path string back to its original binary representation."""
        if byte_width == 1:
            return BEP.decompressor1_be(path) if order == ByteOrder.BIG_ENDIAN else BEP.decompressor1_le(path)
        if byte_width == 2:
            return BEP.decompressor2_be(path) if order == ByteOrder.BIG_ENDIAN else BEP.decompressor2_le(path)
        if byte_width == 3:
            return BEP.decompressor3_be(path) if order == ByteOrder.BIG_ENDIAN else BEP.decompressor3_le(path)
        if byte_width == 4:
            return BEP.decompressor4_be(path) if order == ByteOrder.BIG_ENDIAN else BEP.decompressor4_le(path)
        raise ValueError("Supported widths: 1, 2, 3, 4.")

    # =========================================================================
    # VALUE-LEVEL COMPRESSION — endian-agnostic
    # Operates directly on an integer. No byte conversion, no byte order.
    # =========================================================================

    @staticmethod
    def val_compressor(val: int) -> str:
        """Compress an integer >= 2 into its Binary Equation Path bit string.

        Terminates in exactly floor(log2(val)) steps.
        """
        chars = "0"     # Primary bit — flips on every odd step
        opbinary = ""   # Path built right-to-left via prepend

        while val != 1:
            if val % 2 == 1:                # Odd: flip primary bit and subtract 1
                chars = "1" if chars == "0" else "0"
                val -= 1
            val //= 2                       # Right-shift (floor divide by 2)
            opbinary = chars + opbinary     # Prepend current primary bit

        return opbinary

    @staticmethod
    def val_decompressor(bts: str) -> int:
        """Decompress a BEP path string back to its original integer value.

        Detects bit transitions (flips) in the path to reverse each odd step.
        """
        # Last character indicates whether the original value was odd (1) or even (0)
        odd = int(bts[-1])

        val = 1         # Reconstruction starts from the convergence target
        lc = bts[0]     # Tracks previous character to detect flips

        for c in bts:
            if c != lc:
                val += 1    # Flip detected — reverse the odd step's subtract-1
            val *= 2        # Reverse the divide-by-2
            lc = c

        orig_val = val
        if odd == 1:
            orig_val += 1   # Restore final odd-step subtraction if needed

        return orig_val

    # =========================================================================
    # ENDIANNESS UTILITY
    # =========================================================================

    @staticmethod
    def flip_byte_order(binary: str) -> str:
        """Reverse the byte-chunk order of a binary string.

        Example (16-bit):
          BE input : "11001010 00110001"  →  LE output : "00110001 11001010"

        The bit order within each 8-bit chunk is preserved — only the chunk
        positions are swapped. Pads the final chunk to 8 bits if needed.
        """
        chunks: List[str] = []
        for i in range(0, len(binary), 8):
            remaining = min(8, len(binary) - i)
            chunk = binary[i:i + remaining]
            if len(chunk) < 8:
                chunk = chunk.ljust(8, '0')  # PadRight in C#
            chunks.append(chunk)
        chunks.reverse()
        return "".join(chunks)

    # =========================================================================
    # INTERNAL WALK — shared core logic used by all Compressor variants
    # =========================================================================

    @staticmethod
    def _run_compression(val: int, step_limit: int) -> str:
        """Run the BEP compression walk on a pre-converted integer value.

        Returns the compressed binary path string, or "error" on overflow.
        """
        chars = "0"
        opbinary = ""

        while val != 1:
            if val % 2 == 1:
                chars = "1" if chars == "0" else "0"
                val -= 1
            val //= 2
            if len(opbinary) >= step_limit:
                return "error"
            opbinary = chars + opbinary

        return opbinary

    @staticmethod
    def _run_decompression(bts: str) -> int:
        """Run the BEP decompression walk on a path string, returning the integer value."""
        odd = int(bts[-1])
        val = 1
        lc = bts[0]

        for c in bts:
            if c != lc:
                val += 1
            val *= 2
            lc = c

        orig_val = val
        if odd == 1:
            orig_val += 1
        return orig_val

    # =========================================================================
    # 1-BYTE COMPRESSORS / DECOMPRESSORS (8-bit, values 0–255)
    # =========================================================================

    @staticmethod
    def compressor1_be(used: str) -> str:
        bin_arr = BEP.bin_to_byte_arr_be(used)
        val = BEP.byte_long_convert(bin_arr, 256)
        return BEP._run_compression(val, 8)

    @staticmethod
    def decompressor1_be(bts: str) -> str:
        orig_val = BEP._run_decompression(bts)
        orig_bytes = BEP.int_byte_convert1(orig_val)
        return BEP.byte_arr_to_bin_be(orig_bytes)

    @staticmethod
    def compressor1_le(used: str) -> str:
        bin_arr = BEP.bin_to_byte_arr_be(BEP.flip_byte_order(used))
        val = BEP.byte_long_convert(bin_arr, 256)
        return BEP._run_compression(val, 8)

    @staticmethod
    def decompressor1_le(bts: str) -> str:
        orig_val = BEP._run_decompression(bts)
        orig_bytes = BEP.int_byte_convert1(orig_val)
        return BEP.flip_byte_order(BEP.byte_arr_to_bin_be(orig_bytes))

    # =========================================================================
    # 2-BYTE COMPRESSORS / DECOMPRESSORS (16-bit, values 0–65,535)
    # =========================================================================

    @staticmethod
    def compressor2_be(used: str) -> str:
        bin_arr = BEP.bin_to_byte_arr_be(used)
        val = BEP.byte_long_convert(bin_arr, 256)
        return BEP._run_compression(val, 16)

    @staticmethod
    def decompressor2_be(bts: str) -> str:
        orig_val = BEP._run_decompression(bts)
        orig_bytes = BEP.int_byte_convert2(orig_val)
        return BEP.byte_arr_to_bin_be(orig_bytes)

    @staticmethod
    def compressor2_le(used: str) -> str:
        bin_arr = BEP.bin_to_byte_arr_be(BEP.flip_byte_order(used))
        val = BEP.byte_long_convert(bin_arr, 256)
        return BEP._run_compression(val, 16)

    @staticmethod
    def decompressor2_le(bts: str) -> str:
        orig_val = BEP._run_decompression(bts)
        orig_bytes = BEP.int_byte_convert2(orig_val)
        return BEP.flip_byte_order(BEP.byte_arr_to_bin_be(orig_bytes))

    # =========================================================================
    # 3-BYTE COMPRESSORS / DECOMPRESSORS (24-bit, values 0–16,777,215)
    # =========================================================================

    @staticmethod
    def compressor3_be(used: str) -> str:
        bin_arr = BEP.bin_to_byte_arr_be(used)
        val = BEP.byte_long_convert(bin_arr, 256)
        return BEP._run_compression(val, 24)

    @staticmethod
    def decompressor3_be(bts: str) -> str:
        orig_val = BEP._run_decompression(bts)
        orig_bytes = BEP.int_byte_convert3(orig_val)
        return BEP.byte_arr_to_bin_be(orig_bytes)

    @staticmethod
    def compressor3_le(used: str) -> str:
        bin_arr = BEP.bin_to_byte_arr_be(BEP.flip_byte_order(used))
        val = BEP.byte_long_convert(bin_arr, 256)
        return BEP._run_compression(val, 24)

    @staticmethod
    def decompressor3_le(bts: str) -> str:
        orig_val = BEP._run_decompression(bts)
        orig_bytes = BEP.int_byte_convert3(orig_val)
        return BEP.flip_byte_order(BEP.byte_arr_to_bin_be(orig_bytes))

    # =========================================================================
    # 4-BYTE COMPRESSORS / DECOMPRESSORS (32-bit, values 0–4,294,967,295)
    # =========================================================================

    @staticmethod
    def compressor4_be(used: str) -> str:
        bin_arr = BEP.bin_to_byte_arr_be(used)
        val = BEP.byte_long_convert(bin_arr, 256)
        return BEP._run_compression(val, 32)

    @staticmethod
    def decompressor4_be(bts: str) -> str:
        orig_val = BEP._run_decompression(bts)
        orig_bytes = BEP.int_byte_convert4(orig_val)
        return BEP.byte_arr_to_bin_be(orig_bytes)

    @staticmethod
    def compressor4_le(used: str) -> str:
        bin_arr = BEP.bin_to_byte_arr_be(BEP.flip_byte_order(used))
        val = BEP.byte_long_convert(bin_arr, 256)
        return BEP._run_compression(val, 32)

    @staticmethod
    def decompressor4_le(bts: str) -> str:
        orig_val = BEP._run_decompression(bts)
        orig_bytes = BEP.int_byte_convert4(orig_val)
        return BEP.flip_byte_order(BEP.byte_arr_to_bin_be(orig_bytes))

    # =========================================================================
    # BYTE / BINARY CONVERSION UTILITIES
    # =========================================================================

    @staticmethod
    def bin_to_byte_arr_be(bits: str) -> List[int]:
        """Convert a Big Endian binary string to a byte array.

        Reads each 8-bit chunk MSB-first (bit[0] = weight 128, bit[7] = weight 1).
        Returned list is in Big Endian byte order (index 0 = most significant byte).
        """
        bytes_out: List[int] = []
        for x in range(0, len(bits), 8):
            value = 0
            # MSB-first: bit[0] carries weight 128, bit[7] carries weight 1
            value += int(bits[x]) * 128
            if (x + 1) < len(bits): value += int(bits[x + 1]) * 64
            if (x + 2) < len(bits): value += int(bits[x + 2]) * 32
            if (x + 3) < len(bits): value += int(bits[x + 3]) * 16
            if (x + 4) < len(bits): value += int(bits[x + 4]) * 8
            if (x + 5) < len(bits): value += int(bits[x + 5]) * 4
            if (x + 6) < len(bits): value += int(bits[x + 6]) * 2
            if (x + 7) < len(bits): value += int(bits[x + 7]) * 1
            bytes_out.append(value)
        return bytes_out

    @staticmethod
    def byte_arr_to_bin_be(bytes_in: List[int]) -> str:
        """Convert a byte array to a Big Endian binary string.

        Each byte is written MSB-first; bytes appear in array order.
        """
        return "".join(BEP.length8_convert(b) for b in bytes_in)

    @staticmethod
    def byte_long_convert(n: List[int], bse: int) -> int:
        """Convert a byte array to an integer using positional base arithmetic.

        Treats the array as Big Endian — index 0 is the most significant byte.
        """
        value = 0
        exponent = bse ** (len(n) - 1)  # Start at highest positional power
        for b in n:
            value += exponent * b
            exponent //= bse
        return value

    # --- Fixed-width int_byte_convert variants ---
    # Each converts an integer back to a fixed-size Big Endian byte array
    # by greedily extracting digits from the most significant position down.

    @staticmethod
    def int_byte_convert1(value: int) -> List[int]:
        """Convert an integer to a 1-byte Big Endian array (0–255)."""
        return BEP._extract_bytes(value, [1])

    @staticmethod
    def int_byte_convert2(value: int) -> List[int]:
        """Convert an integer to a 2-byte Big Endian array (0–65,535)."""
        return BEP._extract_bytes(value, [256, 1])

    @staticmethod
    def int_byte_convert3(value: int) -> List[int]:
        """Convert an integer to a 3-byte Big Endian array (0–16,777,215)."""
        return BEP._extract_bytes(value, [65536, 256, 1])

    @staticmethod
    def int_byte_convert4(value: int) -> List[int]:
        """Convert an integer to a 4-byte Big Endian array (0–4,294,967,295)."""
        return BEP._extract_bytes(value, [16777216, 65536, 256, 1])

    @staticmethod
    def _extract_bytes(value: int, bases: List[int]) -> List[int]:
        """Shared byte extraction logic for all int_byte_convert variants.

        Iterates positional base values (descending, most significant first),
        greedily extracting each digit and accumulating the remainder.
        Returns a Big Endian byte array.
        """
        bytes_out: List[int] = []
        for a_bs in bases:
            if a_bs <= value:
                digit = (value - (value % a_bs)) // a_bs
                bytes_out.append(digit)
                value -= a_bs * digit
            else:
                bytes_out.append(0)
        return bytes_out

    @staticmethod
    def length8_convert(n: int) -> str:
        """Convert a single byte value (0–255) to its 8-character Big Endian
        binary string.

        MSB is written first (leftmost). Handles 255 as a fast-path special case.
        """
        if n == 255:
            return "11111111"

        val = 128       # Start from MSB
        bin_str = ""

        while val != 1:
            bin_str = bin_str + ("1" if n >= val else "0")
            if n >= val:
                n -= val
            val //= 2

        bin_str = bin_str + ("1" if n == 1 else "0")  # Final LSB
        return bin_str


# =============================================================================
# DEMONSTRATION (mirrors Program.cs Main)
# =============================================================================

def round_trip(origin: str, byte_width: int, order: ByteOrder) -> None:
    """Compress and decompress a binary string, then print the result."""
    result = BEP.compress(origin, byte_width, order)
    restored = BEP.decompress(result, byte_width, order)
    tag = "BE" if order == ByteOrder.BIG_ENDIAN else "LE"
    result_len = "err" if result == "error" else str(len(result))
    print(f"  Origin  ({len(origin):2} bits) [{tag}]: {origin}")
    print(f"  Result  ({result_len:>3} bits) [{tag}]: {result}")
    print(f"  Restored          [{tag}]: {restored}")
    print(f"  Lossless              : {'YES' if origin == restored else 'NO'}\n")


def main() -> None:
    print("=============================================================")
    print(" Binary Equation Paths — Baseline Demonstration             ")
    print("=============================================================\n")

    # --- Value-level round trip (endian-agnostic) ---
    print("[ Value Compression — endian-agnostic ]")
    test_val = 3123733177
    compressed = BEP.val_compressor(test_val)
    decompressed = BEP.val_decompressor(compressed)
    print(f"  Original value  : {test_val}")
    print(f"  Compressed path : {compressed}  ({len(compressed)} bits)")
    print(f"  Decompressed    : {decompressed}")
    print(f"  Lossless        : {'YES' if test_val == decompressed else 'NO'}\n")

    # --- 4-byte Big Endian round trip ---
    print("[ 4-Byte Compression — Big Endian ]")
    round_trip("10011101010001100000110001011101", 4, ByteOrder.BIG_ENDIAN)

    # --- 4-byte Little Endian round trip ---
    print("[ 4-Byte Compression — Little Endian ]")
    round_trip("10111010000011000100011010011101", 4, ByteOrder.LITTLE_ENDIAN)

    # --- 1-byte both orders ---
    print("[ 1-Byte Compression — Big Endian ]")
    round_trip("11001010", 1, ByteOrder.BIG_ENDIAN)
    print("[ 1-Byte Compression — Little Endian ]")
    round_trip("01010011", 1, ByteOrder.LITTLE_ENDIAN)

    # --- 2-byte both orders ---
    print("[ 2-Byte Compression — Big Endian ]")
    round_trip("1100101000110001", 2, ByteOrder.BIG_ENDIAN)
    print("[ 2-Byte Compression — Little Endian ]")
    round_trip("0011000101010011", 2, ByteOrder.LITTLE_ENDIAN)

    # --- 3-byte both orders ---
    print("[ 3-Byte Compression — Big Endian ]")
    round_trip("110010100011000101001101", 3, ByteOrder.BIG_ENDIAN)
    print("[ 3-Byte Compression — Little Endian ]")
    round_trip("101001010011000101010011", 3, ByteOrder.LITTLE_ENDIAN)

    print("=============================================================")
    print(" Apache 2.0 — newdawndata.com")
    print("=============================================================")


if __name__ == "__main__":
    main()
