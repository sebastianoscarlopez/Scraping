using AngleSharp;
using AngleSharp.Dom;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace scraping
{
	public class IteratorPage
	{
		private readonly IConfiguration config;
        private ILogger logger;

        public IteratorPage(ILogger logger)
        {
            this.logger = logger;
            this.config = Configuration.Default.WithDefaultLoader();
        }

        /// <summary>
        /// Procesas paginas y realiza los scrappers indicados en cada una
        /// </summary>
        /// <param name="urls">Todas las url a las que se hará sraping en forma paralela</param>
        /// <param name="scrappers">scrappers por cada página, se pasa la key y la url al scraper</param>
        public void ProcessPages(IDictionary<int, string> urls, params Scrapper[] scrappers)
        {
            logger.LogTrace($"ProcessPages Total:{urls.Count}");
            Parallel.ForEach(urls, (url) =>
            {
                logger.LogTrace($"ProcessPages Url:{url.Value}");
                var taskDocument = Task.Run(() => BrowsingContext.New(config).OpenAsync(url.Value));
                taskDocument.Wait();
                var document = taskDocument.Result;
                Parallel.ForEach(scrappers,
                    (scrapper) =>
                        scrapper.Process(document, url.Key, url.Value)
                    );
            });
        }
	}
}
