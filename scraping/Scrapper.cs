using AngleSharp.Dom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace scraping
{
    public class Scrapper
    {
        private readonly string selector;
        private readonly Action<IElement, int, string> scrap;

        public string Text { get; private set; }
        public Scrapper(string selector, Action<IElement, int, string> scrap)
        {
            this.selector = selector;
            this.scrap = scrap;
        }

        public void Process(IDocument document, int key = 0, string url = null)
        {
            var elements = document.QuerySelectorAll(selector);
            foreach(var e in elements)
            {
                scrap(e, key, url);
            }
        }
    }
}
