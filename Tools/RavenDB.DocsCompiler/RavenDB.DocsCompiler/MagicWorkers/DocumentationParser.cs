﻿using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MarkdownDeep;

namespace RavenDB.DocsCompiler.MagicWorkers
{
	public static class DocumentationParser
	{
		static readonly Regex CodeFinder = new Regex(@"{CODE\s+(.+)/}", RegexOptions.Compiled);
		static readonly Regex NotesFinder = new Regex(@"{(NOTE|WARNING|INFO|TIP|BLOCK)\s+(.+)/}", RegexOptions.Compiled);
		static readonly Regex FirstLineSpacesFinder = new Regex(@"^(\s|\t)+", RegexOptions.Compiled);

		public static string Parse(Compiler docsCompiler, string fullPath)
		{
			if (!File.Exists(fullPath))
				throw new FileNotFoundException(string.Format("{0} was not found", fullPath));

			var contents = File.ReadAllText(fullPath);
			contents = CodeFinder.Replace(contents, match => GenerateCodeBlock(match.Groups[1].Value.Trim(), docsCompiler.CodeSamplesPath));
			contents = contents.ResolveMarkdown();
			contents = NotesFinder.Replace(contents, match => InjectNoteBlocks(match.Groups[1].Value.Trim(), match.Groups[2].Value.Trim()));

			contents = contents.Replace(@".markdown""", @".html"""); // TEMP HACK TO ALLOW STATIC HTML VIEWS

			return contents;
		}

		private static string InjectNoteBlocks(string blockType, string blockText)
		{
			return string.Format(@"<div class=""{0}-block block""><span>{1}</span></div>", blockType.ToLower(), blockText);
		}

		//private static void ParseFolder(string fullPath)
		//{
		//    throw new NotImplementedException();
		//}

		private static string GenerateCodeBlock(string value, string codeSamplesPath)
		{
			var values = value.Split('@');
			var section = values[0];
			var file = values[1];

			var fileContent = LocateCodeFile(codeSamplesPath, file);
			return "<pre class=\"brush: csharp\">" + Environment.NewLine
				   + ConvertMarkdownCodeStatment(ExtractSection(section, fileContent))
				   .Replace("<", "&lt;") // to support syntax highlighting on pre tags
				   + "</pre>";
		}

		private static string ConvertMarkdownCodeStatment(string code)
		{
			var line = code.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
			var firstLineSpaces = GetFirstLineSpaces(line.FirstOrDefault());
			var firstLineSpacesLength = firstLineSpaces.Length;
			var formattedLines = line.Select(l => string.Format("    {0}", l.Substring(l.Length < firstLineSpacesLength ? 0 : firstLineSpacesLength)));
			return string.Join(Environment.NewLine, formattedLines);
		}

		private static string GetFirstLineSpaces(string firstLine)
		{
			if (firstLine == null)
				return string.Empty;

			var match = FirstLineSpacesFinder.Match(firstLine);
			if (match.Success)
			{
				return firstLine.Substring(0, match.Length);
			}
			return string.Empty;
		}

		private static string ExtractSection(string section, string file)
		{
			// NOTE: Nested regions are not supported
			var startText = string.Format("#region {0}", section);
			var start = file.IndexOf(startText) + startText.Length;
			var end = file.IndexOf("#endregion", start);
			var sectionContent = file.Substring(start, end - start);
			return sectionContent.Trim(Environment.NewLine.ToCharArray());
		}

		private static string LocateCodeFile(string codeSamplesPath, string file)
		{
			var codePath = Path.Combine(codeSamplesPath, file);
			if (File.Exists(codePath) == false)
				throw new FileNotFoundException(string.Format("{0} was not found", codePath));
			return File.ReadAllText(codePath);
		}

		public static string ResolveMarkdown(this string content)
		{
			// http://www.toptensoftware.com/markdowndeep/api
			var md = new Markdown
			         	{
			         		AutoHeadingIDs = true,
							ExtraMode = true,
							NoFollowLinks = false,
							SafeMode = false,
							//UrlBaseLocation = 
			         	};

			return md.Transform(content);
		}
	}
}