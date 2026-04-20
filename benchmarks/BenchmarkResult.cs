// =============================================================================
// BenchmarkResult — Result model for a single algorithm/dataset combination
// Author:  Rich Wagner | License: Apache 2.0 | newdawndata.com
// =============================================================================

namespace BEPBenchmark.Benchmark
{
    public class BenchmarkResult
    {
        public string   DatasetName      { get; init; } = "";
        public string   AlgorithmName    { get; init; } = "";
        public long     OriginalBytes    { get; init; }
        public long     CompressedBytes  { get; init; }
        public double   RatioPercent     => OriginalBytes > 0
                                            ? (double)CompressedBytes / OriginalBytes * 100.0
                                            : 100.0;
        public double   SavedPercent     => 100.0 - RatioPercent;
        public TimeSpan ElapsedCompress  { get; init; }
        public TimeSpan ElapsedDecompress{ get; init; }
        public bool     Lossless         { get; init; }
        public string?  Notes            { get; init; }

        /// <summary>Formats compressed size with unit (B / KB / MB).</summary>
        public string CompressedSizeFormatted => FormatBytes(CompressedBytes);

        /// <summary>Formats original size with unit.</summary>
        public string OriginalSizeFormatted   => FormatBytes(OriginalBytes);

        private static string FormatBytes(long bytes)
        {
            if (bytes >= 1_048_576) return $"{bytes / 1_048_576.0:F2} MB";
            if (bytes >= 1_024)     return $"{bytes / 1_024.0:F2} KB";
            return $"{bytes} B";
        }
    }
}
