using System.Diagnostics;
using HugeCsv.DataImport;

namespace HugeCsv
{
    internal class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Please specify a path to csv file");
                return;
            }

            if (!File.Exists(args[0]))
            {
                Console.WriteLine($"File not found: {args[0]}");
                return;
            }

            var testFile = args[0];

            var importer = new CsvImporter();
            importer.ReportProgressEvery(100_000, () =>
            {
                Console.Write(".");
            });

            var watch = new Stopwatch();

            watch.Start();

            var table = importer.ProcessFile(testFile);

            watch.Stop();
            Console.WriteLine();
            Console.WriteLine($"Finished in {watch.Elapsed.TotalSeconds} seconds");
            
            GC.Collect();

            Process currentProcess = Process.GetCurrentProcess();
            long totalBytesOfMemoryUsed = currentProcess.WorkingSet64;

            Console.WriteLine($"Used {totalBytesOfMemoryUsed/1_000_000M:N0} MB of memory");

            Console.WriteLine(table.ToString());

        }
    }
}
