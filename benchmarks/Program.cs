// =============================================================================
// Binary Equation Paths — Compression Benchmark Tool
// Author:  Rich Wagner | License: Apache 2.0 | newdawndata.com
//
// Usage:
//   dotnet run                        → Interactive menu
//   dotnet run -- bench               → Full synthetic benchmark (64KB datasets)
//   dotnet run -- bench 1048576       → Full synthetic benchmark (1MB datasets)
//   dotnet run -- file path/to/file   → Benchmark a specific file
//   dotnet run -- roundtrip           → Round-trip verification on all profiles
// =============================================================================

using BEPBenchmark.Benchmark;
using BEPBenchmark.Core;

const int DEFAULT_DATA_SIZE = 65536;    // 64 KB default dataset size
const int DEFAULT_BYTE_WIDTH = 4;       // 4-byte chunks by default

PrintBanner();

// --- Command line dispatch ---
if (args.Length > 0)
{
    switch (args[0].ToLower())
    {
        case "bench":
            int size = args.Length > 1 && int.TryParse(args[1], out int s) ? s : DEFAULT_DATA_SIZE;
            RunFullBenchmark(size, DEFAULT_BYTE_WIDTH);
            break;
        case "file":
            if (args.Length < 2) { Console.WriteLine("Usage: dotnet run -- file <path>"); break; }
            RunFileBenchmark(args[1], DEFAULT_BYTE_WIDTH);
            break;
        case "roundtrip":
            RunRoundTripVerification();
            break;
        default:
            Console.WriteLine($"Unknown command: {args[0]}");
            ShowHelp();
            break;
    }
    return;
}

// --- Interactive menu ---
bool running = true;
while (running)
{
    Console.WriteLine("\n┌─────────────────────────────────────────┐");
    Console.WriteLine("│  BEP Benchmark Tool                     │");
    Console.WriteLine("├─────────────────────────────────────────┤");
    Console.WriteLine("│  1. Full synthetic benchmark (64 KB)    │");
    Console.WriteLine("│  2. Full synthetic benchmark (1 MB)     │");
    Console.WriteLine("│  3. Benchmark a file                    │");
    Console.WriteLine("│  4. Round-trip verification             │");
    Console.WriteLine("│  5. Single value compression demo       │");
    Console.WriteLine("│  6. Exit                                │");
    Console.WriteLine("└─────────────────────────────────────────┘");
    Console.Write("\nChoice: ");

    string? choice = Console.ReadLine()?.Trim();
    Console.WriteLine();

    switch (choice)
    {
        case "1": RunFullBenchmark(DEFAULT_DATA_SIZE, DEFAULT_BYTE_WIDTH); break;
        case "2": RunFullBenchmark(1_048_576, DEFAULT_BYTE_WIDTH);         break;
        case "3":
            Console.Write("File path: ");
            string? path = Console.ReadLine()?.Trim();
            if (!string.IsNullOrEmpty(path)) RunFileBenchmark(path, DEFAULT_BYTE_WIDTH);
            break;
        case "4": RunRoundTripVerification(); break;
        case "5": RunSingleValueDemo();       break;
        case "6": running = false;            break;
        default:  Console.WriteLine("Invalid choice."); break;
    }
}


// =============================================================================
// RUNNERS
// =============================================================================

void RunFullBenchmark(int dataSize, int byteWidth)
{
    Console.WriteLine($"Running full benchmark — {dataSize:N0} bytes per dataset, {byteWidth}-byte chunks\n");
    Console.WriteLine("Generating and compressing...");

    var results = Benchmarker.RunSuite(dataSize, byteWidth);

    Console.WriteLine();
    PrintResultsTable(results);
    PrintSummary(results, dataSize);
}

void RunFileBenchmark(string filePath, int byteWidth)
{
    try
    {
        Console.WriteLine($"\nBenchmarking file: {filePath}");
        var results = Benchmarker.RunOnFile(filePath, byteWidth);
        PrintResultsTable(results);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
    }
}

void RunRoundTripVerification()
{
    Console.WriteLine("Round-trip verification — compressing and decompressing all profiles...\n");
    int passed = 0, failed = 0;

    foreach (var (name, data) in TestDataGenerator.AllProfiles(DEFAULT_DATA_SIZE))
    {
        var result = Benchmarker.RunBEPFile(name, data, DEFAULT_BYTE_WIDTH);
        string status = result.Lossless ? "✓ PASS" : "✗ FAIL";
        Console.WriteLine($"  [{status}] {name,-22} {result.CompressedSizeFormatted,10} / {result.OriginalSizeFormatted,10}  {result.Notes}");
        if (result.Lossless) passed++; else failed++;
    }

    Console.WriteLine($"\n  Results: {passed} passed, {failed} failed.");
}

void RunSingleValueDemo()
{
    Console.WriteLine("Single value compression demo\n");
    long[] testValues = { 2, 7, 13, 42, 202, 255, 1000, 51762, 3123733177 };

    Console.WriteLine($"  {"Value",15}  {"Path",35}  {"Orig bits",10}  {"Path bits",10}  {"Saved",8}");
    Console.WriteLine($"  {new string('-', 85)}");

    foreach (long val in testValues)
    {
        string path     = BEP.ValCompressor(val);
        long   origBits = (long)Math.Ceiling(Math.Log2(val + 1));
        int    pathBits = path.Length;
        int    saved    = (int)origBits - pathBits;

        Console.WriteLine($"  {val,15}  {path,-35}  {origBits,10}  {pathBits,10}  {saved,7} bits");

        // Verify round-trip
        long restored = BEP.ValDecompressor(path);
        if (restored != val)
            Console.WriteLine($"  ⚠ Round-trip mismatch! Expected {val}, got {restored}");
    }
    Console.WriteLine();
}


// =============================================================================
// REPORTING
// =============================================================================

void PrintResultsTable(List<BenchmarkResult> results)
{
    // Group by dataset
    var datasets = results.Select(r => r.DatasetName).Distinct().ToList();

    foreach (string dataset in datasets)
    {
        var group = results.Where(r => r.DatasetName == dataset).ToList();
        if (!group.Any()) continue;

        long origBytes = group[0].OriginalBytes;
        Console.WriteLine($"\n  ── {dataset} ({origBytes:N0} bytes) ────────────────────────────────────");
        Console.WriteLine($"  {"Algorithm",-26}  {"Compressed",12}  {"Ratio %",8}  {"Saved %",8}  {"Compress",10}  {"Notes"}");
        Console.WriteLine($"  {new string('─', 90)}");

        foreach (var r in group)
        {
            string losslessTag = r.Lossless ? "" : " ⚠";
            Console.WriteLine(
                $"  {r.AlgorithmName + losslessTag,-26}  " +
                $"{r.CompressedSizeFormatted,12}  " +
                $"{r.RatioPercent,7:F1}%  " +
                $"{r.SavedPercent,7:F1}%  " +
                $"{r.ElapsedCompress.TotalMilliseconds,8:F1} ms  " +
                $"{r.Notes}"
            );
        }
    }
    Console.WriteLine();
}

void PrintSummary(List<BenchmarkResult> results, int dataSize)
{
    Console.WriteLine("\n  ── Summary — Average Saved % across all datasets ──────────────────────────");

    var algorithms = results.Select(r => r.AlgorithmName).Distinct().ToList();
    foreach (string alg in algorithms)
    {
        var group   = results.Where(r => r.AlgorithmName == alg).ToList();
        double avg  = group.Average(r => r.SavedPercent);
        double best = group.Max(r => r.SavedPercent);
        double worst= group.Min(r => r.SavedPercent);
        Console.WriteLine($"  {alg,-26}  avg {avg,6:F1}%   best {best,6:F1}%   worst {worst,6:F1}%");
    }
    Console.WriteLine();
}

void PrintBanner()
{
    Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║  Binary Equation Paths — Compression Benchmark Tool         ║");
    Console.WriteLine("║  Rich Wagner | Apache 2.0 | newdawndata.com                 ║");
    Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
    Console.WriteLine();
}

void ShowHelp()
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  dotnet run                       Interactive menu");
    Console.WriteLine("  dotnet run -- bench              Synthetic benchmark (64 KB)");
    Console.WriteLine("  dotnet run -- bench 1048576      Synthetic benchmark (1 MB)");
    Console.WriteLine("  dotnet run -- file <path>        Benchmark a file");
    Console.WriteLine("  dotnet run -- roundtrip          Round-trip verification");
}
