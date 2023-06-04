// 
// TextFormatter.cs
//  
// Author:
//       Lluis Sanchez Gual <lluis@novell.com>
// 
// Copyright (c) 2009 Novell, Inc (http://www.novell.com)
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Text;

namespace Mono.Addins.Setup;

internal enum WrappingType
{
    None,
    Char,
    Word,
    WordChar
}

internal class TextFormatter
{
    private StringBuilder builder = new();
    private int curCol;
    private StringBuilder currentWord = new();
    private string formattedIndentString;
    private int indentColumnWidth;
    private string indentString = "";
    private bool lastWasSeparator;
    private int leftMargin;
    private bool lineStart = true;
    private string paragFormattedIndentString;
    private int paragIndentColumnWidth;
    private bool paragraphStart = true;
    private int paragraphStartMargin;
    private bool tabsAsSpaces;
    private int tabWidth;
    private int wordLevel;
    private WrappingType wrap;

    public TextFormatter()
    {
        MaxColumns = 80;
        TabWidth = 4;
    }

    public int MaxColumns { get; set; }

    public int TabWidth
    {
        get => tabWidth;
        set
        {
            tabWidth = value;
            formattedIndentString = null;
        }
    }

    public string IndentString
    {
        get => indentString;
        set
        {
            if (value == null)
                throw new ArgumentNullException("value");
            indentString = value;
            formattedIndentString = null;
        }
    }

    public int LeftMargin
    {
        get => leftMargin;
        set
        {
            leftMargin = value;
            formattedIndentString = null;
        }
    }

    public int ParagraphStartMargin
    {
        get => paragraphStartMargin;
        set
        {
            paragraphStartMargin = value;
            formattedIndentString = null;
        }
    }

    public WrappingType Wrap
    {
        get => wrap;
        set
        {
            if (wrap != value)
            {
                AppendCurrentWord('x');
                wrap = value;
            }
        }
    }

    public bool TabsAsSpaces
    {
        get => tabsAsSpaces;
        set
        {
            tabsAsSpaces = value;
            formattedIndentString = null;
        }
    }

    private string FormattedIndentString
    {
        get
        {
            if (formattedIndentString == null)
                CreateIndentString();
            return formattedIndentString;
        }
    }

    private int IndentColumnWidth
    {
        get
        {
            if (formattedIndentString == null)
                CreateIndentString();
            return indentColumnWidth;
        }
    }

    private string ParagFormattedIndentString
    {
        get
        {
            if (formattedIndentString == null)
                CreateIndentString();
            return paragFormattedIndentString;
        }
    }

    private int ParagIndentColumnWidth
    {
        get
        {
            if (formattedIndentString == null)
                CreateIndentString();
            return paragIndentColumnWidth;
        }
    }

    public void Clear()
    {
        builder = new StringBuilder();
        currentWord = new StringBuilder();
        curCol = 0;
        lineStart = true;
        paragraphStart = true;
        lastWasSeparator = false;
    }

    public void AppendWord(string text)
    {
        BeginWord();
        Append(text);
        EndWord();
    }

    public void Append(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        if (builder.Length == 0)
        {
            curCol = IndentColumnWidth;
            lineStart = true;
            paragraphStart = true;
        }

        if (Wrap == WrappingType.None || Wrap == WrappingType.Char)
        {
            AppendChars(text, Wrap == WrappingType.Char);
            return;
        }

        var n = 0;

        while (n < text.Length)
        {
            var sn = n;
            var foundSpace = false;
            while (n < text.Length && !foundSpace)
                if ((char.IsWhiteSpace(text[n]) && wordLevel == 0) || text[n] == '\n')
                    foundSpace = true;
                else
                    n++;

            if (n != sn)
                currentWord.Append(text, sn, n - sn);
            if (foundSpace)
            {
                AppendCurrentWord(text[n]);
                n++;
            }
        }
    }

    public void AppendLine()
    {
        AppendCurrentWord('x');
        AppendChar('\n', false);
    }

    public void BeginWord()
    {
        wordLevel++;
    }

    public void EndWord()
    {
        if (wordLevel == 0)
            throw new InvalidOperationException("Missing BeginWord call");
        wordLevel--;

        var lastChar = 'x';
        if (currentWord.Length > 0)
        {
            lastChar = currentWord[currentWord.Length - 1];
            if (char.IsWhiteSpace(lastChar))
                currentWord.Remove(currentWord.Length - 1, 1);
        }

        AppendCurrentWord(lastChar);
    }

    public void FlushWord()
    {
        AppendCurrentWord('x');
        if (curCol > MaxColumns)
            AppendSoftBreak();
    }

    public override string ToString()
    {
        if (currentWord.Length > 0)
            AppendCurrentWord('x');
        return builder.ToString();
    }

    private void AppendChars(string s, bool wrapChars)
    {
        foreach (var c in s)
            AppendChar(c, wrapChars);
    }

    private void AppendSoftBreak()
    {
        AppendChar('\n', true);
        paragraphStart = false;
        curCol = IndentColumnWidth;
    }

    private void AppendChar(char c, bool wrapChars)
    {
        if (c == '\n')
        {
            lineStart = true;
            paragraphStart = true;
            builder.Append(c);
            curCol = ParagIndentColumnWidth;
            lastWasSeparator = false;
            return;
        }

        if (lineStart)
        {
            if (paragraphStart)
                builder.Append(ParagFormattedIndentString);
            else
                builder.Append(FormattedIndentString);
            lineStart = false;
            paragraphStart = false;
            lastWasSeparator = false;
        }

        if (wrapChars && curCol >= MaxColumns)
        {
            AppendSoftBreak();
            if (!char.IsWhiteSpace(c))
                AppendChar(c, false);
            return;
        }

        if (c == '\t')
        {
            var tw = GetTabWidth(curCol);
            if (TabsAsSpaces)
                builder.Append(' ', tw);
            else
                builder.Append(c);
            curCol += tw;
        }
        else
        {
            builder.Append(c);
            curCol++;
        }
    }

    private void AppendCurrentWord(char separatorChar)
    {
        if (currentWord.Length == 0)
            return;
        if (Wrap == WrappingType.Word || Wrap == WrappingType.WordChar)
            if (curCol + currentWord.Length > MaxColumns)
            {
                // If the last char was a word separator, remove it
                if (lastWasSeparator)
                    builder.Remove(builder.Length - 1, 1);
                if (!lineStart)
                    AppendSoftBreak();
            }

        AppendChars(currentWord.ToString(), Wrap == WrappingType.WordChar);
        if (char.IsWhiteSpace(separatorChar) || (separatorChar == '\n' && !lineStart))
        {
            lastWasSeparator = true;
            AppendChar(separatorChar, true);
        }
        else
        {
            lastWasSeparator = false;
        }

        currentWord = new StringBuilder();
    }

    private int GetTabWidth(int startCol)
    {
        var res = startCol % TabWidth;
        if (res == 0)
            return TabWidth;
        return TabWidth - res;
    }

    private void CreateIndentString()
    {
        var sb = new StringBuilder();
        indentColumnWidth = AddIndentString(sb, indentString);

        paragFormattedIndentString = sb + new string(' ', paragraphStartMargin);
        paragIndentColumnWidth = indentColumnWidth + paragraphStartMargin;

        if (LeftMargin > 0)
        {
            sb.Append(' ', LeftMargin);
            indentColumnWidth += LeftMargin;
        }

        formattedIndentString = sb.ToString();

        if (paragraphStart)
            curCol = paragIndentColumnWidth;
        else if (lineStart)
            curCol = indentColumnWidth;
    }

    private int AddIndentString(StringBuilder sb, string txt)
    {
        if (string.IsNullOrEmpty(txt))
            return 0;
        var count = 0;
        foreach (var c in txt)
            if (c == '\t')
            {
                var tw = GetTabWidth(count);
                count += tw;
                if (TabsAsSpaces)
                    sb.Append(' ', tw);
                else
                    sb.Append(c);
            }
            else
            {
                sb.Append(c);
                count++;
            }

        return count;
    }
}