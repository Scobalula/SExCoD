using System.Diagnostics;
using System.Reflection;

namespace SExCoD
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine($"| ---------------------------------------");
            Console.WriteLine($"| SExCoD");
            Console.WriteLine($"| A tool to convert XMODEL to SEModel");
            Console.WriteLine($"| Developed by Scobalula");
            Console.WriteLine($"| Donate: https://ko-fi.com/scobalula");
            Console.WriteLine($"| Version: {Assembly.GetExecutingAssembly().GetName().Version}");
            Console.WriteLine($"| ---------------------------------------");

            var filesProcessed = 0;
            var cmp = StringComparison.CurrentCultureIgnoreCase;
            foreach (var file in args)
            {
                try
                {
                    var extension = Path.GetExtension(file);

                    if (!extension.Equals(".xmodel_bin", cmp) && !extension.Equals(".xmodel_export", cmp))
                        continue;

                    Console.WriteLine($"| Processing: {Path.GetFileName(file)}...");
                    var watch = Stopwatch.StartNew();
                    filesProcessed++;
                    var model = XModelLoader.Read(file);
                    model.Write(Path.ChangeExtension(file, ".semodel"));
                    Console.WriteLine($"| Processed: {Path.GetFileName(file)} in {watch.ElapsedMilliseconds / 1000.0f} seconds.");
                }
                catch(Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }

            if(filesProcessed == 0)
            {
                Console.BackgroundColor = ConsoleColor.DarkRed;
                Console.WriteLine("| No valid files provided.");
                Console.WriteLine("| To use, drag and drop XMODEL_BIN/XMODEL_EXPORT files.");
                Console.ResetColor();
            }

            Console.WriteLine("| Execution complete, press Enter to exit.");
            Console.ReadLine();            
        }
    }
}