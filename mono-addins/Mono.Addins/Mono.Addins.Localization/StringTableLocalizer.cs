// StringTableLocalizer.cs
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

using System;
using System.Collections;
using System.Threading;

namespace Mono.Addins.Localization;

internal class StringTableLocalizer : IAddinLocalizerFactory, IAddinLocalizer
{
    private static readonly Hashtable nullLocale = new();
    private readonly Hashtable locales = new();

    public string GetString(string id)
    {
        var cname = Thread.CurrentThread.CurrentCulture.Name;
        var loc = (Hashtable)locales[cname];
        if (loc == null)
        {
            var sn = cname.Substring(0, 2);
            loc = (Hashtable)locales[sn];
            if (loc != null)
            {
                locales[cname] = loc;
            }
            else
            {
                locales[cname] = nullLocale;
                return id;
            }
        }

        var msg = (string)loc[id];
        if (msg == null)
        {
            if (cname.Length > 2)
            {
                // Try again without the country
                cname = cname.Substring(0, 2);
                loc = (Hashtable)locales[cname];
                if (loc != null)
                {
                    msg = (string)loc[id];
                    if (msg != null)
                        return msg;
                }
            }

            return id;
        }

        return msg;
    }

    public IAddinLocalizer CreateLocalizer(RuntimeAddin addin, NodeElement element)
    {
        foreach (NodeElement nloc in element.ChildNodes)
        {
            if (nloc.NodeName != "Locale")
                throw new InvalidOperationException(
                    "Invalid element found: '" + nloc.NodeName + "'. Expected: 'Locale'");
            var ln = nloc.GetAttribute("id");
            if (ln.Length == 0)
                throw new InvalidOperationException("Locale id not specified");
            ln = ln.Replace('_', '-');
            var messages = new Hashtable();
            foreach (NodeElement nmsg in nloc.ChildNodes)
            {
                if (nmsg.NodeName != "Msg")
                    throw new InvalidOperationException("Locale '" + ln + "': Invalid element found: '" +
                                                        nmsg.NodeName + "'. Expected: 'Msg'");
                var id = nmsg.GetAttribute("id");
                if (id.Length == 0)
                    throw new InvalidOperationException("Locale '" + ln + "': Message id not specified");
                messages[id] = nmsg.GetAttribute("str");
            }

            locales[ln] = messages;
        }

        return this;
    }
}