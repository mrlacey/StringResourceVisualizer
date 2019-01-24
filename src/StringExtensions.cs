// <copyright file="StringExtensions.cs" company="Matt Lacey">
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
            catch (Exception e)
            {
                Console.WriteLine(source);
                Console.WriteLine(values);
                Console.WriteLine(e);
            }

            return -1;
        }

        public static List<int> GetAllIndexes(this string source, params string[] values)
        {
            var result = new List<int>();

            try
            {
                var startPos = 0;

                while (startPos > -1 && startPos <= source.Length)
                {
                    var index = source.Substring(startPos).IndexOfAny(values);

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
            catch (Exception e)
            {
                Console.WriteLine(source);
                Console.WriteLine(values);
                Console.WriteLine(e);
            }

            return result;
        }
    }
}
