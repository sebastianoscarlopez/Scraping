using AngleSharp.Dom;
using Microsoft.Extensions.Logging;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace scraping
{
    public class Runner
    {
        private readonly ILogger<Runner> logger;

        public Runner(ILogger<Runner> logger)
        {
            this.logger = logger;
        }

        public void Run()
        {
            logger.LogTrace("Scraping initilize");

            var urlBase = "https://www.seg.com.ar";
            var urlProductos = new Dictionary<int, (int, string)>(); // idProducto, idCategoria
            var productosNombres = new Dictionary<int, string>(); // idProducto
            var productosBreves = new Dictionary<int, string>(); // idProducto

            var scrapUrlProducto = new Scrapper(
                    selector: ".product-item-wrapper .product-title a:first-child",
                    scrap: (IElement element, int key, string url) => urlProductos.Add(urlProductos.Count + 1, (key, $"{urlBase}{element.Attributes["href"].Value}")));
            
            var scrapProductoNombre = new Scrapper(
                selector: ".title:first-child",
                scrap: (IElement element, int key, string url) => productosNombres.Add(key, $"{element.InnerHtml}"));

            var scrapProductoBreve = new Scrapper(
                selector: ".title+h2",
                scrap: (IElement element, int key, string url) => productosBreves.Add(key, $"{element.InnerHtml}"));
                
            var urlCategorias = new Dictionary<int, string>(
                new[] {
                    new KeyValuePair<int, string>(1, $"{urlBase}/categoria/CCTV")
                });
            logger.LogTrace($"Starting Scraping categorias");

            var pagesCategorias = new IteratorPage(logger);
            pagesCategorias.ProcessPages(urlCategorias, scrapUrlProducto);

            var pagesProductos = new IteratorPage(logger);
            pagesProductos.ProcessPages(
                    new Dictionary<int, string>(
                        urlProductos
                        .Select(u => new KeyValuePair<int, string>(u.Key, u.Value.Item2))
                    ),
                    scrapProductoNombre, scrapProductoBreve);

            logger.LogTrace("Termino");

            //logger.LogTrace($"Total pages: {iteratorPage.Urls.Count}");
            //iteratorPage.ProcessPages(new Scrapper(".title", (element) => element.TextContent));
        }


    }
}
