// <copyright file="DisplayTextContainer.cs" company="Matt Lacey">
// Copyright (c) Matt Lacey. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

namespace StringResourceVisualizer
{
	/// <summary>
	/// Container for a neutral and localized versions of display strings.
	/// </summary>
	public sealed class DisplayTextContainer
	{
		private readonly List<(CultureInfo Culture, string DisplayText)> _displayTexts = new(
			);

		private string? _neutralDisplayText;

		public bool IsEmpty => _neutralDisplayText is null && _displayTexts.Count == 0;

		private DisplayTextContainer()
		{
		}


		public IEnumerable<(string? localeName, string displayText)> Enumerate()
		{
			if (_neutralDisplayText is not null)
			{
				yield return ("neutral", _neutralDisplayText);
			}

			foreach (var pair in _displayTexts)
			{
				yield return (pair.Culture.Name, pair.DisplayText);
			}
		}

		public string GetDisplayText(CultureInfo? culture)
		{
			if (culture is null)
			{
				return _neutralDisplayText ?? string.Empty;
			}

			foreach (var pair in _displayTexts)
			{
				if (CultureInfoHelper.Equals(pair.Culture, culture))
				{
					return pair.DisplayText;
				}
			}

			return string.Empty;
		}

		public void AddNeutral(string displayText)
		{
			_neutralDisplayText = displayText;
		}

		public void Add(CultureInfo? culture, string displayText)
		{
			if (culture is null)
			{
				AddNeutral(displayText);
				return;
			}

			_displayTexts.Add((culture, displayText));
		}

		public static DisplayTextContainer Create(
			List<XmlResourceFileDescriptor> docsOfInterest,
			string resourceName
			)
		{
			if (docsOfInterest is null)
			{
				throw new ArgumentNullException(nameof(docsOfInterest));
			}

			if (resourceName is null)
			{
				throw new ArgumentNullException(nameof(resourceName));
			}

			var result = new DisplayTextContainer();

			foreach (var file in docsOfInterest)
			{
				var displayText = GetDisplayTextFromDoc(file, resourceName);

				if (!string.IsNullOrWhiteSpace(displayText))
				{
					result.Add(file.Culture, displayText);
				}
			}

			return result;
		}

		private static string GetDisplayTextFromDoc(
			XmlResourceFileDescriptor file,
			string key
			)
		{
			string result = null;

			foreach (XmlElement element in file.Document.GetElementsByTagName("data"))
			{
				if (element.GetAttribute("name") == key)
				{
					var valueElement = element.GetElementsByTagName("value").Item(0);
					result = FormatDisplayText(valueElement?.InnerText);

					break;
				}
			}

			return result;
		}

		public static string FormatDisplayText(string resourceString)
		{
			var result = resourceString;

			if (result != null)
			{
				var returnIndex = result.IndexOfAny(new[] { '\r', '\n' });

				if (returnIndex == 0)
				{
					result = result.TrimStart(' ', '\r', '\n');
					returnIndex = result.IndexOfAny(new[] { '\r', '\n' });

					if (returnIndex >= 0)
					{
						// Truncate at first wrapping character and add "Return Character" to indicate truncation
						result = "⏎" + result.Substring(0, returnIndex) + "⏎";
					}
					else
					{
						result = "⏎" + result;
					}
				}
				else if (returnIndex > 0)
				{
					// Truncate at first wrapping character and add "Return Character" to indicate truncation
					result = result.Substring(0, returnIndex) + "⏎";
				}
			}

			return result;
		}
	}
}
