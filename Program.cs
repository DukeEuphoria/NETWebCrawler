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
        static async Task Main(string[] args)
        {
            WebCrawler crawler = new WebCrawler();

            bool startedOK=crawler.StartCrawl(args);
            if (!startedOK)
            {
                Console.WriteLine("Failed to start crawl.");
                return;
            }

            Console. Write("Crawling...");

            while (crawler.IsCrawling())
            {
                await Task.Delay(1000);
                Console.Write(".");
            }
            Console.WriteLine("Done");

            crawler.ReportResults();
        }
    }
}