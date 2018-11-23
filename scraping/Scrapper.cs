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
        private readonly Func<IElement, string> scrapText;

        public string Text { get; private set; }
        public Scrapper(string selector, Func<IElement, string> scrapText)
        {
            this.selector = selector;
            this.scrapText = scrapText;
        }

        public List<string> getFromDocument(IDocument document)
        {
            var cells = document.QuerySelectorAll(selector);
            return cells
                .Select(m => scrapText(m))
                .ToList();
        }
    }
}
