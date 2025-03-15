using System;
using System.Collections.Generic;
using System.Linq;
using CryptoMonitor.Core.Interfaces.WebScraping;
using Microsoft.Extensions.Logging;

namespace CryptoMonitor.Services.WebScraping.Parsers
{
    public class ParserFactory
    {
        private readonly IEnumerable<IPageParser> _availableParsers;
        private readonly ILogger<ParserFactory> _logger;

        public ParserFactory(
            IEnumerable<IPageParser> availableParsers,
            ILogger<ParserFactory> logger)
        {
            _availableParsers = availableParsers;
            _logger = logger;
        }

        public IPageParser GetParserForUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                _logger.LogWarning("Cannot get parser for null or empty URL");
                return null;
            }

            var parser = _availableParsers.FirstOrDefault(p => p.CanParse(url));

            if (parser == null)
            {
                _logger.LogWarning($"No suitable parser found for URL: {url}");
                return null;
            }

            _logger.LogInformation($"Selected parser '{parser.ParserType}' for URL: {url}");
            return parser;
        }

        public IEnumerable<IPageParser> GetAllParsers()
        {
            return _availableParsers;
        }
    }
}