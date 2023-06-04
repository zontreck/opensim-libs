//
// Util.cs
//
// Author:
//   Lluis Sanchez Gual
//
// Copyright (C) 2007 Novell, Inc (http://www.novell.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//


using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Mono.Addins.Description;

namespace Mono.Addins.Database;

internal class Util
{
    private static int isMono;
    private static string monoVersion;

    private static readonly char[] separators =
        { Path.DirectorySeparatorChar, Path.VolumeSeparatorChar, Path.AltDirectorySeparatorChar };

    public static bool IsWindows => Path.DirectorySeparatorChar == '\\';

    public static bool IsMono
    {
        get
        {
            if (isMono == 0)
                isMono = Type.GetType("Mono.Runtime") != null ? 1 : -1;
            return isMono == 1;
        }
    }

    public static string MonoVersion
    {
        get
        {
            if (monoVersion == null)
            {
                if (!IsMono)
                    throw new InvalidOperationException();
                var mi = Type.GetType("Mono.Runtime")
                    .GetMethod("GetDisplayName", BindingFlags.NonPublic | BindingFlags.Static);
                if (mi != null)
                    monoVersion = (string)mi.Invoke(null, null);
                else
                    monoVersion = string.Empty;
            }

            return monoVersion;
        }
    }

    public static bool TryParseTypeName(string assemblyQualifiedName, out string typeName, out string assemblyName)
    {
        var bracketCount = 0;
        for (var n = 0; n < assemblyQualifiedName.Length; n++)
        {
            var c = assemblyQualifiedName[n];
            if (c == ',')
            {
                if (bracketCount == 0)
                {
                    typeName = assemblyQualifiedName.Substring(0, n).Trim();
                    try
                    {
                        assemblyName = new AssemblyName(assemblyQualifiedName.Substring(n + 1)).Name;
                        return typeName.Length > 0;
                    }
                    catch
                    {
                        typeName = null;
                        assemblyName = null;
                        return false;
                    }
                }
            }
            else if (c == '[' || c == '<' || c == '(')
            {
                bracketCount++;
            }
            else if (c == ']' || c == '>' || c == ')')
            {
                bracketCount--;
            }
        }

        typeName = assemblyQualifiedName;
        assemblyName = null;
        return true;
    }

    public static void CheckWrittableFloder(string path)
    {
        string testFile = null;
        var n = 0;
        var random = new Random();
        do
        {
            testFile = Path.Combine(path, random.Next().ToString());
            n++;
        } while (File.Exists(testFile) && n < 100);

        if (n == 100)
            throw new InvalidOperationException("Could not create file in directory: " + path);

        var w = new StreamWriter(testFile);
        w.Close();
        File.Delete(testFile);
    }

    public static void AddDependencies(AddinDescription desc, AddinScanResult scanResult)
    {
        // Not implemented in AddinScanResult to avoid making AddinDescription remotable
        foreach (ModuleDescription mod in desc.AllModules)
        foreach (Dependency dep in mod.Dependencies)
        {
            var adep = dep as AddinDependency;
            if (adep == null) continue;
            var depid = Addin.GetFullId(desc.Namespace, adep.AddinId, adep.Version);
            scanResult.AddAddinToUpdateRelations(depid);
        }
    }

    public static Assembly LoadAssemblyForReflection(string fileName)
    {
/*			if (!gotLoadMethod) {
				reflectionOnlyLoadFrom = typeof(Assembly).GetMethod ("ReflectionOnlyLoadFrom");
				gotLoadMethod = true;
				LoadAssemblyForReflection (typeof(Util).Assembly.Location);
			}
			
			if (reflectionOnlyLoadFrom != null)
				return (Assembly) reflectionOnlyLoadFrom.Invoke (null, new string [] { fileName });
			else
*/
        return Assembly.LoadFile(fileName);
    }

    public static string NormalizePath(string path)
    {
        if (path == null)
            return null;
        if (path.Length > 2 && path[0] == '[')
        {
            var i = path.IndexOf(']', 1);
            if (i != -1)
                try
                {
                    var fname = path.Substring(1, i - 1);
                    var sf = (Environment.SpecialFolder)Enum.Parse(typeof(Environment.SpecialFolder), fname, true);
                    path = Environment.GetFolderPath(sf) + path.Substring(i + 1);
                }
                catch
                {
                    // Ignore
                }
        }

        if (IsWindows)
            return path.Replace('/', '\\');
        return path.Replace('\\', '/');
    }

    // A private hash calculation method is used to be able to get consistent
    // results across different .NET versions and implementations.
    public static int GetStringHashCode(string s)
    {
        var h = 0;
        var n = 0;
        for (; n < s.Length - 1; n += 2)
        {
            h = unchecked((h << 5) - h + s[n]);
            h = unchecked((h << 5) - h + s[n + 1]);
        }

        if (n < s.Length)
            h = unchecked((h << 5) - h + s[n]);
        return h;
    }

    public static string GetGacPath(string fullName)
    {
        var parts = fullName.Split(',');
        if (parts.Length != 4) return null;
        var name = parts[0].Trim();

        var i = parts[1].IndexOf('=');
        var version = i != -1 ? parts[1].Substring(i + 1).Trim() : parts[1].Trim();

        i = parts[2].IndexOf('=');
        var culture = i != -1 ? parts[2].Substring(i + 1).Trim() : parts[2].Trim();
        if (culture == "neutral") culture = "";

        i = parts[3].IndexOf('=');
        var token = i != -1 ? parts[3].Substring(i + 1).Trim() : parts[3].Trim();

        var versionDirName = version + "_" + culture + "_" + token;

        if (IsMono)
        {
            var gacDir = typeof(Uri).Assembly.Location;
            gacDir = Path.GetDirectoryName(gacDir);
            gacDir = Path.GetDirectoryName(gacDir);
            gacDir = Path.GetDirectoryName(gacDir);
            var dir = Path.Combine(gacDir, name);
            return Path.Combine(dir, versionDirName);
        }
        // .NET 4.0 introduces a new GAC directory structure and location.
        // Assembly version directory names are now prefixed with the CLR version
        // Since there can be different assembly versions for different target CLR runtimes,
        // we now look for the best match, that is, the assembly with the higher CLR version

        var currentVersion = new Version(Environment.Version.Major, Environment.Version.Minor);

        foreach (var gacDir in GetDotNetGacDirectories())
        {
            var asmDir = Path.Combine(gacDir, name);
            if (!Directory.Exists(asmDir))
                continue;
            var bestVersion = new Version(0, 0);
            string bestDir = null;
            foreach (var dir in Directory.GetDirectories(asmDir, "v*_" + versionDirName))
            {
                var dirName = Path.GetFileName(dir);
                i = dirName.IndexOf('_');
                Version av;
                if (Version.TryParse(dirName.Substring(1, i - 1), out av))
                {
                    if (av == currentVersion)
                    {
                        return dir;
                    }

                    if (av < currentVersion && av > bestVersion)
                    {
                        bestDir = dir;
                        bestVersion = av;
                    }
                }
            }

            if (bestDir != null)
                return bestDir;
        }

        // Look in the old GAC. There are no CLR prefixes here

        foreach (var gacDir in GetLegacyDotNetGacDirectories())
        {
            var asmDir = Path.Combine(gacDir, name);
            asmDir = Path.Combine(asmDir, versionDirName);
            if (Directory.Exists(asmDir))
                return asmDir;
        }

        return null;
    }

    private static IEnumerable<string> GetLegacyDotNetGacDirectories()
    {
        var winDir = Path.GetFullPath(Environment.SystemDirectory + "\\..");

        var gacDir = winDir + "\\assembly\\GAC";
        if (Directory.Exists(gacDir))
            yield return gacDir;
        if (Directory.Exists(gacDir + "_32"))
            yield return gacDir + "_32";
        if (Directory.Exists(gacDir + "_64"))
            yield return gacDir + "_64";
        if (Directory.Exists(gacDir + "_MSIL"))
            yield return gacDir + "_MSIL";
    }

    private static IEnumerable<string> GetDotNetGacDirectories()
    {
        var winDir = Path.GetFullPath(Environment.SystemDirectory + "\\..");

        var gacDir = winDir + "\\Microsoft.NET\\assembly\\GAC";
        if (Directory.Exists(gacDir))
            yield return gacDir;
        if (Directory.Exists(gacDir + "_32"))
            yield return gacDir + "_32";
        if (Directory.Exists(gacDir + "_64"))
            yield return gacDir + "_64";
        if (Directory.Exists(gacDir + "_MSIL"))
            yield return gacDir + "_MSIL";
    }

    internal static bool IsManagedAssembly(string filePath)
    {
        try
        {
            using (Stream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read,
                       FileShare.ReadWrite | FileShare.Delete))
            using (var binaryReader = new BinaryReader(fileStream))
            {
                if (fileStream.Length < 64) return false;

                // PE Header starts @ 0x3C (60). Its a 4 byte header.
                fileStream.Position = 0x3C;
                var peHeaderPointer = binaryReader.ReadUInt32();
                if (peHeaderPointer == 0) peHeaderPointer = 0x80;

                // Ensure there is at least enough room for the following structures:
                //     24 byte PE Signature & Header
                //     28 byte Standard Fields         (24 bytes for PE32+)
                //     68 byte NT Fields               (88 bytes for PE32+)
                // >= 128 byte Data Dictionary Table
                if (peHeaderPointer > fileStream.Length - 256) return false;

                // Check the PE signature.  Should equal 'PE\0\0'.
                fileStream.Position = peHeaderPointer;
                var peHeaderSignature = binaryReader.ReadUInt32();
                if (peHeaderSignature != 0x00004550) return false;

                // skip over the PEHeader fields
                fileStream.Position += 20;

                const ushort PE32 = 0x10b;
                const ushort PE32Plus = 0x20b;

                // Read PE magic number from Standard Fields to determine format.
                var peFormat = binaryReader.ReadUInt16();
                if (peFormat != PE32 && peFormat != PE32Plus) return false;

                // Read the 15th Data Dictionary RVA field which contains the CLI header RVA.
                // When this is non-zero then the file contains CLI data otherwise not.
                var dataDictionaryStart = (ushort)(peHeaderPointer + (peFormat == PE32 ? 232 : 248));
                fileStream.Position = dataDictionaryStart;

                var cliHeaderRva = binaryReader.ReadUInt32();
                if (cliHeaderRva == 0) return false;

                return true;
            }
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static string AbsoluteToRelativePath(string baseDirectoryPath, string absPath)
    {
        if (!Path.IsPathRooted(absPath))
            return absPath;
        absPath = Path.GetFullPath(absPath);
        baseDirectoryPath = Path.GetFullPath(baseDirectoryPath.TrimEnd(Path.DirectorySeparatorChar));
        var bPath = baseDirectoryPath.Split(separators);
        var aPath = absPath.Split(separators);
        var indx = 0;
        for (; indx < Math.Min(bPath.Length, aPath.Length); indx++)
            if (!bPath[indx].Equals(aPath[indx]))
                break;
        if (indx == 0)
            return absPath;
        var result = new StringBuilder();
        for (var i = indx; i < bPath.Length; i++)
        {
            result.Append("..");
            if (i + 1 < bPath.Length || aPath.Length - indx > 0)
                result.Append(Path.DirectorySeparatorChar);
        }

        result.Append(string.Join(Path.DirectorySeparatorChar.ToString(), aPath, indx, aPath.Length - indx));
        if (result.Length == 0)
            return ".";
        return result.ToString();
    }

    public static string GetMD5(string file)
    {
        using (var md5 = MD5.Create())
        {
            using (var stream = File.OpenRead(file))
            {
                var bytes = md5.ComputeHash(stream);
                var sb = new StringBuilder();
                foreach (var b in bytes)
                    sb.Append(b.ToString("x"));
                return sb.ToString();
            }
        }
    }
}