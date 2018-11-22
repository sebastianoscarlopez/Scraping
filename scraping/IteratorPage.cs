using AngleSharp;
using AngleSharp.Dom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace scraping
{
	public class IteratorPage
	{
		private readonly string url;
		private readonly string selector;
		private readonly Func<IElement, string> getURL;
		private readonly IConfiguration config;
		
		/// <summary>
		/// Itera por cada elemento
		/// </summary>
		/// <param name="url">URL</param>
		/// <param name="selector">Query selector</param>
		/// <param name="getURL">Funcion que retornara la url completa</param>
		public IteratorPage(string url, string selector, Func<IElement, string> getURL)
		{
			this.url = url;
			this.selector = selector;
			this.getURL = getURL;
			this.config = Configuration.Default.WithDefaultLoader();
		}


		public IEnumerable<string> Urls { get; private set; }

		public async Task GetUrlsPages(){
			var document = await BrowsingContext.New(config).OpenAsync(url);
			var cells = document.QuerySelectorAll(selector);
			Urls = cells
				.Select(m => getURL(m))
				.ToList();
		}
	}
}
