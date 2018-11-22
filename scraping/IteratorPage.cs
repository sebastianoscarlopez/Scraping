using AngleSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace scraping
{
	public class IteratorPage
	{
		private readonly string selector;
		private readonly string attr;
		private readonly IConfiguration config;

		/// <summary>
		/// Itera por cada elemento
		/// </summary>
		/// <param name="url">URL</param>
		/// <param name="selector">Query selector</param>
		public IteratorPage(string url, string selector, string attr){
			Url = url;
			this.selector = selector;
			this.attr = attr;
			this.config = Configuration.Default.WithDefaultLoader();
		}

		public string Url { get; }

		public async Task ProcessPage(){
			var document = await BrowsingContext.New(config).OpenAsync(Url);
			var cells = document.QuerySelectorAll(selector);
			var titles = cells.Select(m => m.Attributes[attr].Value);
		}
	}
}
