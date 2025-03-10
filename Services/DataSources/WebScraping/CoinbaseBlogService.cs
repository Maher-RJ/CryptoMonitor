﻿using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using CryptoMonitor.Core.Interfaces.DataSources;
using CryptoMonitor.Core.Models.Common;
using Microsoft.Extensions.Logging;

namespace CryptoMonitor.Services.DataSources.WebScraping
{
    public class CoinbaseBlogService : ITokenProvider
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<CoinbaseBlogService> _logger;
        private readonly string _blogUrl;

        public string SourceName => "CoinbaseBlog";
        public TokenSource TokenSource => TokenSource.CoinbaseBlog;

        public CoinbaseBlogService(HttpClient httpClient, ILogger<CoinbaseBlogService> logger, string blogUrl = "https://blog.coinbase.com")
        {
            _httpClient = httpClient;
            _logger = logger;
            _blogUrl = blogUrl;
        }

        public async Task<List<Token>> GetTokensAsync()
        {
            // TODO: Implement blog scraping logic
            // This is a placeholder for future implementation
            _logger.LogInformation("CoinbaseBlogService is not yet implemented");
            return new List<Token>();
        }
    }
}