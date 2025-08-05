// <copyright file="XmlResourceFileDescriptor.cs" company="Matt Lacey">
// Copyright (c) Matt Lacey. All rights reserved.
// </copyright>

using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;

namespace StringResourceVisualizer
{
	/// <summary>
	/// Container for a resx file and its metadata.
	/// </summary>
	public sealed class XmlResourceFileDescriptor
	{
		public string Path
		{
			get;
		}

		public string FileNameWithoutCultureAndExtension
		{
			get;
		}

		public CultureInfo? Culture
		{
			get;
		}

		public XmlDocument Document
		{
			get;
			private set;
		}

		private XmlResourceFileDescriptor(
			string path,
			CultureInfo? culture,
			XmlDocument document
			)
		{
			Path = path;
			
			var fnwe = System.IO.Path.GetFileNameWithoutExtension(path);
			var fnwep = fnwe.Split('.');
			FileNameWithoutCultureAndExtension = fnwep[0];

			Culture = culture;
			Document = document;
		}

		public void Reload()
		{
			Document = Reload(Path);
		}

		public static async Task<XmlResourceFileDescriptor> CreateAsync(
			string resourceFile
			)
		{
			if (resourceFile is null)
			{
				throw new ArgumentNullException(nameof(resourceFile));
			}

			var document = Reload(resourceFile);

			CultureInfo? culture = null;
			var prefix = System.IO.Path.GetFileNameWithoutExtension(resourceFile);
			if (prefix.Contains("."))
			{
				var cultureName = prefix.Split('.').Last();
				culture = await CultureInfoHelper.CreateCultureByNameAsync(cultureName);
			}

			return new XmlResourceFileDescriptor(
				resourceFile,
				culture,
				document
				);
		}

		private static XmlDocument Reload(
			string resourceFile
			)
		{
			var document = new XmlDocument();
			document.Load(resourceFile);
			return document;
		}
	}
}
