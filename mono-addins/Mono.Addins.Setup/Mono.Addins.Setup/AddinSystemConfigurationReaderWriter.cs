// It is automatically generated

using System;
using System.Globalization;
using System.Reflection;
using System.Xml;
using System.Xml.Serialization;

namespace Mono.Addins.Setup;

internal class AddinSystemConfigurationReader : XmlSerializationReader
{
    private static readonly MethodInfo fromBinHexStringMethod = typeof(XmlConvert).GetMethod("FromBinHexString",
        BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(string) }, null);

    private static byte[] FromBinHexString(string input)
    {
        return input == null ? null : (byte[])fromBinHexStringMethod.Invoke(null, new object[] { input });
    }

    public object ReadRoot_AddinSystemConfiguration()
    {
        Reader.MoveToContent();
        if (Reader.LocalName != "AddinSystemConfiguration" || Reader.NamespaceURI != "")
            throw CreateUnknownNodeException();
        return ReadObject_AddinSystemConfiguration(true, true);
    }

    public AddinSystemConfiguration ReadObject_AddinSystemConfiguration(bool isNullable, bool checkType)
    {
        AddinSystemConfiguration ob = null;
        if (isNullable && ReadNull()) return null;

        if (checkType)
        {
            var t = GetXsiType();
            if (t == null)
            {
            }
            else if (t.Name != "AddinSystemConfiguration" || t.Namespace != "")
            {
                throw CreateUnknownTypeException(t);
            }
        }

        ob = (AddinSystemConfiguration)Activator.CreateInstance(typeof(AddinSystemConfiguration), true);

        Reader.MoveToElement();

        while (Reader.MoveToNextAttribute())
            if (IsXmlnsAttribute(Reader.Name))
            {
            }
            else
            {
                UnknownNode(ob);
            }

        Reader.MoveToElement();
        Reader.MoveToElement();
        if (Reader.IsEmptyElement)
        {
            Reader.Skip();
            return ob;
        }

        Reader.ReadStartElement();
        Reader.MoveToContent();

        bool b0 = false, b1 = false, b2 = false, b3 = false;

        while (Reader.NodeType != XmlNodeType.EndElement)
        {
            if (Reader.NodeType == XmlNodeType.Element)
            {
                if (Reader.LocalName == "AddinPaths" && Reader.NamespaceURI == "" && !b3)
                {
                    if (ob.AddinPaths == null)
                        throw CreateReadOnlyCollectionException("System.Collections.Specialized.StringCollection");
                    if (Reader.IsEmptyElement)
                    {
                        Reader.Skip();
                    }
                    else
                    {
                        var n4 = 0;
                        Reader.ReadStartElement();
                        Reader.MoveToContent();

                        while (Reader.NodeType != XmlNodeType.EndElement)
                        {
                            if (Reader.NodeType == XmlNodeType.Element)
                            {
                                if (Reader.LocalName == "Addin" && Reader.NamespaceURI == "")
                                {
                                    var s5 = Reader.ReadElementString();
                                    if (ob.AddinPaths == null)
                                        throw CreateReadOnlyCollectionException(
                                            "System.Collections.Specialized.StringCollection");
                                    ob.AddinPaths.Add(s5);
                                    n4++;
                                }
                                else
                                {
                                    UnknownNode(null);
                                }
                            }
                            else
                            {
                                UnknownNode(null);
                            }

                            Reader.MoveToContent();
                        }

                        ReadEndElement();
                    }

                    b3 = true;
                }
                else if (Reader.LocalName == "RepositoryIdCount" && Reader.NamespaceURI == "" && !b1)
                {
                    b1 = true;
                    var s6 = Reader.ReadElementString();
                    ob.RepositoryIdCount = int.Parse(s6, CultureInfo.InvariantCulture);
                }
                else if (Reader.LocalName == "DisabledAddins" && Reader.NamespaceURI == "" && !b2)
                {
                    if (ob.DisabledAddins == null)
                        throw CreateReadOnlyCollectionException("System.Collections.Specialized.StringCollection");
                    if (Reader.IsEmptyElement)
                    {
                        Reader.Skip();
                    }
                    else
                    {
                        var n7 = 0;
                        Reader.ReadStartElement();
                        Reader.MoveToContent();

                        while (Reader.NodeType != XmlNodeType.EndElement)
                        {
                            if (Reader.NodeType == XmlNodeType.Element)
                            {
                                if (Reader.LocalName == "Addin" && Reader.NamespaceURI == "")
                                {
                                    var s8 = Reader.ReadElementString();
                                    if (ob.DisabledAddins == null)
                                        throw CreateReadOnlyCollectionException(
                                            "System.Collections.Specialized.StringCollection");
                                    ob.DisabledAddins.Add(s8);
                                    n7++;
                                }
                                else
                                {
                                    UnknownNode(null);
                                }
                            }
                            else
                            {
                                UnknownNode(null);
                            }

                            Reader.MoveToContent();
                        }

                        ReadEndElement();
                    }

                    b2 = true;
                }
                else if (Reader.LocalName == "Repositories" && Reader.NamespaceURI == "" && !b0)
                {
                    if (ob.Repositories == null)
                        throw CreateReadOnlyCollectionException("System.Collections.ArrayList");
                    if (Reader.IsEmptyElement)
                    {
                        Reader.Skip();
                    }
                    else
                    {
                        var n9 = 0;
                        Reader.ReadStartElement();
                        Reader.MoveToContent();

                        while (Reader.NodeType != XmlNodeType.EndElement)
                        {
                            if (Reader.NodeType == XmlNodeType.Element)
                            {
                                if (Reader.LocalName == "Repository" && Reader.NamespaceURI == "")
                                {
                                    if (ob.Repositories == null)
                                        throw CreateReadOnlyCollectionException("System.Collections.ArrayList");
                                    ob.Repositories.Add(ReadObject_RepositoryRecord(false, true));
                                    n9++;
                                }
                                else
                                {
                                    UnknownNode(null);
                                }
                            }
                            else
                            {
                                UnknownNode(null);
                            }

                            Reader.MoveToContent();
                        }

                        ReadEndElement();
                    }

                    b0 = true;
                }
                else
                {
                    UnknownNode(ob);
                }
            }
            else
            {
                UnknownNode(ob);
            }

            Reader.MoveToContent();
        }

        ReadEndElement();

        return ob;
    }

    public RepositoryRecord ReadObject_RepositoryRecord(bool isNullable, bool checkType)
    {
        RepositoryRecord ob = null;
        if (isNullable && ReadNull()) return null;

        if (checkType)
        {
            var t = GetXsiType();
            if (t == null)
            {
            }
            else if (t.Name != "RepositoryRecord" || t.Namespace != "")
            {
                throw CreateUnknownTypeException(t);
            }
        }

        ob = (RepositoryRecord)Activator.CreateInstance(typeof(RepositoryRecord), true);

        Reader.MoveToElement();

        while (Reader.MoveToNextAttribute())
            if (Reader.LocalName == "id" && Reader.NamespaceURI == "")
            {
                ob.Id = Reader.Value;
            }
            else if (IsXmlnsAttribute(Reader.Name))
            {
            }
            else
            {
                UnknownNode(ob);
            }

        Reader.MoveToElement();
        Reader.MoveToElement();
        if (Reader.IsEmptyElement)
        {
            Reader.Skip();
            return ob;
        }

        Reader.ReadStartElement();
        Reader.MoveToContent();

        bool b10 = false, b11 = false, b12 = false, b13 = false, b14 = false, b15 = false, b16 = false;

        while (Reader.NodeType != XmlNodeType.EndElement)
        {
            if (Reader.NodeType == XmlNodeType.Element)
            {
                if (Reader.LocalName == "File" && Reader.NamespaceURI == "" && !b11)
                {
                    b11 = true;
                    var s16 = Reader.ReadElementString();
                    ob.File = s16;
                }
                else if (Reader.LocalName == "Enabled" && Reader.NamespaceURI == "" && !b15)
                {
                    b15 = true;
                    var s17 = Reader.ReadElementString();
                    ob.Enabled = XmlConvert.ToBoolean(s17);
                }
                else if (Reader.LocalName == "IsReference" && Reader.NamespaceURI == "" && !b10)
                {
                    b10 = true;
                    var s18 = Reader.ReadElementString();
                    ob.IsReference = XmlConvert.ToBoolean(s18);
                }
                else if (Reader.LocalName == "Name" && Reader.NamespaceURI == "" && !b13)
                {
                    b13 = true;
                    var s19 = Reader.ReadElementString();
                    ob.Name = s19;
                }
                else if (Reader.LocalName == "Url" && Reader.NamespaceURI == "" && !b12)
                {
                    b12 = true;
                    var s20 = Reader.ReadElementString();
                    ob.Url = s20;
                }
                else if (Reader.LocalName == "ProviderId" && Reader.NamespaceURI == "" && !b16)
                {
                    b16 = true;
                    ob.ProviderId = Reader.ReadElementString();
                }
                else if (Reader.LocalName == "LastModified" && Reader.NamespaceURI == "" && !b14)
                {
                    b14 = true;
                    var s21 = Reader.ReadElementString();
                    ob.LastModified = XmlConvert.ToDateTime(s21, XmlDateTimeSerializationMode.RoundtripKind);
                }
                else
                {
                    UnknownNode(ob);
                }
            }
            else
            {
                UnknownNode(ob);
            }

            Reader.MoveToContent();
        }

        ReadEndElement();

        return ob;
    }

    protected override void InitCallbacks()
    {
    }

    protected override void InitIDs()
    {
    }
}

internal class AddinSystemConfigurationWriter : XmlSerializationWriter
{
    private const string xmlNamespace = "http://www.w3.org/2000/xmlns/";

    private static readonly MethodInfo toBinHexStringMethod = typeof(XmlConvert).GetMethod("ToBinHexString",
        BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(byte[]) }, null);

    private static string ToBinHexString(byte[] input)
    {
        return input == null ? null : (string)toBinHexStringMethod.Invoke(null, new object[] { input });
    }

    public void WriteRoot_AddinSystemConfiguration(object o)
    {
        WriteStartDocument();
        var ob = (AddinSystemConfiguration)o;
        TopLevelElement();
        WriteObject_AddinSystemConfiguration(ob, "AddinSystemConfiguration", "", true, false, true);
    }

    private void WriteObject_AddinSystemConfiguration(AddinSystemConfiguration ob, string element, string namesp,
        bool isNullable, bool needType, bool writeWrappingElem)
    {
        if (ob == null)
        {
            if (isNullable)
                WriteNullTagLiteral(element, namesp);
            return;
        }

        var type = ob.GetType();
        if (type == typeof(AddinSystemConfiguration))
        {
        }
        else
        {
            throw CreateUnknownTypeException(ob);
        }

        if (writeWrappingElem) WriteStartElement(element, namesp, ob);

        if (needType) WriteXsiType("AddinSystemConfiguration", "");

        if (ob.Repositories != null)
        {
            WriteStartElement("Repositories", "", ob.Repositories);
            for (var n22 = 0; n22 < ob.Repositories.Count; n22++)
                WriteObject_RepositoryRecord((RepositoryRecord)ob.Repositories[n22], "Repository", "", false, false,
                    true);
            WriteEndElement(ob.Repositories);
        }

        WriteElementString("RepositoryIdCount", "", ob.RepositoryIdCount.ToString(CultureInfo.InvariantCulture));
        if (ob.DisabledAddins != null)
        {
            WriteStartElement("DisabledAddins", "", ob.DisabledAddins);
            for (var n23 = 0; n23 < ob.DisabledAddins.Count; n23++)
                WriteElementString("Addin", "", ob.DisabledAddins[n23]);
            WriteEndElement(ob.DisabledAddins);
        }

        if (ob.AddinPaths != null)
        {
            WriteStartElement("AddinPaths", "", ob.AddinPaths);
            for (var n24 = 0; n24 < ob.AddinPaths.Count; n24++) WriteElementString("Addin", "", ob.AddinPaths[n24]);
            WriteEndElement(ob.AddinPaths);
        }

        if (writeWrappingElem) WriteEndElement(ob);
    }

    private void WriteObject_RepositoryRecord(RepositoryRecord ob, string element, string namesp, bool isNullable,
        bool needType, bool writeWrappingElem)
    {
        if (ob == null)
        {
            if (isNullable)
                WriteNullTagLiteral(element, namesp);
            return;
        }

        var type = ob.GetType();
        if (type == typeof(RepositoryRecord))
        {
        }
        else
        {
            throw CreateUnknownTypeException(ob);
        }

        if (writeWrappingElem) WriteStartElement(element, namesp, ob);

        if (needType) WriteXsiType("RepositoryRecord", "");

        WriteAttribute("id", "", ob.Id);

        WriteElementString("IsReference", "", ob.IsReference ? "true" : "false");
        WriteElementString("File", "", ob.File);
        WriteElementString("Url", "", ob.Url);
        WriteElementString("ProviderId", "", ob.ProviderId);
        WriteElementString("Name", "", ob.Name);
        WriteElementString("LastModified", "",
            XmlConvert.ToString(ob.LastModified, XmlDateTimeSerializationMode.RoundtripKind));
        if (ob.Enabled != true) WriteElementString("Enabled", "", ob.Enabled ? "true" : "false");
        if (writeWrappingElem) WriteEndElement(ob);
    }

    protected override void InitCallbacks()
    {
    }
}