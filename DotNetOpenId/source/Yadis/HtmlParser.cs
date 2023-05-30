using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Globalization;

namespace DotNetOpenId.Yadis {
	static class HtmlParser {
		private static readonly Regex attrRe = new Regex("\n# Must start with a sequence of word-characters, followed by an equals sign\n(?<attrname>(\\w|-)+)=\n\n# Then either a quoted or unquoted attribute\n(?:\n\n # Match everything that's between matching quote marks\n (?<qopen>[\"\\'])(?<attrval>.*?)\\k<qopen>\n|\n\n # If the value is not quoted, match up to whitespace\n (?<attrval>(?:[^\\s<>/]|/(?!>))+)\n)\n\n|\n\n(?<endtag>[<>])\n    ", flags);
		private const RegexOptions flags = (RegexOptions.IgnorePatternWhitespace | RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.IgnoreCase);
		private const string tagExpr = "\n# Starts with the tag name at a word boundary, where the tag name is\n# not a namespace\n<{0}\\b(?!:)\n    \n# All of the stuff up to a \">\", hopefully attributes.\n(?<attrs>[^>]*?)\n    \n(?: # Match a short tag\n    />\n    \n|   # Match a full tag\n    >\n    \n    (?<contents>.*?)\n    \n    # Closed by\n    (?: # One of the specified close tags\n        </?{1}\\s*>\n    \n    # End of the string\n    |   \\Z\n    \n    )\n    \n)\n    ";
		private const string startTagExpr = "\n# Starts with the tag name at a word boundary, where the tag name is\n# not a namespace\n<{0}\\b(?!:)\n    \n# All of the stuff up to a \">\", hopefully attributes.\n(?<attrs>[^>]*?)\n    \n(?: # Match a short tag\n    />\n    \n|   # Match a full tag\n    >\n    )\n    ";

		private static readonly Regex headRe = tagMatcher("head", new[] { "body" });
		private static readonly Regex htmlRe = tagMatcher("html", new string[0]);
		private static readonly Regex removedRe = new Regex(@"<!--.*?-->|<!\[CDATA\[.*?\]\]>|<script\b[^>]*>.*?</script>", flags);

		static Regex tagMatcher(string tagName, params string[] closeTags) {
			string text2;
			if (closeTags.Length > 0) {
				StringBuilder builder = new StringBuilder();
				builder.AppendFormat("(?:{0}", tagName);
				int index = 0;
				string[] textArray = closeTags;
				int length = textArray.Length;
				while (index < length) {
					string text = textArray[index];
					index++;
					builder.AppendFormat("|{0}", text);
				}
				builder.Append(")");
				text2 = builder.ToString();
			} else {
				text2 = tagName;
			}
			return new Regex(string.Format(CultureInfo.InvariantCulture, 
				tagExpr, tagName, text2), flags);
		}

		static Regex startTagMatcher(string tag_name) {
			return new Regex(string.Format(CultureInfo.InvariantCulture, startTagExpr, tag_name), flags);
		}
	}
}
