using BenchmarkDotNet.Running;

#pragma warning disable CS8618

namespace NetworkBenchmark
{
    internal static class Program
    {
        static void Main(string[] args)
        {
            
            var summary = BenchmarkRunner.Run<DataRequestsTest>();
            Console.WriteLine(summary);

        }
    }
}
