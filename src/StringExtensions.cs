// <copyright file="StringExtensions.cs" company="Matt Lacey">
// Copyright (c) Matt Lacey. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StringResourceVisualizer
{
    public static class StringExtensions
    {
        public static async Task<int> IndexOfAnyAsync(this string source, params string[] values)
        {
            try
            {
                var valuePositions = new Dictionary<string, int>();

                // Values may be duplicated if multiple apps in the project have resources with the same name.
                foreach (var value in values.Distinct())
                {
                    valuePositions.Add(value, source.IndexOf(value));
                }

                if (valuePositions.Any(v => v.Value > -1))
                {
                    var result = valuePositions.Where(v => v.Value > -1).OrderByDescending(v => v.Key.Length).FirstOrDefault().Value;

                    return result;
                }
            }
            catch (Exception ex)
            {
                await OutputPane.Instance?.WriteAsync("Error in IndexOfAnyAsync");
                await OutputPane.Instance?.WriteAsync(source);
                await OutputPane.Instance?.WriteAsync(string.Join("|", values));
                await OutputPane.Instance?.WriteAsync(ex.Message);
                await OutputPane.Instance?.WriteAsync(ex.Source);
                await OutputPane.Instance?.WriteAsync(ex.StackTrace);
            }

            return -1;
        }

        public static async Task<List<int>> GetAllIndexesAsync(this string source, params string[] values)
        {
            var result = new List<int>();

            try
            {
                var startPos = 0;

                while (startPos > -1 && startPos <= source.Length)
                {
                    var index = await source.Substring(startPos).IndexOfAnyAsync(values);

                    if (index > -1)
                    {
                        result.Add(startPos + index);
                        startPos = startPos + index + 1;
                    }
                    else
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                await OutputPane.Instance?.WriteAsync("Error in GetAllIndexesAsync");
                await OutputPane.Instance?.WriteAsync(source);
                await OutputPane.Instance?.WriteAsync(string.Join("|", values));
                await OutputPane.Instance?.WriteAsync(ex.Message);
                await OutputPane.Instance?.WriteAsync(ex.Source);
                await OutputPane.Instance?.WriteAsync(ex.StackTrace);
            }

            return result;
        }

        public static async Task<List<int>> GetAllIndexesCaseInsensitiveAsync(this string source, string searchTerm)
        {
            var result = new List<int>();

            try
            {
                var startPos = 0;

                while (startPos > -1 && startPos <= source.Length)
                {
                    var index = source.Substring(startPos).IndexOf(searchTerm, StringComparison.InvariantCultureIgnoreCase);

                    if (index > -1)
                    {
                        result.Add(startPos + index);
                        startPos = startPos + index + 1;
                    }
                    else
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                await OutputPane.Instance?.WriteAsync("Error in GetAllIndexesCaseInsensitiveAsync");
                await OutputPane.Instance?.WriteAsync(source);
                await OutputPane.Instance?.WriteAsync(searchTerm);
                await OutputPane.Instance?.WriteAsync(ex.Message);
                await OutputPane.Instance?.WriteAsync(ex.Source);
                await OutputPane.Instance?.WriteAsync(ex.StackTrace);
            }

            return result;
        }
    }
}
