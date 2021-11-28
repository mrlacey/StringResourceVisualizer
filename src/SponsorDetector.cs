// <copyright file="SponsorDetector.cs" company="Matt Lacey">
// Copyright (c) Matt Lacey. All rights reserved.
// </copyright>

using System;
using System.Threading.Tasks;

namespace StringResourceVisualizer
{
    public class SponsorDetector
    {
        // This might be the code you see, but it's not what I compile into the extensions when built ;)
        public static async Task<bool> IsSponsorAsync()
        {
            return await Task.FromResult(false);
        }
    }
}
