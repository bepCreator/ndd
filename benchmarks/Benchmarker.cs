// =============================================================================
// Benchmarker — Runs BEP and standard algorithms, collects results
// Compares: BEP (theoretical), BEP (practical), GZip, Deflate, Brotli
// Author:  Rich Wagner | License: Apache 2.0 | newdawndata.com
// =============================================================================

using System.Diagnostics;
using System.IO.Compression;
using BEPBenchmark.Core;

namespace BEPBenchmark.Benchmark
{
    public static class Benchmarker
    {
        // -------------------------------------------------------------------------
        // RUN A FULL SUITE
        // -------------------------------------------------------------------------

        /// <summary>
        /// Runs all algorithms against all test profiles at the given size.
        /// Returns a flat list of results for reporting.
        /// </summary>
        public static List<BenchmarkResult> RunSuite(int dataSize = 65536, int byteWidth = 4)
        {
            var results = new List<BenchmarkResult>();

            foreach (var (name, data) in TestDataGenerator.AllProfiles(dataSize))
            {
                Console.Write($"  Testing [{name,-22}] ");

                results.AddRange(RunAllAlgorithms(name, data, byteWidth));

                Console.WriteLine("done.");
            }

            return results;
        }

        // -------------------------------------------------------------------------
        // RUN ALL ALGORITHMS ON ONE DATASET
        // -------------------------------------------------------------------------

        public static List<BenchmarkResult> RunAllAlgorithms(string datasetName, byte[] data, int byteWidth = 4)
        {
            var results = new List<BenchmarkResult>();

            results.Add(RunBEPTheoretical(datasetName, data, byteWidth));
            results.Add(RunBEPPractical(datasetName, data, byteWidth));
            results.Add(RunBEPFile(datasetName, data, byteWidth));
            results.Add(RunGZip(datasetName, data));
            results.Add(RunDeflate(datasetName, data));
            results.Add(RunBrotli(datasetName, data));

            return results;
        }

        // -------------------------------------------------------------------------
        // BEP — THEORETICAL (sum of path bits, no framing overhead)
        // -------------------------------------------------------------------------

        public static BenchmarkResult RunBEPTheoretical(string datasetName, byte[] data, int byteWidth = 4)
        {
            var sw = Stopwatch.StartNew();
            var (theoretical, _, literals, total) = BEPFileCompressor.AnalyzeCompression(data, byteWidth);
            sw.Stop();

            long theoreticalBytes = (long)Math.Ceiling(theoretical / 8.0);

            return new BenchmarkResult
            {
                DatasetName     = datasetName,
                AlgorithmName   = $"BEP Theoretical ({byteWidth}B)",
                OriginalBytes   = data.Length,
                CompressedBytes = theoreticalBytes,
                ElapsedCompress = sw.Elapsed,
                Lossless        = true,
                Notes           = $"{literals}/{total} literals"
            };
        }

        // -------------------------------------------------------------------------
        // BEP — PRACTICAL (path bits + length prefix framing, no file header)
        // -------------------------------------------------------------------------

        public static BenchmarkResult RunBEPPractical(string datasetName, byte[] data, int byteWidth = 4)
        {
            var sw = Stopwatch.StartNew();
            var (_, practical, literals, total) = BEPFileCompressor.AnalyzeCompression(data, byteWidth);
            sw.Stop();

            long practicalBytes = (long)Math.Ceiling(practical / 8.0);

            return new BenchmarkResult
            {
                DatasetName     = datasetName,
                AlgorithmName   = $"BEP Practical  ({byteWidth}B)",
                OriginalBytes   = data.Length,
                CompressedBytes = practicalBytes,
                ElapsedCompress = sw.Elapsed,
                Lossless        = true,
                Notes           = $"{literals}/{total} literals, +framing"
            };
        }

        // -------------------------------------------------------------------------
        // BEP — FILE (full round-trip: compress + decompress + verify)
        // -------------------------------------------------------------------------

        public static BenchmarkResult RunBEPFile(string datasetName, byte[] data, int byteWidth = 4)
        {
            byte[]? restored  = null;
            bool    lossless  = false;
            string? notes     = null;

            var swCompress = Stopwatch.StartNew();
            byte[] compressed;
            try
            {
                compressed = BEPFileCompressor.Compress(data, byteWidth);
            }
            catch (Exception ex)
            {
                return new BenchmarkResult
                {
                    DatasetName     = datasetName,
                    AlgorithmName   = $"BEP File       ({byteWidth}B)",
                    OriginalBytes   = data.Length,
                    CompressedBytes = data.Length,
                    Lossless        = false,
                    Notes           = $"Compress error: {ex.Message}"
                };
            }
            swCompress.Stop();

            var swDecompress = Stopwatch.StartNew();
            try
            {
                restored = BEPFileCompressor.Decompress(compressed);
                lossless = restored.SequenceEqual(data);
                if (!lossless) notes = "⚠ ROUND-TRIP MISMATCH";
            }
            catch (Exception ex)
            {
                notes    = $"Decompress error: {ex.Message}";
                lossless = false;
            }
            swDecompress.Stop();

            return new BenchmarkResult
            {
                DatasetName      = datasetName,
                AlgorithmName    = $"BEP File       ({byteWidth}B)",
                OriginalBytes    = data.Length,
                CompressedBytes  = compressed.Length,
                ElapsedCompress  = swCompress.Elapsed,
                ElapsedDecompress= swDecompress.Elapsed,
                Lossless         = lossless,
                Notes            = notes ?? (lossless ? "✓ lossless verified" : "")
            };
        }

        // -------------------------------------------------------------------------
        // GZIP
        // -------------------------------------------------------------------------

        public static BenchmarkResult RunGZip(string datasetName, byte[] data)
        {
            var swCompress = Stopwatch.StartNew();
            byte[] compressed;
            using (var ms = new MemoryStream())
            {
                using (var gz = new GZipStream(ms, CompressionLevel.Optimal))
                    gz.Write(data, 0, data.Length);
                compressed = ms.ToArray();
            }
            swCompress.Stop();

            var swDecompress = Stopwatch.StartNew();
            byte[] restored;
            using (var ms = new MemoryStream(compressed))
            using (var gz = new GZipStream(ms, CompressionMode.Decompress))
            using (var outMs = new MemoryStream())
            {
                gz.CopyTo(outMs);
                restored = outMs.ToArray();
            }
            swDecompress.Stop();

            return new BenchmarkResult
            {
                DatasetName      = datasetName,
                AlgorithmName    = "GZip (Optimal)",
                OriginalBytes    = data.Length,
                CompressedBytes  = compressed.Length,
                ElapsedCompress  = swCompress.Elapsed,
                ElapsedDecompress= swDecompress.Elapsed,
                Lossless         = restored.SequenceEqual(data),
                Notes            = "System.IO.Compression"
            };
        }

        // -------------------------------------------------------------------------
        // DEFLATE
        // -------------------------------------------------------------------------

        public static BenchmarkResult RunDeflate(string datasetName, byte[] data)
        {
            var swCompress = Stopwatch.StartNew();
            byte[] compressed;
            using (var ms = new MemoryStream())
            {
                using (var df = new DeflateStream(ms, CompressionLevel.Optimal))
                    df.Write(data, 0, data.Length);
                compressed = ms.ToArray();
            }
            swCompress.Stop();

            var swDecompress = Stopwatch.StartNew();
            byte[] restored;
            using (var ms = new MemoryStream(compressed))
            using (var df = new DeflateStream(ms, CompressionMode.Decompress))
            using (var outMs = new MemoryStream())
            {
                df.CopyTo(outMs);
                restored = outMs.ToArray();
            }
            swDecompress.Stop();

            return new BenchmarkResult
            {
                DatasetName      = datasetName,
                AlgorithmName    = "Deflate (Opt.)",
                OriginalBytes    = data.Length,
                CompressedBytes  = compressed.Length,
                ElapsedCompress  = swCompress.Elapsed,
                ElapsedDecompress= swDecompress.Elapsed,
                Lossless         = restored.SequenceEqual(data),
                Notes            = "System.IO.Compression"
            };
        }

        // -------------------------------------------------------------------------
        // BROTLI (.NET 6+)
        // -------------------------------------------------------------------------

        public static BenchmarkResult RunBrotli(string datasetName, byte[] data)
        {
            var swCompress = Stopwatch.StartNew();
            byte[] compressed;
            using (var ms = new MemoryStream())
            {
                using (var br = new BrotliStream(ms, CompressionLevel.Optimal))
                    br.Write(data, 0, data.Length);
                compressed = ms.ToArray();
            }
            swCompress.Stop();

            var swDecompress = Stopwatch.StartNew();
            byte[] restored;
            using (var ms = new MemoryStream(compressed))
            using (var br = new BrotliStream(ms, CompressionMode.Decompress))
            using (var outMs = new MemoryStream())
            {
                br.CopyTo(outMs);
                restored = outMs.ToArray();
            }
            swDecompress.Stop();

            return new BenchmarkResult
            {
                DatasetName      = datasetName,
                AlgorithmName    = "Brotli (Opt.)",
                OriginalBytes    = data.Length,
                CompressedBytes  = compressed.Length,
                ElapsedCompress  = swCompress.Elapsed,
                ElapsedDecompress= swDecompress.Elapsed,
                Lossless         = restored.SequenceEqual(data),
                Notes            = "System.IO.Compression"
            };
        }

        // -------------------------------------------------------------------------
        // RUN ON A REAL FILE
        // -------------------------------------------------------------------------

        /// <summary>
        /// Runs the full benchmark suite against a file on disk.
        /// </summary>
        public static List<BenchmarkResult> RunOnFile(string filePath, int byteWidth = 4)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}");

            byte[] data = File.ReadAllBytes(filePath);
            string name = Path.GetFileName(filePath);
            Console.WriteLine($"  File: {name} ({data.Length:N0} bytes)\n");

            return RunAllAlgorithms(name, data, byteWidth);
        }
    }
}
