//
// Author:
//   Jb Evain (jbevain@gmail.com)
//
// Copyright (c) 2008 - 2015 Jb Evain
// Copyright (c) 2008 - 2011 Novell, Inc.
//
// Licensed under the MIT/X11 license.
//

using System;
using System.IO;
using Mono.Cecil.Cil;
using Mono.Cecil.Metadata;
using RVA = System.UInt32;

namespace Mono.Cecil.PE;

internal sealed class Image : IDisposable
{
    private readonly int[] coded_index_sizes = new int [14];

    private readonly Func<Table, int> counter;
    public TargetArchitecture Architecture;
    public ModuleAttributes Attributes;
    public BlobHeap BlobHeap;
    public uint Characteristics;
    public DataDirectory Debug;

    public ImageDebugHeader DebugHeader;
    public ModuleCharacteristics DllCharacteristics;

    public uint EntryPointToken;
    public string FileName;
    public GuidHeap GuidHeap;

    public ModuleKind Kind;
    public ushort LinkerVersion;

    public Section MetadataSection;
    public PdbHeap PdbHeap;
    public DataDirectory Resources;
    public string RuntimeVersion;

    public Section[] Sections;

    public Disposable<Stream> Stream;

    public StringHeap StringHeap;
    public DataDirectory StrongName;
    public ushort SubSystemMajor;
    public ushort SubSystemMinor;
    public TableHeap TableHeap;
    public uint Timestamp;
    public UserStringHeap UserStringHeap;

    public DataDirectory Win32Resources;

    public Image()
    {
        counter = GetTableLength;
    }

    public void Dispose()
    {
        Stream.Dispose();
    }

    public bool HasTable(Table table)
    {
        return GetTableLength(table) > 0;
    }

    public int GetTableLength(Table table)
    {
        return (int)TableHeap[table].Length;
    }

    public int GetTableIndexSize(Table table)
    {
        return GetTableLength(table) < 65536 ? 2 : 4;
    }

    public int GetCodedIndexSize(CodedIndex coded_index)
    {
        var index = (int)coded_index;
        var size = coded_index_sizes[index];
        if (size != 0)
            return size;

        return coded_index_sizes[index] = coded_index.GetSize(counter);
    }

    public uint ResolveVirtualAddress(uint rva)
    {
        var section = GetSectionAtVirtualAddress(rva);
        if (section == null)
            throw new ArgumentOutOfRangeException();

        return ResolveVirtualAddressInSection(rva, section);
    }

    public uint ResolveVirtualAddressInSection(uint rva, Section section)
    {
        return rva + section.PointerToRawData - section.VirtualAddress;
    }

    public Section GetSection(string name)
    {
        var sections = Sections;
        for (var i = 0; i < sections.Length; i++)
        {
            var section = sections[i];
            if (section.Name == name)
                return section;
        }

        return null;
    }

    public Section GetSectionAtVirtualAddress(uint rva)
    {
        var sections = Sections;
        for (var i = 0; i < sections.Length; i++)
        {
            var section = sections[i];
            if (rva >= section.VirtualAddress && rva < section.VirtualAddress + section.SizeOfRawData)
                return section;
        }

        return null;
    }

    private BinaryStreamReader GetReaderAt(uint rva)
    {
        var section = GetSectionAtVirtualAddress(rva);
        if (section == null)
            return null;

        var reader = new BinaryStreamReader(Stream.value);
        reader.MoveTo(ResolveVirtualAddressInSection(rva, section));
        return reader;
    }

    public TRet GetReaderAt<TItem, TRet>(uint rva, TItem item, Func<TItem, BinaryStreamReader, TRet> read)
        where TRet : class
    {
        var position = Stream.value.Position;
        try
        {
            var reader = GetReaderAt(rva);
            if (reader == null)
                return null;

            return read(item, reader);
        }
        finally
        {
            Stream.value.Position = position;
        }
    }

    public bool HasDebugTables()
    {
        return HasTable(Table.Document)
               || HasTable(Table.MethodDebugInformation)
               || HasTable(Table.LocalScope)
               || HasTable(Table.LocalVariable)
               || HasTable(Table.LocalConstant)
               || HasTable(Table.StateMachineMethod)
               || HasTable(Table.CustomDebugInformation);
    }
}