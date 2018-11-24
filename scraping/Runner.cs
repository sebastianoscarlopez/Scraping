using AngleSharp;
using AngleSharp.Dom;
using Microsoft.Extensions.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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

            var config = Configuration.Default.WithDefaultLoader();

            var urlBase = "https://www.seg.com.ar";
            var carpetaArchivos = @"c:\temp";
            var carpetaImagenes = $@"{carpetaArchivos}\imagenes";
            var categorias = new Dictionary<int, string>(new[] {
                new KeyValuePair<int, string>(1, "CCTV"),
                new KeyValuePair<int, string>(2, "Intrusión")
            });

            object __lockUrlProductos = new { };
            var urlProductos = new Dictionary<int, (int, string)>(); // idProducto, idCategoria
            var productosNombres = new Dictionary<int, string>();
            var productosBreves = new Dictionary<int, string>();
            object __lockUrlImagenes = new { };
            var urlImagenes = new Dictionary<int, (int, string)>(); // idProducto

            var scrapUrlProducto = new Scrapper(
                selector: ".product-item-wrapper .product-title a:first-child",
                scrap: (IElement element, int key, string url) =>
                {
                    lock (__lockUrlProductos)
                    {
                        urlProductos.Add(urlProductos.Count + 1, (key, $"{urlBase}{element.Attributes["href"].Value}"));
                    }
                });
            
            var scrapProductoNombre = new Scrapper(
                selector: ".title:first-child",
                scrap: (IElement element, int key, string url) =>
                    productosNombres.Add(key, $"{element.InnerHtml}"));

            var scrapProductoBreve = new Scrapper(
                selector: ".title+h2",
                scrap: (IElement element, int key, string url) =>
                    productosBreves.Add(key, $"{element.InnerHtml}"));

            var scrapProductoImagen = new Scrapper(
                selector: ".image-item img",
                scrap: (IElement element, int key, string url) =>
                {
                    lock (__lockUrlProductos)
                    {
                        urlImagenes.Add(urlImagenes.Count + 1, (key, new Uri(new Uri(urlBase), element.Attributes["src"].Value).AbsoluteUri));
                    }
                });

            var urlCategorias = new Dictionary<int, string>(
                categorias
                .Select(c =>
                    new KeyValuePair<int, string>(c.Key, $"{urlBase}/categoria/{c.Value}")
                ));
            logger.LogTrace($"Starting Scraping categorias");

            var pagesCategorias = new IteratorPage(config);
            pagesCategorias.ProcessPages(urlCategorias, scrapUrlProducto);

            logger.LogTrace($"Total productos: {urlProductos.Count}");

            var pagesProductos = new IteratorPage(config);
            pagesProductos.ProcessPages(
                    new Dictionary<int, string>(
                        urlProductos
                        .Select(u => new KeyValuePair<int, string>(u.Key, u.Value.Item2))
                    ),
                    scrapProductoNombre, scrapProductoBreve, scrapProductoImagen);

            logger.LogTrace($"Total imagenes: {urlImagenes.Count}");

            logger.LogTrace($"Archivo de productos en {carpetaImagenes}");

            logger.LogTrace($"Descargando imagenes en {carpetaImagenes}");

            var strProductos = new FileStream($@"{}\productos.json", FileMode.Create);
            Parallel.ForEach(urlImagenes, (img) =>
            {
                try
                {
                    using (var client = new HttpClient())
                    {
                        var nombreImagen = $"{img.Value.Item1}_{img.Key}.jpg";
                        //var nombreImagen = $"{img.Key}_{Regex.Replace(img.Value.Item2, @".+[\/](.+)$", "$1")}";
                        var nombreArchivo = $@"{carpetaImagenes}\{nombreImagen}";
                        var taskImg = Task.Run(() => client.GetAsync(img.Value.Item2));
                        taskImg.Wait();
                        using (var response = taskImg.Result)
                        {
                            using (var strImg = new FileStream(nombreArchivo, FileMode.Create))
                            {
                                Task.Run(() => response.Content.CopyToAsync(strImg))
                                    .Wait();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, $"Error al descargar {img.Value.Item2}");
                }
            });
            logger.LogTrace("Termino");
        }
    }
}
