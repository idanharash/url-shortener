using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        string inputUrl = args.Length > 0 ? args[0] : "http://127.0.0.1:5000/EODrQ";
        int totalRequests = args.Length > 1 && int.TryParse(args[1], out int r) ? r : 100;
        int maxConcurrency = 10;

        Console.WriteLine($"🚀 Sending {totalRequests} GET requests to {inputUrl} (TTFB only)...");

        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false
        };

        using var client = new HttpClient(handler);
        var semaphore = new SemaphoreSlim(maxConcurrency);

        var tasks = Enumerable.Range(1, totalRequests).Select(async i =>
        {
            await semaphore.WaitAsync();
            try
            {
                await Task.Delay(i * 3);
                var sw = Stopwatch.StartNew();
                var response = await client.GetAsync(inputUrl);
                sw.Stop();
                return (i, sw.ElapsedMilliseconds, (int)response.StatusCode);
            }
            finally
            {
                semaphore.Release();
            }
        }).ToArray();

        var results = await Task.WhenAll(tasks);

        foreach (var result in results.OrderBy(r => r.Item1))
            Console.WriteLine($"[{result.Item1}] {result.Item2} ms - {result.Item3}");

        var times = results.Select(r => r.Item2).ToArray();
        Console.WriteLine("\n---- Summary ----");
        Console.WriteLine($"Total: {totalRequests}");
        Console.WriteLine($"Average: {times.Average():F1} ms");
        Console.WriteLine($"Min: {times.Min()} ms");
        Console.WriteLine($"Max: {times.Max()} ms");
        Console.WriteLine($"P95: {Percentile(times, 95)} ms");

        PrintDistribution(times, "< 50ms", t => t < 50, totalRequests);
        PrintDistribution(times, "50–100ms", t => t >= 50 && t < 100, totalRequests);
        PrintDistribution(times, "100–200ms", t => t >= 100 && t < 200, totalRequests);
        PrintDistribution(times, "200–500ms", t => t >= 200 && t < 500, totalRequests);
        PrintDistribution(times, "500ms+", t => t >= 500, totalRequests);
    }

    static void PrintDistribution(long[] times, string label, Func<long, bool> predicate, int total)
    {
        int count = times.Count(predicate);
        double percent = (double)count / total * 100;
        Console.WriteLine($"{label}: {count} requests ({percent:F1}%)");
    }

    static long Percentile(long[] values, double percentile)
    {
        var sorted = values.OrderBy(x => x).ToArray();
        int index = (int)Math.Ceiling(percentile / 100.0 * sorted.Length) - 1;
        return sorted[Math.Clamp(index, 0, sorted.Length - 1)];
    }
}
