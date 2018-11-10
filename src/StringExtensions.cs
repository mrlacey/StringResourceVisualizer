// <copyright file="ResourceAdornmentManager.cs" company="Matt Lacey">
// Copyright (c) Matt Lacey. All rights reserved.
// </copyright>

using System.Collections.Generic;
using System.Linq;

namespace StringResourceVisualizer
{
    public static class StringExtensions
    {
        public static int IndexOfAny(this string source, params string[] values)
        {
            var valuePostions = new Dictionary<string, int>();

            foreach (var value in values)
            {
                valuePostions.Add(value, source.IndexOf(value));
            }

            if (valuePostions.Any(v => v.Value > -1))
            {
                var result = valuePostions.Select(v => v.Value).Where(v => v > -1).OrderByDescending(v => v).FirstOrDefault();

                return result;
            }

            return -1;
        }
    }
}
