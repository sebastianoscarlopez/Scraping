using AngleSharp;
using System;

namespace scraping
{
	class Program
	{
		static void Main(string[] args)
		{
			var address = "https://www.seg.com.ar/categoria/Automatizaciones";
			Console.Write($"Starting Scraping {address}");
			
			var iteratorPage = new IteratorPage(address, ".product-item-wrapper a", "href");

			iteratorPage.ProcessPage().Wait();

		}


	}
}
