using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CryptoMonitor.Core.Models.Blog;

namespace CryptoMonitor.Core.Interfaces.DataSources
{
    public interface IBlogMonitor
    {
        string SourceName { get; }
        string BlogUrl { get; }

        Task<(List<BlogToken> Tokens, string HtmlContent)> GetTokensAsync();

        Task<List<BlogToken>> ExtractTokensFromHtml(string html);
    }
}