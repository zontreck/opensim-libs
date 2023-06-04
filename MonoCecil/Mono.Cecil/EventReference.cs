//
// Author:
//   Jb Evain (jbevain@gmail.com)
//
// Copyright (c) 2008 - 2015 Jb Evain
// Copyright (c) 2008 - 2011 Novell, Inc.
//
// Licensed under the MIT/X11 license.
//

namespace Mono.Cecil;

public abstract class EventReference : MemberReference
{
    protected EventReference(string name, TypeReference eventType)
        : base(name)
    {
        Mixin.CheckType(eventType, Mixin.Argument.eventType);
        EventType = eventType;
    }

    public TypeReference EventType { get; set; }

    public override string FullName => EventType.FullName + " " + MemberFullName();

    protected override IMemberDefinition ResolveDefinition()
    {
        return Resolve();
    }

    public new abstract EventDefinition Resolve();
}