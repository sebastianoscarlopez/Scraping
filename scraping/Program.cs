using AngleSharp;
using AngleSharp.Dom;
using System;

namespace scraping
{
	class Program
	{
		static void Main(string[] args)
		{
			var address = "https://www.seg.com.ar";
			var url = $"{address}/categoria/Automatizaciones";
			Console.Write($"Starting Scraping {url}");
			
			var iteratorPage = new IteratorPage(url, ".product-item-wrapper a", (IElement element) => $"{address}{element.Attributes["href"].Value}");

			iteratorPage.GetUrlsPages().Wait();

			foreach(var u in iteratorPage.Urls){
				Console.WriteLine(u);
			}
			Console.ReadKey();
		}
	}
}
