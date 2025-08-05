// <copyright file="CultureInfoHelper.cs" company="Matt Lacey">
// Copyright (c) Matt Lacey. All rights reserved.
// </copyright>

using System;
using System.Globalization;
using System.Threading.Tasks;

namespace StringResourceVisualizer
{
	/// <summary>
	/// A static helper for CultureInfo classes.
	/// </summary>
	public static class CultureInfoHelper
	{
		public static async Task<CultureInfo?> CreateCultureByNameAsync(
			string cultureName
			)
		{
			if (string.IsNullOrWhiteSpace(cultureName))
			{
				return null;
			}

			try
			{
				return new CultureInfo(cultureName);
			}
			catch (Exception)
			{
				await OutputPane.Instance?.WriteAsync($"Failed to parse culture name: {cultureName}");
			}

			return null;
		}

		public static bool Equals(CultureInfo? x, CultureInfo? y)
		{
			if (x is null && y is null)
			{
				return true;
			}
			if (x is not null && y is null)
			{
				return false;
			}
			if (x is null && y is not null)
			{
				return false;
			}

			return x.TwoLetterISOLanguageName == y.TwoLetterISOLanguageName || x.Equals(y);
		}
	}
}
