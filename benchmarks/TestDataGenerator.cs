// =============================================================================
// TestDataGenerator — Generates synthetic datasets for benchmarking
// Author:  Rich Wagner | License: Apache 2.0 | newdawndata.com
// =============================================================================

namespace BEPBenchmark.Benchmark
{
    public static class TestDataGenerator
    {
        private static readonly Random Rng = new Random(42);   // Seeded for reproducibility

        // -------------------------------------------------------------------------
        // PROFILES
        // -------------------------------------------------------------------------

        /// <summary>Purely random bytes — worst case for most compression algorithms.</summary>
        public static byte[] Random(int sizeBytes)
        {
            var data = new byte[sizeBytes];
            Rng.NextBytes(data);
            return data;
        }

        /// <summary>
        /// Highly repetitive data — best case for RLE-based algorithms.
        /// Alternates between a small set of byte values.
        /// </summary>
        public static byte[] Repetitive(int sizeBytes)
        {
            var data    = new byte[sizeBytes];
            byte[] pattern = { 0x41, 0x41, 0x41, 0x41, 0x42, 0x41, 0x41, 0x41 };
            for (int i = 0; i < sizeBytes; i++)
                data[i] = pattern[i % pattern.Length];
            return data;
        }

        /// <summary>
        /// Sequential 32-bit integers (0, 1, 2, 3 ...) packed as Big Endian bytes.
        /// Tests BEP on small-value integers — where savings are largest.
        /// </summary>
        public static byte[] Sequential(int sizeBytes)
        {
            var data = new byte[sizeBytes];
            for (int i = 0; i < sizeBytes / 4; i++)
            {
                uint val = (uint)i;
                data[i*4]   = (byte)((val >> 24) & 0xFF);
                data[i*4+1] = (byte)((val >> 16) & 0xFF);
                data[i*4+2] = (byte)((val >>  8) & 0xFF);
                data[i*4+3] = (byte)((val)       & 0xFF);
            }
            return data;
        }

        /// <summary>
        /// ASCII text-like data — printable characters (ASCII 32–126).
        /// Simulates plain text files without real semantic content.
        /// </summary>
        public static byte[] TextLike(int sizeBytes)
        {
            var data = new byte[sizeBytes];
            for (int i = 0; i < sizeBytes; i++)
                data[i] = (byte)Rng.Next(32, 127);
            return data;
        }

        /// <summary>
        /// Low-entropy data — mostly zeros with sparse non-zero values.
        /// Simulates sparse matrices, padding-heavy formats, or empty regions.
        /// </summary>
        public static byte[] SparseNonZero(int sizeBytes)
        {
            var data = new byte[sizeBytes];
            // Set ~5% of bytes to a random non-zero value
            int nonZeroCount = sizeBytes / 20;
            for (int i = 0; i < nonZeroCount; i++)
            {
                int pos = Rng.Next(0, sizeBytes);
                data[pos] = (byte)Rng.Next(2, 256);
            }
            return data;
        }

        /// <summary>
        /// High-entropy data simulating encrypted or already-compressed content.
        /// Uses a linear congruential generator for pseudo-cryptographic noise.
        /// </summary>
        public static byte[] HighEntropy(int sizeBytes)
        {
            var data = new byte[sizeBytes];
            ulong state = 0x123456789ABCDEF0UL;
            for (int i = 0; i < sizeBytes; i++)
            {
                state = state * 6364136223846793005UL + 1442695040888963407UL;
                data[i] = (byte)(state >> 56);
            }
            return data;
        }

        /// <summary>
        /// Mixed data — four equal segments of different profiles joined together.
        /// Closest approximation to real-world heterogeneous file content.
        /// </summary>
        public static byte[] Mixed(int sizeBytes)
        {
            int seg  = sizeBytes / 4;
            var data = new byte[sizeBytes];
            Buffer.BlockCopy(TextLike(seg),      0, data, 0,       seg);
            Buffer.BlockCopy(Repetitive(seg),    0, data, seg,     seg);
            Buffer.BlockCopy(Sequential(seg),    0, data, seg*2,   seg);
            Buffer.BlockCopy(SparseNonZero(seg), 0, data, seg*3,   seg);
            return data;
        }

        /// <summary>
        /// Simulates 32-bit floating point sensor data (signed, small range).
        /// Values cluster around 0 with gaussian-like distribution.
        /// </summary>
        public static byte[] SensorData(int sizeBytes)
        {
            var data = new byte[sizeBytes];
            for (int i = 0; i < sizeBytes / 4; i++)
            {
                // Box-Muller transform for gaussian distribution
                double u1 = 1.0 - Rng.NextDouble();
                double u2 = 1.0 - Rng.NextDouble();
                double z  = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
                float  f  = (float)(z * 100.0);
                byte[] fb = BitConverter.GetBytes(f);
                data[i*4]   = fb[3]; // Store Big Endian
                data[i*4+1] = fb[2];
                data[i*4+2] = fb[1];
                data[i*4+3] = fb[0];
            }
            return data;
        }

        // -------------------------------------------------------------------------
        // CATALOG
        // -------------------------------------------------------------------------

        /// <summary>Returns all test profiles as named pairs for iteration.</summary>
        public static IEnumerable<(string Name, byte[] Data)> AllProfiles(int sizeBytes)
        {
            yield return ("Random Binary",     Random(sizeBytes));
            yield return ("High Entropy",      HighEntropy(sizeBytes));
            yield return ("Text-Like (ASCII)", TextLike(sizeBytes));
            yield return ("Sequential Ints",   Sequential(sizeBytes));
            yield return ("Repetitive",        Repetitive(sizeBytes));
            yield return ("Sparse / Low Ent.", SparseNonZero(sizeBytes));
            yield return ("Sensor Data",       SensorData(sizeBytes));
            yield return ("Mixed",             Mixed(sizeBytes));
        }
    }
}
