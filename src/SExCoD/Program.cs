using SELib;
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
            var animModelFile = string.Empty;
            SEModel? animModel = null;

            // A first pass to find the first instance of an XMODEL for use in XAnims
            foreach (var file in args)
            {
                var extension = Path.GetExtension(file);

                if (!extension.Equals(".xmodel_bin", cmp) && !extension.Equals(".xmodel_export", cmp))
                    continue;

                animModelFile = file;
            }


            foreach (var file in args)
            {
                try
                {
                    var extension = Path.GetExtension(file);

                    if (extension.Equals(".xmodel_bin", cmp) || !extension.Equals(".xmodel_export", cmp))
                    {
                        Console.WriteLine($"| Processing: {Path.GetFileName(file)}...");
                        var watch = Stopwatch.StartNew();
                        filesProcessed++;
                        var model = XModelLoader.Read(file);
                        model.Write(Path.ChangeExtension(file, ".semodel"));
                        Console.WriteLine($"| Processed: {Path.GetFileName(file)} in {watch.ElapsedMilliseconds / 1000.0f} seconds.");
                    }
                    else if (extension.Equals(".xanim_bin", cmp) || !extension.Equals(".xanim_export", cmp))
                    {
                        Console.WriteLine($"| Processing: {Path.GetFileName(file)}...");
                        var watch = Stopwatch.StartNew();
                        filesProcessed++;

                        // Ensure we have a main anim file, lazy loading as there is no point in reading 
                        // until we actually have an xanim.
                        if(animModel == null)
                        {
                            if (string.IsNullOrWhiteSpace(animModelFile))
                                throw new Exception("No animation model provided.");
                            if (!File.Exists(animModelFile))
                                throw new Exception("The provided animation model does not exist.");

                            animModel = XModelLoader.Read(animModelFile, true);
                        }

                        var anim = XAnimLoader.Read(file, animModel);
                        anim.Write(Path.ChangeExtension(file, ".seanim"));
                        Console.WriteLine($"| Processed: {Path.GetFileName(file)} in {watch.ElapsedMilliseconds / 1000.0f} seconds.");
                    }
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