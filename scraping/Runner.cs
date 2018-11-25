using AngleSharp;
using AngleSharp.Dom;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;

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
            var carpetaArchivos = @"c:\temporal";
            var carpetaImagenes = $@"{carpetaArchivos}\imagenes";
            var categorias = new List<(int, string)>(new[] {
                (1, "Automatizaciones"),
                (2, "CCTV"),
                (3, "Intrusión"),
                (4, "Control-de-Accesos"),
                (5, "Accesorios")
            });

            var urlCategorias = new List<(int, string)>(
                categorias
                .Select(c =>
                    (c.Item1, $"{urlBase}/categoria/{c.Item2}")
                ));
            object __lockCategoriaProductos = new object();
            var relCategoriaProductos = new Dictionary<int, List<int>>(
                    from c in categorias
                    select new KeyValuePair<int, List<int>>(c.Item1, new List<int>())
                );
            var urlProductos = new List<(int, string)>();
            var productosNombres = new List<(int, string)>();
            var productosBreves = new List<(int, string)>();
            var productosDescripcion = new List<(int, string)>();
            var urlImagenes = new List<(int, string)>();

            var scrapUrlProducto = new Scrapper(
                selector: ".product-item-wrapper .product-title a:first-child",
                scrap: (IElement element, int idPage, int idCategoria, string url) =>
                        urlProductos.Add((idCategoria, $"{urlBase}{element.Attributes["href"].Value}")));
            
            var scrapProductoNombre = new Scrapper(
                selector: ".title:first-child",
                scrap: (IElement element, int idProducto, int idCategoria, string url) =>
                {
                    lock (__lockCategoriaProductos)
                    {
                        productosNombres.Add((idProducto, $"{element.InnerHtml}"));
                        var categoria = relCategoriaProductos.Single(c => c.Key == idCategoria);
                        categoria.Value.Add(idProducto);
                    }
                });

            var scrapProductoBreve = new Scrapper(
                selector: ".title+h2",
                scrap: (IElement element, int idProducto, int idCategoria, string url) =>
                    productosBreves.Add((idProducto, $"{element.InnerHtml}")));

            var scrapProductoDescripcion = new Scrapper(
                selector: "#product .description",
                scrap: (IElement element, int idProducto, int idCategoria, string url) =>
                    productosDescripcion.Add((idProducto, $"{element.InnerHtml}")));

            var scrapProductoImagen = new Scrapper(
                selector: ".image-item img",
                scrap: (IElement element, int idProducto, int idCategoria, string url) =>
                    urlImagenes.Add((idProducto, new Uri(new Uri(urlBase), element.Attributes["src"].Value).AbsoluteUri)));

            logger.LogTrace($"Starting Scraping categorias");

            var pagesCategorias = new IteratorPage(config);
            pagesCategorias.ProcessPages(urlCategorias, scrapUrlProducto);

            logger.LogTrace($"Total productos: {urlProductos.Count}");

            var pagesProductos = new IteratorPage(config);
            pagesProductos.ProcessPages(
                    urlProductos,
                    scrapProductoNombre, scrapProductoBreve, scrapProductoDescripcion, scrapProductoImagen);

            logger.LogTrace($"Total imagenes: {urlImagenes.Count}");

            logger.LogTrace($"Descargando imagenes en {carpetaImagenes}");

            var relProductoImagenes = new Dictionary<int, List<int>>(
                    from p in productosNombres
                    select new KeyValuePair<int, List<int>>(p.Item1, new List<int>())
                );

            var __lockIdImagen = new object();
            var idImagen = 1;
            Parallel.ForEach(urlImagenes, (img) =>
            {
                try
                {
                    using (var client = new HttpClient())
                    {
                        var taskImg = Task.Run(() => client.GetAsync(img.Item2));
                        taskImg.Wait();
                        using (var response = taskImg.Result)
                        {
                            var contentType = response.Content.Headers.GetValues("content-type").FirstOrDefault();
                            if (contentType == null || contentType.Contains("text")) return;
                            Task.Run(() =>
                            {
                                var auxIdImagen = 0;
                                lock (__lockIdImagen)
                                {
                                    auxIdImagen = idImagen++;
                                    relProductoImagenes
                                        .Single(p => p.Key == img.Item1)    
                                        .Value
                                        .Add(auxIdImagen);
                                }
                                var nombreImagen = $"{auxIdImagen}_{img.Item1}.jpg";
                                //var nombreImagen = $"{img.Key}_{Regex.Replace(img.Value.Item2, @".+[\/](.+)$", "$1")}";
                                //_{img.Item2.Replace("/", "_").Replace(":", "_")}
                                var nombreArchivo = $@"{carpetaImagenes}\{nombreImagen}";
                                using (var strImg = new FileStream(nombreArchivo, FileMode.Create))
                                {
                                    response.Content
                                        .CopyToAsync(strImg)
                                        .Wait();
                                }
                            })
                            .Wait();
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, $"Error al descargar {img.Item1}:{img.Item2}");
                }
            });

            logger.LogTrace($"Archivo de productos en {carpetaImagenes}");

            var productoImagenes = new List<(int, string)>();
            foreach(var r in relProductoImagenes)
            {
                productoImagenes.AddRange(
                    from i in r.Value
                    select (r.Key, i.ToString())
                );
            }

            var categoriaProductos = new List<(int, string)>();
            foreach (var r in relCategoriaProductos)
            {
                categoriaProductos.AddRange(
                    from i in r.Value
                    select (r.Key, i.ToString())
                );
            }

            CrearProductoXML($@"{carpetaArchivos}\productos.xml", productosNombres, productosBreves, productosDescripcion, productoImagenes, categoriaProductos);

            logger.LogTrace("Termino");
        }

        /// <summary>
        /// Arma xml de productos
        /// </summary>
        /// <param name="path">ruta del archivo</param>
        /// <param name="datos">nombres, breve, descripcion, imagenes, categorias en este orden</param>
        private void CrearProductoXML(string path, params List<(int, string)>[] datos)
        {
            var root = new XElement("Productos", (
                    from d in datos[0]
                    select new XElement("Producto",
                        new XAttribute("IdCategoria", datos[4].Where(i => int.Parse(i.Item2) == d.Item1).Single().Item1),
                        new XAttribute("IdProducto", d.Item1),
                        new XAttribute("Nombre", d.Item2 ?? ""),
                        new XAttribute("Breve", datos[1].Where(i => i.Item1 == d.Item1).FirstOrDefault().Item2 ?? ""),
                        new XAttribute("Descripcion", datos[2].Where(i => i.Item1 == d.Item1).FirstOrDefault().Item2 ?? ""),
                        datos[3].Where(i => i.Item1 == d.Item1).Count() > 0
                            ? new XElement("Imagenes",
                                from r in datos[3].Where(i => i.Item1 == d.Item1)
                                select new XElement("Imagen", r.Item2)
                                )
                            : new XElement("Imagenes", ""))
                ));

            logger.LogTrace(root.ToString());
            root.Save(path);
        }
    }
}
