using AngleSharp;
using AngleSharp.Dom;
using System;

namespace scraping
{
	class Program
	{
		static void Main(string[] args)
		{
			var urlBase = "https://www.seg.com.ar";
			var url = $"{urlBase}/categoria/Automatizaciones";
			Console.Write($"Starting Scraping {url}");
			
			var iteratorPage = new IteratorPage(url, ".product-item-wrapper .product-title a:first-child", (IElement element) => $"{urlBase}{element.Attributes["href"].Value}");

			iteratorPage.GetUrlsPages().Wait();

			foreach(var u in iteratorPage.Urls){
				Console.WriteLine(u);
			}
            iteratorPage.ProcessPages(new Scrapper(".title", (element) => element.TextContent));

            Console.WriteLine("fin");
            Console.ReadKey();
		}
	}
}
