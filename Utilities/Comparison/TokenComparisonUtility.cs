using System;
using System.Collections.Generic;
using System.Linq;

namespace CryptoMonitor.Utilities.Comparison
{
    public static class TokenComparisonUtility
    {
        public static List<T> FindNewTokens<T, TKey>(
            List<T> currentTokens,
            List<T> previousTokens,
            Func<T, TKey> keySelector)
        {
            if (currentTokens == null || previousTokens == null)
            {
                throw new ArgumentNullException(
                    currentTokens == null ? nameof(currentTokens) : nameof(previousTokens));
            }

            var previousKeys = previousTokens.Select(keySelector).ToHashSet();
            return currentTokens.Where(t => !previousKeys.Contains(keySelector(t))).ToList();
        }

        public static List<T> FindNewTokens<T>(
            List<T> currentTokens,
            List<T> previousTokens,
            Func<T, T, bool> comparer)
        {
            if (currentTokens == null || previousTokens == null)
            {
                throw new ArgumentNullException(
                    currentTokens == null ? nameof(currentTokens) : nameof(previousTokens));
            }

            return currentTokens
                .Where(current => !previousTokens.Any(previous => comparer(current, previous)))
                .ToList();
        }
    }
}