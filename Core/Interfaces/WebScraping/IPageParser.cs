using System.Collections.Generic;
using System.Threading.Tasks;
using CryptoMonitor.Core.Models.Blog;

namespace CryptoMonitor.Core.Interfaces.WebScraping
{
    public interface IPageParser
    {
        string SourceName { get; }
        string ParserType { get; }
        bool CanParse(string url);
        Task<List<BlogToken>> ParseTokensAsync(string htmlContent);
        bool IsHtmlChanged(string oldHtml, string newHtml);
    }
}