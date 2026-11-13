using System.Diagnostics;
using System.Globalization;

// Measures redirect latency (time to the 302 response, redirects not followed).
// Usage: dotnet run -c Release -- <short-url> [total-requests=1000] [concurrency=20]
// Results depend heavily on the machine and on whether the app runs in Docker; report them with that context.

var url = args.Length > 0 ? args[0] : throw new ArgumentException("Pass the short URL to test, e.g. http://localhost:5000/abc12");
var totalRequests = args.Length > 1 ? int.Parse(args[1], CultureInfo.InvariantCulture) : 1000;
var concurrency = args.Length > 2 ? int.Parse(args[2], CultureInfo.InvariantCulture) : 20;

using var client = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false, MaxConnectionsPerServer = concurrency });

// Warm-up: JIT, connection pool and the cache entry for this code.
for (var i = 0; i < 20; i++)
    (await client.GetAsync(url)).Dispose();

Console.WriteLine($"Sending {totalRequests} GET requests to {url} with concurrency {concurrency}...");

var latencies = new double[totalRequests];
var failures = 0;
var next = -1;
var total = Stopwatch.StartNew();

var workers = Enumerable.Range(0, concurrency).Select(async _ =>
{
    int index;
    while ((index = Interlocked.Increment(ref next)) < totalRequests)
    {
        var sw = Stopwatch.StartNew();
        using var response = await client.GetAsync(url);
        latencies[index] = sw.Elapsed.TotalMilliseconds;
        if ((int)response.StatusCode != 302)
            Interlocked.Increment(ref failures);
    }
});
await Task.WhenAll(workers);
total.Stop();

Array.Sort(latencies);
double Percentile(double p) => latencies[Math.Clamp((int)Math.Ceiling(p / 100 * latencies.Length) - 1, 0, latencies.Length - 1)];

Console.WriteLine($"""

    Requests:    {totalRequests} ({failures} not 302)
    Throughput:  {totalRequests / total.Elapsed.TotalSeconds:F0} req/s
    Mean:        {latencies.Average():F1} ms
    P50:         {Percentile(50):F1} ms
    P95:         {Percentile(95):F1} ms
    P99:         {Percentile(99):F1} ms
    Max:         {latencies[^1]:F1} ms
    """);
