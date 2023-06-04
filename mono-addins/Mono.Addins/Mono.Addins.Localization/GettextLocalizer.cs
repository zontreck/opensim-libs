// GettextLocalizer.cs
//
// Author:
//   Lluis Sanchez Gual
//
// Copyright (C) 2007 Novell, Inc (http://www.novell.com)
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
//
//

namespace Mono.Addins.Localization;

internal class GettextLocalizer : IAddinLocalizerFactory, IAddinLocalizer, IPluralAddinLocalizer
{
    private GettextDomain domain;

    public string GetString(string msgid)
    {
        return domain.GetString(msgid);
    }

    public IAddinLocalizer CreateLocalizer(RuntimeAddin addin, NodeElement element)
    {
        var pkg = element.GetAttribute("catalog");
        if (pkg.Length == 0)
            pkg = addin.Id;
        var dir = element.GetAttribute("location");
        if (dir.Length == 0)
            dir = "locale";
        dir = addin.GetFilePath(dir);
        domain = new GettextDomain();
        domain.Init(pkg, dir);
        return this;
    }

    public string GetPluralString(string singular, string defaultPlural, int n)
    {
        return domain.GetPluralString(singular, defaultPlural, n);
    }
}