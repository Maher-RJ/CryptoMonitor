using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CryptoMonitor.Core.Models.Blog;

namespace CryptoMonitor.Core.Interfaces.Storage
{
    public interface IBlogRepository
    {
        Task<List<BlogToken>> GetPreviousTokensAsync(string source);
        Task SaveTokensAsync(List<BlogToken> tokens, string source);

        Task<string> GetPreviousHtmlAsync(string source);
        Task SaveHtmlAsync(string htmlContent, string source);

        Task<(List<BlogToken> NewTokens, bool HtmlChanged)> GetChangesAsync(
            List<BlogToken> currentTokens,
            string currentHtml,
            string source);
    }
}