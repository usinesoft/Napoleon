using BenchmarkDotNet.Running;

#pragma warning disable CS8618

namespace NetworkBenchmark;

internal static class Program
{
    private static void Main(string[] args)
    {
        var test = 0;

        if (args.Length > 0) test = int.Parse(args[0]);

        if (test == 1)
        {
            var summary = BenchmarkRunner.Run<DataRequestsTest>();
            Console.WriteLine(summary);
            return;
        }

        if (test == 2)
        {
            var summary = BenchmarkRunner.Run<DataSynchronizationTest>();
            Console.WriteLine(summary);
            return;
        }

        var benchmark = new DataSynchronizationTest();
        benchmark.Debug().Wait();
    }
}