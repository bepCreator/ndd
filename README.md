# Binary Equation Paths

**Author:** Rich Wagner  
**Date:** March 11, 2026  
**Revision:** May 18, 2026  
**Contact:** rich@newdawndata.com  
**Compressor:** [inBEP](https://github.com/bepCreator/inBEP)

---

## Abstract

Traditional computing has utilized the base-256 model for binary management since IBM announced the System/360 on April 7, 1964, which standardized the 8-bit byte. The binary numeral system itself was developed by mathematician Gottfried Leibniz in the late 17th century (manuscript drafts dating to 1679) and published as *Explication de l'Arithmétique Binaire* in 1703. His correspondence with Jesuit missionary Joachim Bouvet later revealed that the 64 hexagrams of the *I Ching* — reorganized by Shao Yong in the 11th century from the older divination manual and philosophical framework — corresponded exactly to the integers 0–63 in his binary system.

Binary was conceived as a universal representation of information. By assuming binary randomness, an equation for a shorter binary path can be found for every positive integer greater than 1, achieved by transforming the Collatz Conjecture into a path-based, strictly-decreasing construction. Two complementary walks emerge — a **floor walk** that strips the least significant bit at each step, and a **ceil walk** that rounds upward — and both converge to 1 with provably optimal step counts of `⌊log₂(n)⌋` and `⌈log₂(n)⌉` respectively.

```mermaid
xychart-beta
    title "BEP floor-walk path length vs byte-aligned binary length"
    x-axis "BEP path length L (bits)" [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16]
    y-axis "Bits to represent the value" 0 --> 28
    bar  [8, 8, 8, 8, 8, 8, 8, 8, 16, 16, 16, 16, 16, 16, 16, 24]
    line [1, 2, 3, 4, 5, 6, 7, 8,  9, 10, 11, 12, 13, 14, 15, 16]
```

---

## Table of Contents

1. [Introduction](#1-introduction)
2. [Proof of Convergence](#2-proof-of-convergence)
   * [Definitions](#definitions)
   * [Floor Walk Convergence](#floor-walk-convergence)
     * [Lemma F1 — Closure on Positive Integers](#lemma-f1--closure-on-positive-integers)
     * [Lemma F2 — Strict Decrease](#lemma-f2--strict-decrease)
     * [Lemma F3 — No Fixed Point in the Active Domain](#lemma-f3--no-fixed-point-in-the-active-domain)
     * [Theorem F — Convergence to 1](#theorem-f--convergence-to-1)
     * [Step Count Bound — Floor Walk](#step-count-bound--floor-walk)
   * [Ceil Walk Convergence](#ceil-walk-convergence)
     * [Lemma C1 — Closure on Positive Integers](#lemma-c1--closure-on-positive-integers)
     * [Lemma C2 — Strict Decrease](#lemma-c2--strict-decrease)
     * [Lemma C3 — 1 is the Unique Fixed Point](#lemma-c3--1-is-the-unique-fixed-point)
     * [Theorem C — Convergence to 1](#theorem-c--convergence-to-1)
     * [Step Count Bound — Ceil Walk](#step-count-bound--ceil-walk)
   * [Comparison of the Two Walks](#comparison-of-the-two-walks)
   * [Contrast with the Original Collatz Conjecture](#contrast-with-the-original-collatz-conjecture)
3. [Compression](#3-compression)
   * [Floor Walk — Worked Example](#floor-walk--worked-example)
   * [Ceil Walk — Worked Example](#ceil-walk--worked-example)
   * [Samples](#samples)
   * [Numeric Observations](#numeric-observations-in-current-byte-structures)
   * [Compression Functions](#compression-functions)
   * [Decompression Functions](#decompression-functions)
4. [Endianness](#4-endianness)
   * [What is Byte Order?](#what-is-byte-order)
   * [How BEP Handles Endianness](#how-bep-handles-endianness)
   * [FlipByteOrder Helper](#flipbyteorder-helper)
   * [Unified API](#unified-api)
   * [Big Endian Examples](#big-endian-examples)
   * [Little Endian Examples](#little-endian-examples)
   * [Value Compression — Endian-Agnostic](#value-compression--endian-agnostic)

---

## 1. Introduction

Binary is a base-2 numbering system where each digit (bit) is either `0` or `1`. Converting to base-256 means grouping those bits into chunks of 8 (since 2⁸ = 256), where each chunk forms a single "digit" in the base-256 system with a value ranging from 0 to 255. The examples below use Big-Endian convention for readability.

**Bit-positional formula:**

```
Total = y + (x * z)
```

| Position weight (z) |   1 |   2 |   4 |   8 |  16 |  32 |  64 | 128 |
|---------------------|----:|----:|----:|----:|----:|----:|----:|----:|
| Bit (x)             |   0 |   1 |   0 |   1 |   1 |   0 |   1 |   1 |
| **Running total (y)** | **0** | **2** | **2** | **10** | **26** | **26** | **90** | **218** |

The running total after the last position is 218, the decimal value of the byte `11011010` (reading bits from most to least significant).

For example, the 16-bit binary number `11001010 00110001` consists of two 8-bit groups: `11001010` (202 in decimal) and `00110001` (49 in decimal), making it the two-digit base-256 number `[202, 49]`. This is exactly how computers represent raw bytes — every byte is one base-256 digit — which is why base-256 is so natural in computing:

* A 32-bit integer is a 4-digit base-256 number
* An IPv4 address is a 4-digit base-256 number written with dots (e.g., `192.168.1.1`)
* Encoding schemes like Base64 exist to compress or transport these base-256 byte sequences in text-safe formats

The positional value of each digit follows the same rule as any base system: the rightmost digit is multiplied by 256⁰, the next by 256¹, then 256², and so on:

```
[202, 49] = (202 × 256¹) + (49 × 256⁰) = 51,712 + 49 = 51,761 in decimal
```

By taking this decimal as an integer and applying an alteration of the Collatz Conjecture formula, a shorter path can be found, resulting in a smaller binary string.

### The Collatz Conjecture

The Collatz Conjecture is a simple but unsolved problem in mathematics that studies what happens when you repeatedly apply a rule to any positive integer:

* If the number is **even**, divide it by 2
* If the number is **odd**, multiply it by 3 and add 1
* Repeat with the new number

**Standard Collatz Function:**

```
a(k+1) = a(k) / 2          if a(k) is even
a(k+1) = 3 * a(k) + 1      if a(k) is odd
```

For example, starting with 13:

```
13 → 40 → 20 → 10 → 5 → 16 → 8 → 4 → 2 → 1
```

The conjecture states that no matter which positive integer you start with, the sequence will eventually reach 1. Despite the simplicity of the rule, this has never been formally proven for all integers.

### Binary Equation Path Functions

By altering the odd-case rule to **adjust by one** rather than multiply by three and add one, the open question at the heart of the Collatz Conjecture is removed. Two complementary adjustments exist — subtracting one before halving (the **floor walk**) or adding one before halving (the **ceil walk**). Both produce strictly decreasing sequences that converge to 1, and each has a closed-form step count.

**Floor Walk — Binary Equation Path Formula (subtracts on odd):**

```
f(n) = n / 2          if n is even   → add primary bit
f(n) = (n - 1) / 2    if n is odd    → swap and add primary bit
```

This is equivalent to `f(n) = ⌊n/2⌋` — an integer right-shift that strips the least significant bit at each step.

**Ceil Walk — Binary Equation Path Formula (adds on odd):**

```
g(n) = n / 2          if n is even   → add primary bit
g(n) = (n + 1) / 2    if n is odd    → swap and add primary bit
```

This is equivalent to `g(n) = ⌈n/2⌉` — the upward-rounded counterpart of `f`.

Both methodologies take the binary numeric representation of any positive integer ≥ 2 and convert the compressed binary string into an equation. The floor walk reaches 1 in exactly `⌊log₂(n)⌋` steps; the ceil walk reaches 1 in exactly `⌈log₂(n)⌉` steps.

---

## 2. Proof of Convergence

### Definitions

Define the two **Binary Equation Path functions** on the active domain of integers ≥ 2 (the values for which the recursive step produces a positive integer output). Both walks terminate upon reaching `n = 1`:

```
                  ⎧ n / 2          if n is even
f(n)  =  ⌊n/2⌋  = ⎨
                  ⎩ (n - 1) / 2    if n is odd

                  ⎧ n / 2          if n is even
g(n)  =  ⌈n/2⌉  = ⎨
                  ⎩ (n + 1) / 2    if n is odd
```

Formally, `f` and `g` map `{n ∈ Z⁺ : n ≥ 2} → Z⁺`, with `n = 1` as the terminal state.

For a positive integer `n₀ ≥ 2`, define the two sequences:

```
n₀, n₁ = f(n₀), n₂ = f(n₁), ..., n(k+1) = f(nk)        (floor walk)
m₀, m₁ = g(m₀), m₂ = g(m₁), ..., m(k+1) = g(mk)        (ceil walk)
```

**Claim (Floor):** For all positive integers `n₀ ≥ 2`, there exists a finite `K_f` such that `n_{K_f} = 1`, and the smallest such `K_f` equals `⌊log₂(n₀)⌋`.

**Claim (Ceil):** For all positive integers `m₀ ≥ 2`, there exists a finite `K_g` such that `m_{K_g} = 1`, and the smallest such `K_g` equals `⌈log₂(m₀)⌉`.

---

## Floor Walk Convergence

### Lemma F1 — Closure on Positive Integers

For all `n ≥ 2`, `f(n) ≥ 1`.

**Proof:**

* *Even case:* `f(n) = n/2`. Since `n ≥ 2`, `f(n) ≥ 1`. ✓
* *Odd case:* `f(n) = (n-1)/2`. Since `n ≥ 3` (smallest odd integer > 1), `f(n) = (n-1)/2 ≥ 1`. ✓

Therefore `f(n)` remains a positive integer for all `n ≥ 2`. ∎

---

### Lemma F2 — Strict Decrease

For all `n ≥ 2`, `f(n) < n`.

**Proof:**

* *Even case:* `n/2 < n` for all `n ≥ 1`. ✓
* *Odd case:* We need `(n-1)/2 < n`, i.e. `n - 1 < 2n`, i.e. `-1 < n`. This holds for all `n ≥ 1`. ✓

Therefore `f` strictly decreases `n` at every step. ∎

---

### Lemma F3 — No Fixed Point in the Active Domain

There is no `n ≥ 2` such that `f(n) = n`.

**Proof:** This follows directly from Lemma F2, which gives `f(n) < n` for all `n ≥ 2`, so equality is impossible. The sequence has no cycles in the active domain — it strictly decreases without getting stuck until it reaches the terminal value `n = 1`. ∎

---

### Theorem F — Convergence to 1

**For all positive integers `n₀ ≥ 2`, the sequence `{nk}` reaches `1` in finitely many steps.**

**Proof:**

By **Lemma F1**, each `nk ≥ 1` — the sequence stays in the positive integers.

By **Lemma F2**, `n(k+1) < nk` for all `nk ≥ 2` — the sequence is strictly decreasing while above 1.

By the **Well-Ordering Principle** (every non-empty subset of the positive integers has a minimum element), a strictly decreasing sequence of positive integers cannot be infinite — it must terminate.

The sequence terminates when `nk = 1`, and by **Lemma F3** no cycle can occur before reaching it.

Therefore, there exists a finite `K_f` such that `n_{K_f} = 1`. ∎

---

### Step Count Bound — Floor Walk

**The floor sequence reaches 1 in exactly `⌊log₂(n₀)⌋` steps.**

**Proof:**

In both cases, `f(n) = ⌊n/2⌋`:

* Even: `n/2 = ⌊n/2⌋` ✓
* Odd: `(n-1)/2 = ⌊n/2⌋` ✓

Therefore `f` is exactly the **integer right-shift** — each application removes the least significant bit of `n`.

By induction on `k`, `f^k(n₀) = ⌊n₀ / 2^k⌋`. Setting `f^{K_f}(n₀) = 1` gives `⌊n₀ / 2^{K_f}⌋ = 1`, which holds iff `2^{K_f} ≤ n₀ < 2^{K_f+1}`, i.e. `K_f = ⌊log₂(n₀)⌋`. ∎

> **Key insight:** The bit-flip behavior recorded at each step (the primary bit `a`) is the compression output. The underlying walk is simply stripping bits off the number one at a time — equivalent to reading the binary representation of `n` from most-significant to least-significant bit.

---

## Ceil Walk Convergence

### Lemma C1 — Closure on Positive Integers

For all `n ≥ 2`, `g(n) ≥ 1`.

**Proof:**

* *Even case:* `g(n) = n/2`. Since `n ≥ 2`, `g(n) ≥ 1`. ✓
* *Odd case:* `g(n) = (n+1)/2`. Since `n ≥ 3`, `g(n) = (n+1)/2 ≥ 2 ≥ 1`. ✓

Therefore `g(n)` remains a positive integer for all `n ≥ 2`. ∎

---

### Lemma C2 — Strict Decrease

For all `n ≥ 2`, `g(n) < n`.

**Proof:**

* *Even case:* `n/2 < n` for all `n ≥ 1`. ✓
* *Odd case:* We need `(n+1)/2 < n`, i.e. `n + 1 < 2n`, i.e. `1 < n`. This holds for all `n ≥ 2`, and the smallest odd integer satisfying `n ≥ 2` is `n = 3`, where `g(3) = 2 < 3`. ✓

Therefore `g` strictly decreases `n` at every step for `n ≥ 2`. ∎

---

### Lemma C3 — 1 is the Unique Fixed Point

The only `n ∈ Z⁺` such that `g(n) = n` is `n = 1`.

**Proof:**

* *Even case:* `n/2 = n` implies `n = 0`. Not a positive integer. ✗
* *Odd case:* `(n+1)/2 = n` implies `n + 1 = 2n`, i.e. `n = 1`. ✓

Therefore the only fixed point in `Z⁺` is `n = 1`, which serves as the termination state. For all `n ≥ 2` the sequence has no cycles — it strictly decreases until it reaches 1. ∎

---

### Theorem C — Convergence to 1

**For all positive integers `m₀ ≥ 2`, the sequence `{mk}` reaches `1` in finitely many steps.**

**Proof:**

By **Lemma C1**, each `mk ≥ 1` — the sequence stays in the positive integers.

By **Lemma C2**, `m(k+1) < mk` for all `mk ≥ 2` — the sequence is strictly decreasing while above 1.

By the **Well-Ordering Principle**, a strictly decreasing sequence of positive integers must terminate. By **Lemma C3** the only terminal value reachable from `m₀ ≥ 2` is `m_k = 1`, since 1 is the unique fixed point.

Therefore, there exists a finite `K_g` such that `m_{K_g} = 1`. ∎

---

### Step Count Bound — Ceil Walk

**The ceil sequence reaches 1 in exactly `⌈log₂(m₀)⌉` steps.**

**Proof:**

In both cases, `g(n) = ⌈n/2⌉`:

* Even: `n/2 = ⌈n/2⌉` ✓
* Odd: `(n+1)/2 = ⌈n/2⌉` ✓

We claim `g^k(m₀) = ⌈m₀ / 2^k⌉` for all `k ≥ 0`.

* *Base case (k = 0):* `g^0(m₀) = m₀ = ⌈m₀ / 2^0⌉`. ✓
* *Inductive step:* Assume `g^k(m₀) = ⌈m₀ / 2^k⌉`. Then  
`g^{k+1}(m₀) = g(g^k(m₀)) = g(⌈m₀/2^k⌉) = ⌈⌈m₀/2^k⌉ / 2⌉ = ⌈m₀ / 2^{k+1}⌉`,  
using the standard integer-arithmetic identity `⌈⌈x⌉/m⌉ = ⌈x/m⌉` for any positive integer `m`. ✓

Setting `g^{K_g}(m₀) = 1` gives `⌈m₀ / 2^{K_g}⌉ = 1`, which holds iff `0 < m₀ / 2^{K_g} ≤ 1`, i.e. `2^{K_g} ≥ m₀`, i.e. `K_g ≥ log₂(m₀)`. Since `K_g` is a non-negative integer, the smallest such `K_g` is `⌈log₂(m₀)⌉`. ∎

> **Key insight:** Where `f` strips the least significant bit, `g` rounds away from zero on every odd step. For powers of 2 (`m₀ = 2^k`) the two walks are identical and both take `k` steps. For all other `m₀`, the ceil walk takes exactly one more step than the floor walk — the extra step accounting for the rounding-up behavior on the first odd value encountered.

---

## Comparison of the Two Walks

| Property | Floor Walk `f(n) = ⌊n/2⌋` | Ceil Walk `g(n) = ⌈n/2⌉` |
|----------|---------------------------|---------------------------|
| Even rule | `n/2` | `n/2` |
| Odd rule | `(n-1)/2` | `(n+1)/2` |
| Convergence proven? | ✅ Theorem F | ✅ Theorem C |
| Strictly decreasing on `n ≥ 2`? | ✅ Lemma F2 | ✅ Lemma C2 |
| Fixed points | None in active domain | Only `n = 1` (the terminus) |
| Closed-form step count | `⌊log₂(n)⌋` | `⌈log₂(n)⌉` |
| Iterated form | `f^k(n) = ⌊n / 2^k⌋` | `g^k(n) = ⌈n / 2^k⌉` |
| Bit interpretation | Right-shift (drop LSB) | Round-up halving |

For powers of 2, `⌊log₂(n)⌋ = ⌈log₂(n)⌉`, so the two walks have identical step counts. For all other positive integers ≥ 2, the ceil walk is exactly one step longer than the floor walk.

---

## Contrast with the Original Collatz Conjecture

| Property | Original Collatz (`3n + 1`) | Floor Walk (`n - 1`) | Ceil Walk (`n + 1`) |
|----------|------------------------------|------------------------|------------------------|
| Convergence proven? | ❌ Unsolved | ✅ Proven | ✅ Proven |
| Monotone decreasing? | ❌ Odd step increases `n` | ✅ Always decreases | ✅ Always decreases |
| Fixed points / cycles? | Unknown in general | ✅ None | ✅ Only `n = 1` |
| Step bound | Unknown | ✅ Exactly `⌊log₂ n⌋` | ✅ Exactly `⌈log₂ n⌉` |
| Bit interpretation | Not direct | ✅ Right-shift | ✅ Round-up halving |

The original Collatz function's odd rule `3n + 1` can dramatically **increase** `n`, which is precisely why convergence is so difficult to prove. Replacing it with `n - 1` (floor walk) or `n + 1` (ceil walk) makes the odd step a decrease in both cases, and the entire convergence proof reduces to the Well-Ordering Principle — one of the most fundamental properties of the integers. The two adjustments give two distinct paths to 1 with mirror-image step bounds.

---

## 3. Compression

### Floor Walk — Worked Example

Given the following binary code:

```
10111010 00110000 01100010 10111001
```

Converted into traditional bytes (Little-Endian byte ordering shown here, with positional weights):

| Position weight (z) |       1 |     256 |   65,536 |   16,777,216 |
|---------------------|--------:|--------:|---------:|-------------:|
| Byte value (x)      |     185 |      98 |       48 |          186 |
| **Running total (y)** | **185** | **25,273** | **3,171,001** | **3,123,733,177** |

The resulting integer is `3,123,733,177`. Running this value through the floor walk records one bit per iteration: starting with the **primary bit** `a = "0"`, every odd value flips `a` (and subtracts 1), every value halves, and the post-flip value of `a` is the bit appended to the output.

| Step | Input value | Parity | a (after flip) | Halved → | Bit appended |
|-----:|------------:|:------:|:--------------:|---------:|:------------:|
| 1 | 3,123,733,177 | odd | 1 | 1,561,866,588 | 1 |
| 2 | 1,561,866,588 | even | 1 | 780,933,294 | 1 |
| 3 | 780,933,294 | even | 1 | 390,466,647 | 1 |
| 4 | 390,466,647 | odd | 0 | 195,233,323 | 0 |
| 5 | 195,233,323 | odd | 1 | 97,616,661 | 1 |
| 6 | 97,616,661 | odd | 0 | 48,808,330 | 0 |
| 7 | 48,808,330 | even | 0 | 24,404,165 | 0 |
| 8 | 24,404,165 | odd | 1 | 12,202,082 | 1 |
| 9 | 12,202,082 | even | 1 | 6,101,041 | 1 |
| 10 | 6,101,041 | odd | 0 | 3,050,520 | 0 |
| 11 | 3,050,520 | even | 0 | 1,525,260 | 0 |
| 12 | 1,525,260 | even | 0 | 762,630 | 0 |
| 13 | 762,630 | even | 0 | 381,315 | 0 |
| 14 | 381,315 | odd | 1 | 190,657 | 1 |
| 15 | 190,657 | odd | 0 | 95,328 | 0 |
| 16 | 95,328 | even | 0 | 47,664 | 0 |
| 17 | 47,664 | even | 0 | 23,832 | 0 |
| 18 | 23,832 | even | 0 | 11,916 | 0 |
| 19 | 11,916 | even | 0 | 5,958 | 0 |
| 20 | 5,958 | even | 0 | 2,979 | 0 |
| 21 | 2,979 | odd | 1 | 1,489 | 1 |
| 22 | 1,489 | odd | 0 | 744 | 0 |
| 23 | 744 | even | 0 | 372 | 0 |
| 24 | 372 | even | 0 | 186 | 0 |
| 25 | 186 | even | 0 | 93 | 0 |
| 26 | 93 | odd | 1 | 46 | 1 |
| 27 | 46 | even | 1 | 23 | 1 |
| 28 | 23 | odd | 0 | 11 | 0 |
| 29 | 11 | odd | 1 | 5 | 1 |
| 30 | 5 | odd | 0 | 2 | 0 |
| 31 | 2 | even | 0 | 1 | 0 |

The path produced by the walk:

```
input  (32 bits, BE):  10111010 00110000 01100010 10111001                    → decimal 3,123,733,177
result (31 bits):         0010110000100000010000110010111
```

The path length is `⌊log₂(3,123,733,177)⌋ = 31`, matching the 31 recorded steps. Standard binary requires `⌊log₂(n)⌋ + 1 = 32` bits to represent this value; the floor walk produces exactly `⌊log₂(n)⌋ = 31` bits — one fewer than standard binary, every time.

### Ceil Walk — Worked Example

Running the same value `3,123,733,177` through the ceil walk — odd values now **add** one before halving — produces a path of length `⌈log₂(3,123,733,177)⌉ = 32`:

```
input  (32 bits, BE):  10111010 00110000 01100010 10111001                    → decimal 3,123,733,177
result (32 bits):         00111100101110101000101100111101
```

For this value the ceil walk produces a 32-bit path — one bit longer than the floor walk's 31-bit result — reflecting the universal `⌈log₂(n)⌉` step bound. For powers of 2, the two walks coincide; for all other values ≥ 2, the ceil walk is exactly one step longer than the floor walk.

> **Note:** The values `0` and `1` are inaccessible by either walk — the walks terminate at 1, and 0 is outside the active domain. Every integer from **2 to 255** has a `⌊log₂(n)⌋ ≤ 7`-bit floor path and a `⌈log₂(n)⌉ ≤ 8`-bit ceil path. The two paths coincide in length at powers of 2 and differ by exactly one bit elsewhere. This applies to all integers ≥ 2 with no dictionary, storage methodology, or indexing required.

---

## Samples

Each sample below shows the original binary string (as written in Big-Endian) and the floor-walk path produced by running that value through the compressor. All paths have been round-trip verified.

### 32-Bit Samples (Floor Walk)

```
origin (32):  11001011111111011101001111000110
result (31):  0111001010101001011000101000010

origin (32):  01000000010001001110001100111100
result (30):  111111110000111010000100010100

origin (32):  10001011010000110011011010111101
result (31):  0000110110000010001001001101011
```

### 26-Bit Samples (Floor Walk)

```
origin (26):  00001000111111010011001110
result (21):  000010101001110111010

origin (26):  11101100101110000010110100
result (25):  0100100011010000001101100

origin (26):  00100100011001101000011101
result (23):  00011110111011000001011
```

### 20-Bit Samples (Floor Walk)

```
origin (20):  10111101110001010101
result (19):  1101011010000110011

origin (20):  11010101111011000110
result (19):  1001100101001000010

origin (20):  11111101000001001110
result (19):  0101011000000111010
```

### 14-Bit Samples (Floor Walk)

```
origin (14):  01011101111101
result (12):  110100101011

origin (14):  11110100101010
result (13):  1010011100110

origin (14):  11000100100100
result (13):  0111100011100
```

### 8-Bit Samples — Floor vs Ceil Walks

| value | binary   | floor path | ceil path  |
|------:|----------|------------|------------|
|     2 | 00000010 | 0          | 0          |
|     3 | 00000011 | 1          | 11         |
|     4 | 00000100 | 00         | 00         |
|    11 | 00001011 | 001        | 0011       |
|   128 | 10000000 | 0000000    | 0000000    |
|   129 | 10000001 | 1111111    | 11010101   |
|   254 | 11111110 | 0101010    | 11111110   |
|   255 | 11111111 | 1010101    | 11111111   |

For all powers of 2, floor and ceil paths coincide. For all other values, the ceil path is exactly one bit longer.

---

## Numeric Observations in Current Byte Structures

By incorporating the ASCII codes for standardized English (without accents), and performing standardized byte swaps to incorporate the 7-bit range (as standardized by ANSI in 1963), used bits can be reduced into a 7-bit range — fully recalculable, without storage of swaps or designation.

**ASCII Reference Table (32–127):**

| Dec | Binary  | Char | Dec | Binary  | Char | Dec | Binary  | Char |
|----:|---------|------|----:|---------|------|----:|---------|------|
|  32 | 0100000 | SP   |  65 | 1000001 | A    |  98 | 1100010 | b    |
|  33 | 0100001 | !    |  66 | 1000010 | B    |  99 | 1100011 | c    |
|  34 | 0100010 | "    |  67 | 1000011 | C    | 100 | 1100100 | d    |
|  35 | 0100011 | #    |  68 | 1000100 | D    | 101 | 1100101 | e    |
|  36 | 0100100 | $    |  69 | 1000101 | E    | 102 | 1100110 | f    |
|  37 | 0100101 | %    |  70 | 1000110 | F    | 103 | 1100111 | g    |
|  38 | 0100110 | &    |  71 | 1000111 | G    | 104 | 1101000 | h    |
|  39 | 0100111 | '    |  72 | 1001000 | H    | 105 | 1101001 | i    |
|  40 | 0101000 | (    |  73 | 1001001 | I    | 106 | 1101010 | j    |
|  41 | 0101001 | )    |  74 | 1001010 | J    | 107 | 1101011 | k    |
|  42 | 0101010 | *    |  75 | 1001011 | K    | 108 | 1101100 | l    |
|  43 | 0101011 | +    |  76 | 1001100 | L    | 109 | 1101101 | m    |
|  44 | 0101100 | ,    |  77 | 1001101 | M    | 110 | 1101110 | n    |
|  45 | 0101101 | -    |  78 | 1001110 | N    | 111 | 1101111 | o    |
|  46 | 0101110 | .    |  79 | 1001111 | O    | 112 | 1110000 | p    |
|  47 | 0101111 | /    |  80 | 1010000 | P    | 113 | 1110001 | q    |
|  48 | 0110000 | 0    |  81 | 1010001 | Q    | 114 | 1110010 | r    |
|  49 | 0110001 | 1    |  82 | 1010010 | R    | 115 | 1110011 | s    |
|  50 | 0110010 | 2    |  83 | 1010011 | S    | 116 | 1110100 | t    |
|  51 | 0110011 | 3    |  84 | 1010100 | T    | 117 | 1110101 | u    |
|  52 | 0110100 | 4    |  85 | 1010101 | U    | 118 | 1110110 | v    |
|  53 | 0110101 | 5    |  86 | 1010110 | V    | 119 | 1110111 | w    |
|  54 | 0110110 | 6    |  87 | 1010111 | W    | 120 | 1111000 | x    |
|  55 | 0110111 | 7    |  88 | 1011000 | X    | 121 | 1111001 | y    |
|  56 | 0111000 | 8    |  89 | 1011001 | Y    | 122 | 1111010 | z    |
|  57 | 0111001 | 9    |  90 | 1011010 | Z    | 123 | 1111011 | {    |
|  58 | 0111010 | :    |  91 | 1011011 | [    | 124 | 1111100 | \|    |
|  59 | 0111011 | ;    |  92 | 1011100 | \\   | 125 | 1111101 | }    |
|  60 | 0111100 | <    |  93 | 1011101 | ]    | 126 | 1111110 | ~    |
|  61 | 0111101 | =    |  94 | 1011110 | ^    | 127 | 1111111 | DEL  |
|  62 | 0111110 | >    |  95 | 1011111 | _    |     |         |      |
|  63 | 0111111 | ?    |  96 | 1100000 | `    |     |         |      |
|  64 | 1000000 | @    |  97 | 1100001 | a    |     |         |      |

These are operations similar to Huffman coding, DEFLATE, and other compression algorithms. However, with this methodology, compression can continue further by re-initiating either Binary Equation Path formula — the binary can be further compressed while the program retains the designated result value.

---

## Compression Functions

The baseline implementation exposes a unified entry point that accepts `byteWidth` (1–4) and a `ByteOrder` enum, dispatching to the appropriate internal variant automatically. See [Section 4 — Endianness](#4-endianness) for full examples of both conventions.

### Unified Entry Points (C#)

```csharp
// Compress a binary string (floor walk, default)
string result = BEP.Compress(binary, byteWidth, ByteOrder.BigEndian);
string result = BEP.Compress(binary, byteWidth, ByteOrder.LittleEndian);

// Decompress a BEP path string back to binary
string original = BEP.Decompress(path, byteWidth, ByteOrder.BigEndian);
string original = BEP.Decompress(path, byteWidth, ByteOrder.LittleEndian);

// Ceil-walk variants
string resultC   = BEP.CompressCeil(binary, byteWidth, ByteOrder.BigEndian);
string originalC = BEP.DecompressCeil(path, byteWidth, ByteOrder.BigEndian);
```

### Compressor (C#)

The top-level compressor parses the binary string into bytes, merges those bytes into a base-256 integer, and runs that integer through the value compressor. `BinToByteArrBE`, `ByteLongConvert`, and `RunCompression` are standard string-to-bytes / bytes-to-int / value-walking helpers; see the [inBEP repository](https://github.com/bepCreator/inBEP) for their definitions.

```csharp
static string Compressor4BE(string used)
{
    byte[] bin = BinToByteArrBE(used);          // Parse binary string MSB-first
    long val   = ByteLongConvert(bin, 256);     // Merge bytes into base-256 integer
    return RunCompression(val, 32);             // Walk to 1, record path
}
```

### Decompressor (C#)

The mirror operation: reverse the walk to recover the integer, split it back into bytes, and emit the BE binary string.

```csharp
static string Decompressor4BE(string bts)
{
    long origVal     = RunDecompression(bts);    // Reverse the walk to recover integer
    byte[] origBytes = IntByteConvert4(origVal); // Split back into bytes
    return ByteArrToBinBE(origBytes);            // Convert to Big Endian binary string
}
```

> **Note on binary-string length:** A binary string parsed to an integer loses its **leading** zeros (the high-order zeros that don't contribute to the integer's value). To reconstruct a binary string of the intended width, the decompressor uses `Convert.ToInt32(value, base)` / `Convert.ToString(value, base)` and re-pads on the left to the original `byteWidth × 8` bits.

### Value Compressor — Floor Walk (C#)

```csharp
static string ValCompressor(long val)
{
    string chars    = "0";  // Primary bit — flips on every odd step
    string opbinary = "";
    while (val != 1)
    {
        if (val % 2 == 1)
        {
            chars = (chars == "0") ? "1" : "0";
            val  -= 1;
        }
        val /= 2;
        opbinary = chars + opbinary;
    }
    return opbinary;
}
```

### Value Decompressor — Floor Walk (C#)

```csharp
static long ValDecompressor(string bts)
{
    long odd = Convert.ToInt32(char.GetNumericValue(bts[bts.Length - 1]));
    long val = 1;
    char lc  = bts[0];
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
```

### Value Compressor — Ceil Walk (C#)

The only difference from the floor compressor is the sign of the odd-step adjustment: `val += 1` instead of `val -= 1`.

```csharp
static string ValCompressorCeil(long val)
{
    string chars    = "0";  // Primary bit — flips on every odd step
    string opbinary = "";
    while (val != 1)
    {
        if (val % 2 == 1)
        {
            chars = (chars == "0") ? "1" : "0";
            val  += 1;        // ceil walk: add instead of subtract
        }
        val /= 2;
        opbinary = chars + opbinary;
    }
    return opbinary;
}
```

### Value Decompressor — Ceil Walk (C#)

The mirror operation: subtract instead of add when reconstructing.

```csharp
static long ValDecompressorCeil(string bts)
{
    long odd = Convert.ToInt32(char.GetNumericValue(bts[bts.Length - 1]));
    long val = 1;
    char lc  = bts[0];
    foreach (char c in bts)
    {
        if (c != lc) val -= 1;   // ceil walk: subtract instead of add
        val *= 2;
        lc = c;
    }
    long origVal = val;
    if (odd == 1) origVal -= 1;  // ceil walk: subtract instead of add
    return origVal;
}
```

Both pairs `(ValCompressor, ValDecompressor)` and `(ValCompressorCeil, ValDecompressorCeil)` round-trip losslessly for all `val ≥ 2`. The floor pair always produces paths of length `⌊log₂(val)⌋`; the ceil pair always produces paths of length `⌈log₂(val)⌉`.

---

## 4. Endianness

### What is Byte Order?

Byte order (endianness) describes the sequence in which bytes are arranged when representing a multi-byte value as a binary string.

**Big Endian (BE)** — most significant byte first. This is the standard readable/network order and the convention used throughout the paper's worked examples.

```
Value: 51,761   →   bytes [202, 49]   →   binary: 11001010 00110001
                     MSB first                      ^^^^^^^^ most significant byte
```

**Little Endian (LE)** — least significant byte first. This is the native memory order on x86/x64 processors (Intel, AMD) and most modern consumer hardware.

```
Value: 51,761   →   bytes [49, 202]   →   binary: 00110001 11001010
                     LSB first                      ^^^^^^^^ least significant byte
```

The integer value is the same in both cases — only the byte ordering of its binary string representation differs.

---

### How BEP Handles Endianness

The BEP compression walks themselves are **endian-agnostic** — they operate on a plain integer and always produce the same path regardless of byte order. Endianness only applies at the boundaries: converting an incoming binary string into an integer before the walk, and converting the integer back to a binary string after decompression. This is true for both the floor walk and the ceil walk.

```
[BE binary string]  ──►  integer  ──►  BEP walk  ──►  path string  ──►  integer  ──►  [BE binary string]
[LE binary string]  ──►  FlipByteOrder  ──►  integer  ──►  BEP walk  ──►  path string  ──►  integer  ──►  FlipByteOrder  ──►  [LE binary string]
```

---

### FlipByteOrder Helper

`FlipByteOrder` is the single conversion point between BE and LE. It reverses the byte-chunk order of a binary string while preserving the bit order within each 8-bit chunk.

```csharp
public static string FlipByteOrder(string binary)
{
    List<string> chunks = new List<string>();
    for (int i = 0; i < binary.Length; i += 8)
    {
        string chunk = binary.Substring(i, Math.Min(8, binary.Length - i));
        if (chunk.Length < 8) chunk = chunk.PadRight(8, '0');
        chunks.Add(chunk);
    }
    chunks.Reverse();
    return string.Join("", chunks);
}
```

Example:

```
BE: 11001010 00110001   →   FlipByteOrder   →   LE: 00110001 11001010
LE: 00110001 11001010   →   FlipByteOrder   →   BE: 11001010 00110001
```

---

### Unified API

The `Compress` and `Decompress` methods accept a `ByteOrder` enum and dispatch automatically:

```csharp
public enum ByteOrder { BigEndian, LittleEndian }

string result   = BEP.Compress(binary,    byteWidth, ByteOrder.BigEndian);
string result   = BEP.Compress(binary,    byteWidth, ByteOrder.LittleEndian);
string original = BEP.Decompress(path,    byteWidth, ByteOrder.BigEndian);
string original = BEP.Decompress(path,    byteWidth, ByteOrder.LittleEndian);
```

Supported `byteWidth` values: `1` (8-bit), `2` (16-bit), `3` (24-bit), `4` (32-bit). The same dispatch is available for the ceil-walk variants via `BEP.CompressCeil` / `BEP.DecompressCeil`.

---

### Big Endian Examples

All examples from Section 3 of this paper use Big Endian convention (MSB first).

**1-Byte (8-bit) — Floor Walk**

```
origin  (8 bits) [BE]:  11001010           → decimal 202
result  (7 bits) [BE]:  1000110            ← length = ⌊log₂(202)⌋ = 7
restored         [BE]:  11001010  ✓
```

**1-Byte (8-bit) — Ceil Walk**

```
origin  (8 bits) [BE]:  11001010           → decimal 202
result  (8 bits) [BE]:  00010010           ← length = ⌈log₂(202)⌉ = 8
restored         [BE]:  11001010  ✓
```

**2-Byte (16-bit) — Floor Walk**

```
origin (16 bits) [BE]:  11001010 00110001  → decimal 51,761
result (15 bits) [BE]:  011100111101111    ← length = ⌊log₂(51,761)⌋ = 15
restored         [BE]:  11001010 00110001  ✓
```

**4-Byte (32-bit) — Floor Walk**

```
origin (32 bits) [BE]:  10111010 00110000 01100010 10111001  → decimal 3,123,733,177
result (31 bits) [BE]:  0010110000100000010000110010111    ← length = ⌊log₂(n)⌋ = 31
restored         [BE]:  10111010 00110000 01100010 10111001  ✓
```

---

### Little Endian Examples

The same values expressed in Little Endian (LSB byte first). The integer being compressed is identical — only the binary string representation is reversed at the byte level.

**1-Byte (8-bit) — Floor Walk**

> Single-byte values are byte-order invariant — there is only one byte to order.

```
origin  (8 bits) [LE]:  11001010           → decimal 202
result  (7 bits) [LE]:  1000110
restored         [LE]:  11001010  ✓
```

**2-Byte (16-bit) — Floor Walk**

```
origin (16 bits) [LE]:  00110001 11001010  → decimal 51,761  (bytes reversed vs BE)
result (15 bits) [LE]:  011100111101111
restored         [LE]:  00110001 11001010  ✓
```

> **Note:** The compressed path string is the same for both BE and LE inputs of the same value — because the path is derived from the integer, not the string representation. Only the restored output binary string differs in byte order. This holds for both floor and ceil walks.

---

### Value Compression — Endian-Agnostic

When working directly with integer values rather than binary strings, byte order is irrelevant. The four value-level methods operate purely on the number:

```csharp
long original = 3123733177;

string fpath  = BEP.ValCompressor(original);        // floor path, length 31
long  frest   = BEP.ValDecompressor(fpath);         // 3123733177
bool  flossless = (original == frest);              // true

string cpath  = BEP.ValCompressorCeil(original);    // ceil  path, length 32
long  crest   = BEP.ValDecompressorCeil(cpath);     // 3123733177
bool  clossless = (original == crest);              // true
```

Use the value-level methods when your pipeline already works with integers and byte order conversion is handled externally.

---

## License

Copyright 2026 Rich Wagner — [newdawndata.com](https://newdawndata.com)

Licensed under the Apache License, Version 2.0. You may obtain a copy of the License at:

http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software distributed under this license is distributed on an **"AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND**, either express or implied. See the License for the specific language governing permissions and limitations under the License.
