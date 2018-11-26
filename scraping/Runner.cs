using AngleSharp;
using AngleSharp.Dom;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
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
            var carpetaArchivos = @"c:\temporal\seg";
            var carpetaImagenes = $@"{carpetaArchivos}\imagenes";
            var categorias = new List<(int, string)>(new[] {
				(1, "Automatizaciones/Portones/Corredizos"),
				(2, "Automatizaciones/Portones/Levadizos"),
				(3, "Automatizaciones/Portones/Pivotantes"),
				(4, "Automatizaciones/Accesorios"),
				(5, "CCTV"),
				(6, "Intrusión/Alarmas"),
                (7, "Control-de-Accesos"),
                (8, "Accesorios")
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
            var urlProductos = new ConcurrentBag<(int, string)>();
            var productosNombres = new ConcurrentBag<(int, string)>();
            var productosBreves = new ConcurrentBag<(int, string)>();
            var productosDescripcion = new ConcurrentBag<(int, string)>();
            var productosDetalles = new ConcurrentBag<(int, string)>();
            var urlImagenes = new ConcurrentBag<(int, string)>();

            var scrapUrlProducto = new Scrapper(
                selector: ".product-item-wrapper .product-title a:first-child",
                scrap: (IElement element, int idPage, int idCategoria, string url, int idElement) =>
                        urlProductos.Add((idCategoria, $"{urlBase}{element.Attributes["href"].Value}")));
            
            var scrapProductoNombre = new Scrapper(
                selector: ".title:first-child",
                scrap: (IElement element, int idProducto, int idCategoria, string url, int idElement) =>
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
                scrap: (IElement element, int idProducto, int idCategoria, string url, int idElement) =>
                    productosBreves.Add((idProducto, $"{element.InnerHtml}")));

            var scrapProductoDescripcion = new Scrapper(
                selector: "#product .description",
                scrap: (IElement element, int idProducto, int idCategoria, string url, int idElement) =>
                    productosDescripcion.Add((idProducto, $"{element.InnerHtml}")));

            var scrapProductoDetalle = new Scrapper(
                selector: "#tabs-information .panel-body h4",
                scrap: (IElement element, int idProducto, int idCategoria, string url, int idElement) =>
                    productosDetalles.Add((idProducto, $"{element.QuerySelector("strong").TextContent}{element.QuerySelector("span").TextContent}")));
            
            var scrapProductoImagen = new Scrapper(
                selector: ".image-item img",
                scrap: (IElement element, int idProducto, int idCategoria, string url, int idElement) =>
                    urlImagenes.Add((idProducto, new Uri(new Uri(urlBase), element.Attributes["src"].Value).AbsoluteUri)));

            logger.LogTrace($"Starting Scraping categorias");

            var pagesCategorias = new IteratorPage(config);
            pagesCategorias.ProcessPages(urlCategorias, scrapUrlProducto);
            
            logger.LogTrace($"Scraping en {urlProductos.Count} productos");
            var pagesProductos = new IteratorPage(config);
            pagesProductos.ProcessPages(
                    urlProductos,
                    scrapProductoNombre, scrapProductoBreve, scrapProductoDescripcion, scrapProductoDetalle, scrapProductoImagen);

            logger.LogTrace($"Generando información");

            var exp = new Regex(@"([^\:]+)\:(.*)");
            var especificacion = new List<(int, string)>();
            var relProductosEspecificaciones = new List<(int, int, string)>();
            var idEspecificacion = 0;
            foreach (var d in productosDetalles
                    .GroupBy(e => exp.Replace(e.Item2, "$1").Trim())){
                especificacion.Add((++idEspecificacion, d.Key));
                relProductosEspecificaciones.AddRange(
                        from r in d
                        select (r.Item1, idEspecificacion, exp.Replace(r.Item2, "$2").Trim())
                    );
            }

            CrearEspecificacionesXML(carpetaArchivos, especificacion, relProductosEspecificaciones);


            var categoriaProductos = new List<(int, string)>();
            foreach (var r in relCategoriaProductos)
            {
                categoriaProductos.AddRange(
                    from i in r.Value
                    select (r.Key, i.ToString())
                );
            }
			CrearCategoriasXML(carpetaArchivos, categorias);

			CrearProductoXML(carpetaArchivos, productosNombres, productosBreves, productosDescripcion, categoriaProductos);

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

            var productoImagenes = new List<(int, int)>();
            foreach(var r in relProductoImagenes)
            {
                productoImagenes.AddRange(
                    from i in r.Value
                    select (r.Key, i)
                );
            }

            CrearProductoImagenesXML(carpetaArchivos, productoImagenes);
            logger.LogTrace("Termino");
        }

		private void CrearCategoriasXML(string path, List<(int, string)> categorias)
		{
			var root = new XElement("Categorias",
					from e in categorias
					select new XElement("Categoria",
							new XAttribute("IdCategoria", e.Item1),
							new XAttribute("Nombre", e.Item2)
					));
			root.Save($@"{path}\categorias.xml");
		}
		
		private void CrearEspecificacionesXML(string path, List<(int, string)> especificacion, List<(int, int, string)> relProductosEspecificaciones)
        {
            var root = new XElement("Especificaciones",
                    from e in especificacion
                    select new XElement("Especificacion",
                            new XAttribute("IdEspecificacion", e.Item1),
                            new XAttribute("Nombre", e.Item2)
                    ));
            root.Save($@"{path}\especificaciones.xml");

            root = new XElement("ProductosEspecificaciones",
                    from r in relProductosEspecificaciones
                    select new XElement("Values",
                        new XAttribute("IdProducto", r.Item1),
                        new XAttribute("IdEspecificacion", r.Item2),
                        new XAttribute("Texto", r.Item3)
                    ));

            root.Save($@"{path}\productosEspecificacion.xml");
        }

        /// <summary>
        /// Arma xml de productos
        /// </summary>
        /// <param name="path">carpeta</param>
        /// <param name="datos">nombres, breve, descripcion, categorias en este orden</param>
        private void CrearProductoXML(string path, params IEnumerable<(int, string)>[] datos)
        {
            var root = new XElement("Productos", (
                    from d in datos[0]
                    select new XElement("Producto",
                        new XAttribute("IdCategoria", datos[3].Where(i => int.Parse(i.Item2) == d.Item1).Single().Item1),
                        new XAttribute("IdProducto", d.Item1),
                        new XAttribute("Nombre", d.Item2 ?? ""),
                        new XAttribute("Breve", datos[1].Where(i => i.Item1 == d.Item1).FirstOrDefault().Item2 ?? ""),
                        new XAttribute("Descripcion", datos[2].Where(i => i.Item1 == d.Item1).FirstOrDefault().Item2 ?? ""))
                ));            
            root.Save($@"{path}\productos.xml");
        }

        private void CrearProductoImagenesXML(string path, IEnumerable<(int, int)> imagenes)
        {
            var root = new XElement("ProductosImagenes",
                    from i in imagenes
                    select new XElement("Values",
                        new XAttribute("IdProducto", i.Item1),
                        new XAttribute("IdImagen", i.Item2)
                ));
            root.Save($@"{path}\productosImagenes.xml");
        }
    }
}
