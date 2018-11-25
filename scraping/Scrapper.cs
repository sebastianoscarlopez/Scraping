using AngleSharp.Dom;
using System;

namespace scraping
{
    public class Scrapper
    {
        private readonly string selector;
        private readonly Action<IElement, int, int, string, int> scrap;

        public string Text { get; private set; }
        public Scrapper(string selector, Action<IElement, int, int, string, int> scrap)
        {
            this.selector = selector;
            this.scrap = scrap;
        }

        public void Process(IDocument document, int idPage, int key, string url)
        {
            var idElement = 1;
            var elements = document.QuerySelectorAll(selector);
            foreach(var e in elements)
            {
                scrap(e, idPage, key, url, idElement++);
            }
        }
    }
}
