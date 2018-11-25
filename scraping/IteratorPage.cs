using AngleSharp;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace scraping
{
    public class IteratorPage
	{
		private readonly IConfiguration config;

        public IteratorPage(IConfiguration config)
        {
            this.config = config;
        }

        /// <summary>
        /// Procesas paginas y realiza los scrappers indicados en cada una
        /// </summary>
        /// <param name="urls">Todas las url a las que se hará sraping en forma paralela</param>
        /// <param name="scrappers">scrappers por cada página, se pasa la key y la url al scraper</param>
        public void ProcessPages(IEnumerable<(int, string)> urls, params Scrapper[] scrappers)
        {
            var idPage = 1;
            var _lock = new object();
            Parallel.ForEach(urls, (url) =>
            {
                var taskDocument = Task.Run(() => BrowsingContext.New(config).OpenAsync(url.Item2));
                taskDocument.Wait();
                var document = taskDocument.Result;
                var auxIdPage = 0;
                lock (_lock)
                {
                    auxIdPage = idPage++;
                }
                Parallel.ForEach(scrappers,
                    s => s.Process(document, auxIdPage, url.Item1, url.Item2)
                );
            });
        }
	}
}
