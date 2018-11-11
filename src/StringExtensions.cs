// <copyright file="ResourceAdornmentManager.cs" company="Matt Lacey">
// Copyright (c) Matt Lacey. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;

namespace StringResourceVisualizer
{
    public static class StringExtensions
    {
        public static int IndexOfAny(this string source, params string[] values)
        {
            try
            {
                var valuePostions = new Dictionary<string, int>();

                // Values may be duplicated if multiple apps in the project have resources with the same name.
                foreach (var value in values.Distinct())
                {
                    valuePostions.Add(value, source.IndexOf(value));
                }

                if (valuePostions.Any(v => v.Value > -1))
                {
                    var result = valuePostions.Select(v => v.Value).Where(v => v > -1).OrderByDescending(v => v).FirstOrDefault();

                    return result;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(source);
                Console.WriteLine(values);
                Console.WriteLine(e);
            }

            return -1;
        }
    }
}
