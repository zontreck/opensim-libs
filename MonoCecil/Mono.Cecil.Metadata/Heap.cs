//
// Author:
//   Jb Evain (jbevain@gmail.com)
//
// Copyright (c) 2008 - 2015 Jb Evain
// Copyright (c) 2008 - 2011 Novell, Inc.
//
// Licensed under the MIT/X11 license.
//

namespace Mono.Cecil.Metadata;

internal abstract class Heap
{
    internal readonly byte[] data;

    public int IndexSize;

    protected Heap(byte[] data)
    {
        this.data = data;
    }
}