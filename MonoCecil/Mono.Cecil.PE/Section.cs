//
// Author:
//   Jb Evain (jbevain@gmail.com)
//
// Copyright (c) 2008 - 2015 Jb Evain
// Copyright (c) 2008 - 2011 Novell, Inc.
//
// Licensed under the MIT/X11 license.
//

using RVA = System.UInt32;

namespace Mono.Cecil.PE;

internal sealed class Section
{
    public string Name;
    public uint PointerToRawData;
    public uint SizeOfRawData;
    public uint VirtualAddress;
    public uint VirtualSize;
}