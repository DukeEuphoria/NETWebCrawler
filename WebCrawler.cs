using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;


namespace NETWebCrawler
{
    class WebCrawler
    {
        public class CrawlRequest(string url, int depth)
        {
            public string Url { get; set; } = url;
            public int Depth { get; set; } = depth;
        }

        public class CrawlResult(string url, CrawlResult.Status crawlStatus)
        {
            public enum Status
            {
                Success,
                Failed,
                Skipped,
                Timeout
            }

            public string Url { get; set; } = url;
            public Status CrawlStatus { get; set; } = crawlStatus;
        }

        Queue<CrawlRequest>     m_RequestList;
        HashSet<string>         m_Visited;

        List<CrawlResult>       m_Results;

        HttpClient m_Client;
        Task? m_CrawlTask;

        public WebCrawler()
        {
            m_RequestList = new Queue<CrawlRequest>();
            m_Visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            m_Results = new List<CrawlResult>();
            m_Client = new HttpClient();
        }

        public bool StartCrawl(List<string> startUrl, int maxDepth)
        {
            if (startUrl == null || startUrl.Count == 0)
            {
                Console.WriteLine("Start URL cannot be null or empty.", nameof(startUrl));
                return false;
            }

            foreach (string pageUrl in startUrl)
            {
                if (string.IsNullOrWhiteSpace(pageUrl))
                    continue;

                bool goodUrl = FixHumanUrl(pageUrl, out Uri? fixedUri);
                if (goodUrl && fixedUri != null)
                {
                    string link = MakeAbsoluteUrl(fixedUri.AbsoluteUri, ""); //Stops the user being able to give us a relative URL as the start URL
                    if (m_Visited.Contains(link))
                        continue;           //code ordering here Stop the user giving us 10 of the same link (Grr naughty user!)

                    m_Visited.Add(link);
                    CrawlRequest request = new CrawlRequest(link, 0);
                    m_RequestList.Enqueue(request);
                }
            }

            if (m_RequestList.Count == 0)
            {
                Console.WriteLine("No valid start URLs provided.");
                return false;                
            }

            m_CrawlTask = CreateCrawlTask(maxDepth);
            return true;
        }

        public bool IsCrawling()
        {
            return m_CrawlTask != null && !m_CrawlTask.IsCompleted;
        }

        private async Task CreateCrawlTask(int maxDepth)
        {
            Console.WriteLine("Starting crawl. Initial request count: " + m_RequestList.Count);
            while (m_RequestList.Count > 0)
            {
                CrawlRequest request = m_RequestList.Dequeue();
                string pageUrl = request.Url ?? "";

                string html = await GetWebPageAsString(pageUrl);
                if (string.IsNullOrEmpty(html))
                    continue;

                //O.o AutoComplete just banged this REGEX straight in. It gives me Teh FEER!
                MatchCollection matches = Regex.Matches(html, @"<a\s+(?:[^>]*?\s+)?href=([""'])(.*?)\1", RegexOptions.IgnoreCase);
                foreach (Match match in matches)
                {
                                                            //group 0 contains the entire match
                                                            //group 1 contains the quote character used around the href value
                    string link = match.Groups[2].Value;    //Group 2 contains the href value, could be anything at this point.

                    link = MakeAbsoluteUrl(pageUrl, link);

                    bool goodLink = !string.IsNullOrEmpty(link);
                    bool goodDepth = request.Depth + 1 <= maxDepth;
                    bool notVisited = !m_Visited.Contains(link);


                    if (goodLink && goodDepth && notVisited)
                    {
                        CrawlRequest newRequest = new CrawlRequest(link, request.Depth + 1);
                        m_RequestList.Enqueue(newRequest);

                        m_Visited.Add(link);    //Mark this link as visited immediately after enqueueing to prevent duplicate processing
                    }
                }
            }
            Console.WriteLine("Crawling complete.");
        }

        
        /// <summary>
        /// Fetches the web page at the specified URL as a string.
        /// Returns an empty string if the request fails, times out, or the content is not HTML.
        /// </summary>
        /// <param name="pageUrl">The URL of the web page to fetch.</param>
        /// <returns>The HTML content of the web page as a string, or an empty string if the request fails, times out, or the content is not HTML.</returns>
        private async Task<string> GetWebPageAsString(string pageUrl)
        {
            try
            {
                using HttpResponseMessage response = await m_Client.GetAsync(pageUrl);

                if (response.IsSuccessStatusCode)
                {
                    m_Results.Add(new CrawlResult(pageUrl, CrawlResult.Status.Success));
                }
                else
                {
                    m_Results.Add(new CrawlResult(pageUrl, CrawlResult.Status.Failed));  //Cannot fetch
                    return string.Empty;
                }

                //Good response, but is it a good link ?
                string mediaType = response.Content.Headers.ContentType?.MediaType ?? "unknown media";

                if (mediaType != "text/html" && mediaType != "application/xhtml+xml")
                {
                    m_Results.Add(new CrawlResult(pageUrl, CrawlResult.Status.Skipped)); //Not HTML
                    return string.Empty;
                }

                //At this point, we have a successful HTTP response with HTML content.
                string html = await response.Content.ReadAsStringAsync();
                return html;
            }


            catch (Exception ex)
            {
                switch (ex.GetType())
                {
                    case Type t when t == typeof(TaskCanceledException):
                    {
                        m_Results.Add(new CrawlResult(pageUrl, CrawlResult.Status.Timeout)); //TaskCanceledException
                        break;
                    }

                    default:
                    case Type t when t == typeof(HttpRequestException):
                    {
                        //Could get more detail here if we wanted to log the exception message or inner exception
                        m_Results.Add(new CrawlResult(pageUrl, CrawlResult.Status.Failed)); //HttpRequestException
                        break;
                    }
                }
            }

            return string.Empty;
        }

        //we may need to add an HTTP to the url the people pass in
        private static bool FixHumanUrl(string url, out Uri? uri)
        {
            uri = null;

            if (Uri.TryCreate(url, UriKind.Absolute, out Uri? parsed) &&
                (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps))
            {
                uri = parsed;
                return true;
            }

            // Try treating the input as a hostname or URL missing its scheme.
            return Uri.TryCreate("https://" + url, UriKind.Absolute, out uri);
        }


        /// <summary>
        /// Converts a relative URL to an absolute URL based on the specified base URL.
        /// Returns an empty string if the URL cannot be resolved or is not an HTTP/HTTPS URL.
        /// </summary>
        /// <param name="baseUrl">The base URL to resolve against.</param>
        /// <param name="link">The relative or absolute URL to resolve.</param>
        /// <returns>The absolute URL as a string, or an empty string if it cannot be resolved.</returns>
        string MakeAbsoluteUrl(string baseUrl, string link)
        {
            bool goodurl = Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? baseUri);

            if (goodurl)
            {
                bool goodLink=Uri.TryCreate(baseUri, link, out Uri? resolved);
            
                if (goodLink && resolved!=null && (resolved.Scheme == Uri.UriSchemeHttp || resolved.Scheme == Uri.UriSchemeHttps)) //Relying on IF execution order to ensure 'resolved' is not null and has a valid HTTP/HTTPS scheme
                {
                    UriBuilder builder = new UriBuilder(resolved);
                    builder.Fragment = "";
                    return  builder.Uri.AbsoluteUri;
                }
            }
            return string.Empty;
        }


        public void ReportResults()
        {
            Console.WriteLine("Crawled URLs:");
            foreach (CrawlResult url in m_Results)
            {
                Console.WriteLine($"{url.Url}  Status: {url.CrawlStatus}");
            }
        }
    }
}