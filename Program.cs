using System;
using System.Net.Http;
using System.Threading.Tasks;
namespace NETWebCrawler
{
    /// <summary>
    /// This is written as though a console command , "WEBCRAWL <url1> <url2> ..." etc...
    /// </summary>
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Please provide at least one start URL.");
                return -1;
            }

            if (args[0].Contains("-?") || args[0].Contains("-h") || args[0].Contains("-help"))
            {
                Console.WriteLine("Usage: WEBCRAWL <url1> <url2> ... [-depth <n>]");
                return 0;
            }

            int maxDepth = 2;

            List<string> startUrls = new List<string>();
            for (int i=0;i<args.Length;i++)
            {
                string arg = args[i];

                arg=arg.ToLower();
                if (arg=="-depth" || arg=="-d")
                {
                    if (i+1 < args.Length && int.TryParse(args[i+1], out int depth))
                    {
                        maxDepth = depth;
                        i++; // Skip the next argument as it is the depth value
                    }
                    continue;
                }

                if (arg.StartsWith("-depth=") || arg.StartsWith("-d="))
                {
                    string[] parts = arg.Split('=');
                    if (parts.Length == 2 && int.TryParse(parts[1], out int depth))
                    {
                        maxDepth = depth;
                    }
                    continue;
                }

                if (maxDepth <= 0 || maxDepth > 10)
                {
                    maxDepth = Math.Clamp(maxDepth, 1, 10);
                    Console.WriteLine("Invalid depth specified. Must be between 1 and 10. Depth Clamped to " + maxDepth);
                }

                startUrls.Add(arg);
            }




            WebCrawler crawler = new WebCrawler();
            bool startedOK=crawler.StartCrawl(startUrls, maxDepth);
            if (!startedOK)
            {
                Console.WriteLine("Failed to start crawl.");
                return -1;
            }

            Console. Write("Crawling. Max depth: " + maxDepth);

            while (crawler.IsCrawling())
            {
                await Task.Delay(1000);
                Console.Write(".");
            }
            Console.WriteLine("Done");

            crawler.ReportResults();
            return 0;
        }
    }
}