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
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Threading;
using Mono.Cecil.Cil;
using Mono.Cecil.PE;
using Mono.Collections.Generic;
using SR = System.Reflection;

namespace Mono.Cecil.Cil
{
    [StructLayout(LayoutKind.Sequential)]
    public struct ImageDebugDirectory
    {
        public const int Size = 28;

        public int Characteristics;
        public int TimeDateStamp;
        public short MajorVersion;
        public short MinorVersion;
        public ImageDebugType Type;
        public int SizeOfData;
        public int AddressOfRawData;
        public int PointerToRawData;
    }

    public enum ImageDebugType
    {
        CodeView = 2,
        Deterministic = 16,
        EmbeddedPortablePdb = 17,
        PdbChecksum = 19
    }

    public sealed class ImageDebugHeader
    {
        public ImageDebugHeader(ImageDebugHeaderEntry[] entries)
        {
            Entries = entries ?? Empty<ImageDebugHeaderEntry>.Array;
        }

        public ImageDebugHeader()
            : this(Empty<ImageDebugHeaderEntry>.Array)
        {
        }

        public ImageDebugHeader(ImageDebugHeaderEntry entry)
            : this(new[] { entry })
        {
        }

        public bool HasEntries => !Entries.IsNullOrEmpty();

        public ImageDebugHeaderEntry[] Entries { get; }
    }

    public sealed class ImageDebugHeaderEntry
    {
        public ImageDebugHeaderEntry(ImageDebugDirectory directory, byte[] data)
        {
            Directory = directory;
            Data = data ?? Empty<byte>.Array;
        }

        public ImageDebugDirectory Directory { get; internal set; }

        public byte[] Data { get; }
    }

    public sealed class ScopeDebugInformation : DebugInformation
    {
        internal Collection<ConstantDebugInformation> constants;
        internal InstructionOffset end;
        internal ImportDebugInformation import;
        internal Collection<ScopeDebugInformation> scopes;

        internal InstructionOffset start;
        internal Collection<VariableDebugInformation> variables;

        internal ScopeDebugInformation()
        {
            token = new MetadataToken(TokenType.LocalScope);
        }

        public ScopeDebugInformation(Instruction start, Instruction end)
            : this()
        {
            if (start == null)
                throw new ArgumentNullException("start");

            this.start = new InstructionOffset(start);

            if (end != null)
                this.end = new InstructionOffset(end);
        }

        public InstructionOffset Start
        {
            get => start;
            set => start = value;
        }

        public InstructionOffset End
        {
            get => end;
            set => end = value;
        }

        public ImportDebugInformation Import
        {
            get => import;
            set => import = value;
        }

        public bool HasScopes => !scopes.IsNullOrEmpty();

        public Collection<ScopeDebugInformation> Scopes
        {
            get
            {
                if (scopes == null)
                    Interlocked.CompareExchange(ref scopes, new Collection<ScopeDebugInformation>(), null);

                return scopes;
            }
        }

        public bool HasVariables => !variables.IsNullOrEmpty();

        public Collection<VariableDebugInformation> Variables
        {
            get
            {
                if (variables == null)
                    Interlocked.CompareExchange(ref variables, new Collection<VariableDebugInformation>(), null);

                return variables;
            }
        }

        public bool HasConstants => !constants.IsNullOrEmpty();

        public Collection<ConstantDebugInformation> Constants
        {
            get
            {
                if (constants == null)
                    Interlocked.CompareExchange(ref constants, new Collection<ConstantDebugInformation>(), null);

                return constants;
            }
        }

        public bool TryGetName(VariableDefinition variable, out string name)
        {
            name = null;
            if (variables == null || variables.Count == 0)
                return false;

            for (var i = 0; i < variables.Count; i++)
                if (variables[i].Index == variable.Index)
                {
                    name = variables[i].Name;
                    return true;
                }

            return false;
        }
    }

    public struct InstructionOffset
    {
        private readonly int? offset;

        public int Offset
        {
            get
            {
                if (ResolvedInstruction != null)
                    return ResolvedInstruction.Offset;
                if (offset.HasValue)
                    return offset.Value;

                throw new NotSupportedException();
            }
        }

        public bool IsEndOfMethod => ResolvedInstruction == null && !offset.HasValue;

        internal bool IsResolved => ResolvedInstruction != null || !offset.HasValue;

        internal Instruction ResolvedInstruction { get; }

        public InstructionOffset(Instruction instruction)
        {
            if (instruction == null)
                throw new ArgumentNullException("instruction");

            ResolvedInstruction = instruction;
            offset = null;
        }

        public InstructionOffset(int offset)
        {
            ResolvedInstruction = null;
            this.offset = offset;
        }
    }

    [Flags]
    public enum VariableAttributes : ushort
    {
        None = 0,
        DebuggerHidden = 1
    }

    public struct VariableIndex
    {
        private readonly int? index;

        public int Index
        {
            get
            {
                if (ResolvedVariable != null)
                    return ResolvedVariable.Index;
                if (index.HasValue)
                    return index.Value;

                throw new NotSupportedException();
            }
        }

        internal bool IsResolved => ResolvedVariable != null;

        internal VariableDefinition ResolvedVariable { get; }

        public VariableIndex(VariableDefinition variable)
        {
            if (variable == null)
                throw new ArgumentNullException("variable");

            ResolvedVariable = variable;
            index = null;
        }

        public VariableIndex(int index)
        {
            ResolvedVariable = null;
            this.index = index;
        }
    }

    public abstract class DebugInformation : ICustomDebugInformationProvider
    {
        internal Collection<CustomDebugInformation> custom_infos;

        internal MetadataToken token;

        internal DebugInformation()
        {
        }

        public MetadataToken MetadataToken
        {
            get => token;
            set => token = value;
        }

        public bool HasCustomDebugInformations => !custom_infos.IsNullOrEmpty();

        public Collection<CustomDebugInformation> CustomDebugInformations
        {
            get
            {
                if (custom_infos == null)
                    Interlocked.CompareExchange(ref custom_infos, new Collection<CustomDebugInformation>(), null);

                return custom_infos;
            }
        }
    }

    public sealed class VariableDebugInformation : DebugInformation
    {
        private ushort attributes;
        internal VariableIndex index;

        internal VariableDebugInformation(int index, string name)
        {
            if (name == null)
                throw new ArgumentNullException("name");

            this.index = new VariableIndex(index);
            Name = name;
        }

        public VariableDebugInformation(VariableDefinition variable, string name)
        {
            if (variable == null)
                throw new ArgumentNullException("variable");
            if (name == null)
                throw new ArgumentNullException("name");

            index = new VariableIndex(variable);
            Name = name;
            token = new MetadataToken(TokenType.LocalVariable);
        }

        public int Index => index.Index;

        public string Name { get; set; }

        public VariableAttributes Attributes
        {
            get => (VariableAttributes)attributes;
            set => attributes = (ushort)value;
        }

        public bool IsDebuggerHidden
        {
            get => attributes.GetAttributes((ushort)VariableAttributes.DebuggerHidden);
            set => attributes = attributes.SetAttributes((ushort)VariableAttributes.DebuggerHidden, value);
        }
    }

    public sealed class ConstantDebugInformation : DebugInformation
    {
        public ConstantDebugInformation(string name, TypeReference constant_type, object value)
        {
            if (name == null)
                throw new ArgumentNullException("name");

            Name = name;
            ConstantType = constant_type;
            Value = value;
            token = new MetadataToken(TokenType.LocalConstant);
        }

        public string Name { get; set; }

        public TypeReference ConstantType { get; set; }

        public object Value { get; set; }
    }

    public enum ImportTargetKind : byte
    {
        ImportNamespace = 1,
        ImportNamespaceInAssembly = 2,
        ImportType = 3,
        ImportXmlNamespaceWithAlias = 4,
        ImportAlias = 5,
        DefineAssemblyAlias = 6,
        DefineNamespaceAlias = 7,
        DefineNamespaceInAssemblyAlias = 8,
        DefineTypeAlias = 9
    }

    public sealed class ImportTarget
    {
        internal string alias;

        internal ImportTargetKind kind;

        internal string @namespace;
        internal AssemblyNameReference reference;
        internal TypeReference type;

        public ImportTarget(ImportTargetKind kind)
        {
            this.kind = kind;
        }

        public string Namespace
        {
            get => @namespace;
            set => @namespace = value;
        }

        public TypeReference Type
        {
            get => type;
            set => type = value;
        }

        public AssemblyNameReference AssemblyReference
        {
            get => reference;
            set => reference = value;
        }

        public string Alias
        {
            get => alias;
            set => alias = value;
        }

        public ImportTargetKind Kind
        {
            get => kind;
            set => kind = value;
        }
    }

    public sealed class ImportDebugInformation : DebugInformation
    {
        internal ImportDebugInformation parent;
        internal Collection<ImportTarget> targets;

        public ImportDebugInformation()
        {
            token = new MetadataToken(TokenType.ImportScope);
        }

        public bool HasTargets => !targets.IsNullOrEmpty();

        public Collection<ImportTarget> Targets
        {
            get
            {
                if (targets == null)
                    Interlocked.CompareExchange(ref targets, new Collection<ImportTarget>(), null);

                return targets;
            }
        }

        public ImportDebugInformation Parent
        {
            get => parent;
            set => parent = value;
        }
    }

    public interface ICustomDebugInformationProvider : IMetadataTokenProvider
    {
        bool HasCustomDebugInformations { get; }
        Collection<CustomDebugInformation> CustomDebugInformations { get; }
    }

    public enum CustomDebugInformationKind
    {
        Binary,
        StateMachineScope,
        DynamicVariable,
        DefaultNamespace,
        AsyncMethodBody,
        EmbeddedSource,
        SourceLink
    }

    public abstract class CustomDebugInformation : DebugInformation
    {
        internal CustomDebugInformation(Guid identifier)
        {
            Identifier = identifier;
            token = new MetadataToken(TokenType.CustomDebugInformation);
        }

        public Guid Identifier { get; }

        public abstract CustomDebugInformationKind Kind { get; }
    }

    public sealed class BinaryCustomDebugInformation : CustomDebugInformation
    {
        public BinaryCustomDebugInformation(Guid identifier, byte[] data)
            : base(identifier)
        {
            Data = data;
        }

        public byte[] Data { get; set; }

        public override CustomDebugInformationKind Kind => CustomDebugInformationKind.Binary;
    }

    public sealed class AsyncMethodBodyDebugInformation : CustomDebugInformation
    {
        public static Guid KindIdentifier = new("{54FD2AC5-E925-401A-9C2A-F94F171072F8}");

        internal InstructionOffset catch_handler;
        internal Collection<MethodDefinition> resume_methods;
        internal Collection<InstructionOffset> resumes;
        internal Collection<InstructionOffset> yields;

        internal AsyncMethodBodyDebugInformation(int catchHandler)
            : base(KindIdentifier)
        {
            catch_handler = new InstructionOffset(catchHandler);
        }

        public AsyncMethodBodyDebugInformation(Instruction catchHandler)
            : base(KindIdentifier)
        {
            catch_handler = new InstructionOffset(catchHandler);
        }

        public AsyncMethodBodyDebugInformation()
            : base(KindIdentifier)
        {
            catch_handler = new InstructionOffset(-1);
        }

        public InstructionOffset CatchHandler
        {
            get => catch_handler;
            set => catch_handler = value;
        }

        public Collection<InstructionOffset> Yields
        {
            get
            {
                if (yields == null)
                    Interlocked.CompareExchange(ref yields, new Collection<InstructionOffset>(), null);

                return yields;
            }
        }

        public Collection<InstructionOffset> Resumes
        {
            get
            {
                if (resumes == null)
                    Interlocked.CompareExchange(ref resumes, new Collection<InstructionOffset>(), null);

                return resumes;
            }
        }

        public Collection<MethodDefinition> ResumeMethods =>
            resume_methods ?? (resume_methods = new Collection<MethodDefinition>());

        public override CustomDebugInformationKind Kind => CustomDebugInformationKind.AsyncMethodBody;
    }

    public sealed class StateMachineScope
    {
        internal InstructionOffset end;

        internal InstructionOffset start;

        internal StateMachineScope(int start, int end)
        {
            this.start = new InstructionOffset(start);
            this.end = new InstructionOffset(end);
        }

        public StateMachineScope(Instruction start, Instruction end)
        {
            this.start = new InstructionOffset(start);
            this.end = end != null ? new InstructionOffset(end) : new InstructionOffset();
        }

        public InstructionOffset Start
        {
            get => start;
            set => start = value;
        }

        public InstructionOffset End
        {
            get => end;
            set => end = value;
        }
    }

    public sealed class StateMachineScopeDebugInformation : CustomDebugInformation
    {
        public static Guid KindIdentifier = new("{6DA9A61E-F8C7-4874-BE62-68BC5630DF71}");

        internal Collection<StateMachineScope> scopes;

        public StateMachineScopeDebugInformation()
            : base(KindIdentifier)
        {
        }

        public Collection<StateMachineScope> Scopes => scopes ?? (scopes = new Collection<StateMachineScope>());

        public override CustomDebugInformationKind Kind => CustomDebugInformationKind.StateMachineScope;
    }

    public sealed class EmbeddedSourceDebugInformation : CustomDebugInformation
    {
        public static Guid KindIdentifier = new("{0E8A571B-6926-466E-B4AD-8AB04611F5FE}");
        internal bool compress;
        internal byte[] content;
        internal MetadataReader debug_reader;

        internal uint index;
        internal bool resolved;

        internal EmbeddedSourceDebugInformation(uint index, MetadataReader debug_reader)
            : base(KindIdentifier)
        {
            this.index = index;
            this.debug_reader = debug_reader;
        }

        public EmbeddedSourceDebugInformation(byte[] content, bool compress)
            : base(KindIdentifier)
        {
            resolved = true;
            this.content = content;
            this.compress = compress;
        }

        public byte[] Content
        {
            get
            {
                if (!resolved)
                    Resolve();

                return content;
            }
            set
            {
                content = value;
                resolved = true;
            }
        }

        public bool Compress
        {
            get
            {
                if (!resolved)
                    Resolve();

                return compress;
            }
            set
            {
                compress = value;
                resolved = true;
            }
        }

        public override CustomDebugInformationKind Kind => CustomDebugInformationKind.EmbeddedSource;

        internal byte[] ReadRawEmbeddedSourceDebugInformation()
        {
            if (debug_reader == null)
                throw new InvalidOperationException();

            return debug_reader.ReadRawEmbeddedSourceDebugInformation(index);
        }

        private void Resolve()
        {
            if (resolved)
                return;

            if (debug_reader == null)
                throw new InvalidOperationException();

            var row = debug_reader.ReadEmbeddedSourceDebugInformation(index);
            content = row.Col1;
            compress = row.Col2;
            resolved = true;
        }
    }

    public sealed class SourceLinkDebugInformation : CustomDebugInformation
    {
        public static Guid KindIdentifier = new("{CC110556-A091-4D38-9FEC-25AB9A351A6A}");

        internal string content;

        public SourceLinkDebugInformation(string content)
            : base(KindIdentifier)
        {
            this.content = content;
        }

        public string Content
        {
            get => content;
            set => content = value;
        }

        public override CustomDebugInformationKind Kind => CustomDebugInformationKind.SourceLink;
    }

    public sealed class MethodDebugInformation : DebugInformation
    {
        internal int code_size;
        internal MethodDefinition kickoff_method;
        internal MetadataToken local_var_token;

        internal MethodDefinition method;
        internal ScopeDebugInformation scope;
        internal Collection<SequencePoint> sequence_points;

        internal MethodDebugInformation(MethodDefinition method)
        {
            if (method == null)
                throw new ArgumentNullException("method");

            this.method = method;
            token = new MetadataToken(TokenType.MethodDebugInformation, method.MetadataToken.RID);
        }

        public MethodDefinition Method => method;

        public bool HasSequencePoints => !sequence_points.IsNullOrEmpty();

        public Collection<SequencePoint> SequencePoints
        {
            get
            {
                if (sequence_points == null)
                    Interlocked.CompareExchange(ref sequence_points, new Collection<SequencePoint>(), null);

                return sequence_points;
            }
        }

        public ScopeDebugInformation Scope
        {
            get => scope;
            set => scope = value;
        }

        public MethodDefinition StateMachineKickOffMethod
        {
            get => kickoff_method;
            set => kickoff_method = value;
        }

        public SequencePoint GetSequencePoint(Instruction instruction)
        {
            if (!HasSequencePoints)
                return null;

            for (var i = 0; i < sequence_points.Count; i++)
                if (sequence_points[i].Offset == instruction.Offset)
                    return sequence_points[i];

            return null;
        }

        public IDictionary<Instruction, SequencePoint> GetSequencePointMapping()
        {
            var instruction_mapping = new Dictionary<Instruction, SequencePoint>();
            if (!HasSequencePoints || !method.HasBody)
                return instruction_mapping;

            var offset_mapping = new Dictionary<int, SequencePoint>(sequence_points.Count);

            for (var i = 0; i < sequence_points.Count; i++)
                if (!offset_mapping.ContainsKey(sequence_points[i].Offset))
                    offset_mapping.Add(sequence_points[i].Offset, sequence_points[i]);

            var instructions = method.Body.Instructions;

            for (var i = 0; i < instructions.Count; i++)
            {
                SequencePoint sequence_point;
                if (offset_mapping.TryGetValue(instructions[i].Offset, out sequence_point))
                    instruction_mapping.Add(instructions[i], sequence_point);
            }

            return instruction_mapping;
        }

        public IEnumerable<ScopeDebugInformation> GetScopes()
        {
            if (scope == null)
                return Empty<ScopeDebugInformation>.Array;

            return GetScopes(new[] { scope });
        }

        private static IEnumerable<ScopeDebugInformation> GetScopes(IList<ScopeDebugInformation> scopes)
        {
            for (var i = 0; i < scopes.Count; i++)
            {
                var scope = scopes[i];

                yield return scope;

                if (!scope.HasScopes)
                    continue;

                foreach (var sub_scope in GetScopes(scope.Scopes))
                    yield return sub_scope;
            }
        }

        public bool TryGetName(VariableDefinition variable, out string name)
        {
            name = null;

            var has_name = false;
            var unique_name = "";

            foreach (var scope in GetScopes())
            {
                string slot_name;
                if (!scope.TryGetName(variable, out slot_name))
                    continue;

                if (!has_name)
                {
                    has_name = true;
                    unique_name = slot_name;
                    continue;
                }

                if (unique_name != slot_name)
                    return false;
            }

            name = unique_name;
            return has_name;
        }
    }

    public interface ISymbolReader : IDisposable
    {
        ISymbolWriterProvider GetWriterProvider();
        bool ProcessDebugHeader(ImageDebugHeader header);
        MethodDebugInformation Read(MethodDefinition method);
    }

    public interface ISymbolReaderProvider
    {
        ISymbolReader GetSymbolReader(ModuleDefinition module, string fileName);
        ISymbolReader GetSymbolReader(ModuleDefinition module, Stream symbolStream);
    }

#if !NET_CORE
    [Serializable]
#endif
    public sealed class SymbolsNotFoundException : FileNotFoundException
    {
        public SymbolsNotFoundException(string message) : base(message)
        {
        }

#if !NET_CORE
        private SymbolsNotFoundException(
            SerializationInfo info,
            StreamingContext context)
            : base(info, context)
        {
        }
#endif
    }

#if !NET_CORE
    [Serializable]
#endif
    public sealed class SymbolsNotMatchingException : InvalidOperationException
    {
        public SymbolsNotMatchingException(string message) : base(message)
        {
        }

#if !NET_CORE
        private SymbolsNotMatchingException(
            SerializationInfo info,
            StreamingContext context)
            : base(info, context)
        {
        }
#endif
    }

    public class DefaultSymbolReaderProvider : ISymbolReaderProvider
    {
        private readonly bool throw_if_no_symbol;

        public DefaultSymbolReaderProvider()
            : this(true)
        {
        }

        public DefaultSymbolReaderProvider(bool throwIfNoSymbol)
        {
            throw_if_no_symbol = throwIfNoSymbol;
        }

        public ISymbolReader GetSymbolReader(ModuleDefinition module, string fileName)
        {
            if (module.Image.HasDebugTables())
                return null;

            if (module.HasDebugHeader)
            {
                var header = module.GetDebugHeader();
                var entry = header.GetEmbeddedPortablePdbEntry();
                if (entry != null)
                    return new EmbeddedPortablePdbReaderProvider().GetSymbolReader(module, fileName);
            }

            var pdb_file_name = Mixin.GetPdbFileName(fileName);

            if (File.Exists(pdb_file_name))
            {
                if (Mixin.IsPortablePdb(Mixin.GetPdbFileName(fileName)))
                    return new PortablePdbReaderProvider().GetSymbolReader(module, fileName);

                try
                {
                    return SymbolProvider.GetReaderProvider(SymbolKind.NativePdb).GetSymbolReader(module, fileName);
                }
                catch (Exception)
                {
                    // We might not include support for native pdbs.
                }
            }

            var mdb_file_name = Mixin.GetMdbFileName(fileName);
            if (File.Exists(mdb_file_name))
                try
                {
                    return SymbolProvider.GetReaderProvider(SymbolKind.Mdb).GetSymbolReader(module, fileName);
                }
                catch (Exception)
                {
                    // We might not include support for mdbs.
                }

            if (throw_if_no_symbol)
                throw new SymbolsNotFoundException(string.Format("No symbol found for file: {0}", fileName));

            return null;
        }

        public ISymbolReader GetSymbolReader(ModuleDefinition module, Stream symbolStream)
        {
            if (module.Image.HasDebugTables())
                return null;

            if (module.HasDebugHeader)
            {
                var header = module.GetDebugHeader();
                var entry = header.GetEmbeddedPortablePdbEntry();
                if (entry != null)
                    return new EmbeddedPortablePdbReaderProvider().GetSymbolReader(module, "");
            }

            Mixin.CheckStream(symbolStream);
            Mixin.CheckReadSeek(symbolStream);

            var position = symbolStream.Position;

            const int portablePdbHeader = 0x424a5342;

            var reader = new BinaryStreamReader(symbolStream);
            var intHeader = reader.ReadInt32();
            symbolStream.Position = position;

            if (intHeader == portablePdbHeader)
                return new PortablePdbReaderProvider().GetSymbolReader(module, symbolStream);

            const string nativePdbHeader = "Microsoft C/C++ MSF 7.00";

            var bytesHeader = reader.ReadBytes(nativePdbHeader.Length);
            symbolStream.Position = position;
            var isNativePdb = true;

            for (var i = 0; i < bytesHeader.Length; i++)
                if (bytesHeader[i] != (byte)nativePdbHeader[i])
                {
                    isNativePdb = false;
                    break;
                }

            if (isNativePdb)
                try
                {
                    return SymbolProvider.GetReaderProvider(SymbolKind.NativePdb).GetSymbolReader(module, symbolStream);
                }
                catch (Exception)
                {
                    // We might not include support for native pdbs.
                }

            const long mdbHeader = 0x45e82623fd7fa614;

            var longHeader = reader.ReadInt64();
            symbolStream.Position = position;

            if (longHeader == mdbHeader)
                try
                {
                    return SymbolProvider.GetReaderProvider(SymbolKind.Mdb).GetSymbolReader(module, symbolStream);
                }
                catch (Exception)
                {
                    // We might not include support for mdbs.
                }

            if (throw_if_no_symbol)
                throw new SymbolsNotFoundException("No symbols found in stream");

            return null;
        }
    }

    internal enum SymbolKind
    {
        NativePdb,
        PortablePdb,
        EmbeddedPortablePdb,
        Mdb
    }

    internal static class SymbolProvider
    {
        private static SR.AssemblyName GetSymbolAssemblyName(SymbolKind kind)
        {
            if (kind == SymbolKind.PortablePdb)
                throw new ArgumentException();

            var suffix = GetSymbolNamespace(kind);

            var cecil_name = typeof(SymbolProvider).Assembly.GetName();

            var name = new SR.AssemblyName
            {
                Name = cecil_name.Name + "." + suffix,
                Version = cecil_name.Version,
#if NET_CORE
				CultureName = cecil_name.CultureName,
#else
                CultureInfo = cecil_name.CultureInfo,
#endif
            };

            name.SetPublicKeyToken(cecil_name.GetPublicKeyToken());

            return name;
        }

        private static Type GetSymbolType(SymbolKind kind, string fullname)
        {
            var type = Type.GetType(fullname);
            if (type != null)
                return type;

            var assembly_name = GetSymbolAssemblyName(kind);

            type = Type.GetType(fullname + ", " + assembly_name.FullName);
            if (type != null)
                return type;

            try
            {
                var assembly = SR.Assembly.Load(assembly_name);
                if (assembly != null)
                    return assembly.GetType(fullname);
            }
            catch (FileNotFoundException)
            {
            }
            catch (FileLoadException)
            {
            }

            return null;
        }

        public static ISymbolReaderProvider GetReaderProvider(SymbolKind kind)
        {
            if (kind == SymbolKind.PortablePdb)
                return new PortablePdbReaderProvider();
            if (kind == SymbolKind.EmbeddedPortablePdb)
                return new EmbeddedPortablePdbReaderProvider();

            var provider_name = GetSymbolTypeName(kind, "ReaderProvider");
            var type = GetSymbolType(kind, provider_name);
            if (type == null)
                throw new TypeLoadException("Could not find symbol provider type " + provider_name);

            return (ISymbolReaderProvider)Activator.CreateInstance(type);
        }

        private static string GetSymbolTypeName(SymbolKind kind, string name)
        {
            return "Mono.Cecil" + "." + GetSymbolNamespace(kind) + "." + kind + name;
        }

        private static string GetSymbolNamespace(SymbolKind kind)
        {
            if (kind == SymbolKind.PortablePdb || kind == SymbolKind.EmbeddedPortablePdb)
                return "Cil";
            if (kind == SymbolKind.NativePdb)
                return "Pdb";
            if (kind == SymbolKind.Mdb)
                return "Mdb";

            throw new ArgumentException();
        }
    }

    public interface ISymbolWriter : IDisposable
    {
        ISymbolReaderProvider GetReaderProvider();
        ImageDebugHeader GetDebugHeader();
        void Write(MethodDebugInformation info);
        void Write();
    }

    public interface ISymbolWriterProvider
    {
        ISymbolWriter GetSymbolWriter(ModuleDefinition module, string fileName);
        ISymbolWriter GetSymbolWriter(ModuleDefinition module, Stream symbolStream);
    }

    public class DefaultSymbolWriterProvider : ISymbolWriterProvider
    {
        public ISymbolWriter GetSymbolWriter(ModuleDefinition module, string fileName)
        {
            var reader = module.SymbolReader;
            if (reader == null)
                throw new InvalidOperationException();

            if (module.Image != null && module.Image.HasDebugTables())
                return null;

            return reader.GetWriterProvider().GetSymbolWriter(module, fileName);
        }

        public ISymbolWriter GetSymbolWriter(ModuleDefinition module, Stream symbolStream)
        {
            throw new NotSupportedException();
        }
    }
}

namespace Mono.Cecil
{
    internal static partial class Mixin
    {
        public static ImageDebugHeaderEntry GetCodeViewEntry(this ImageDebugHeader header)
        {
            return GetEntry(header, ImageDebugType.CodeView);
        }

        public static ImageDebugHeaderEntry GetDeterministicEntry(this ImageDebugHeader header)
        {
            return GetEntry(header, ImageDebugType.Deterministic);
        }

        public static ImageDebugHeader AddDeterministicEntry(this ImageDebugHeader header)
        {
            var entry = new ImageDebugHeaderEntry(new ImageDebugDirectory { Type = ImageDebugType.Deterministic },
                Empty<byte>.Array);
            if (header == null)
                return new ImageDebugHeader(entry);

            var entries = new ImageDebugHeaderEntry [header.Entries.Length + 1];
            Array.Copy(header.Entries, entries, header.Entries.Length);
            entries[entries.Length - 1] = entry;
            return new ImageDebugHeader(entries);
        }

        public static ImageDebugHeaderEntry GetEmbeddedPortablePdbEntry(this ImageDebugHeader header)
        {
            return GetEntry(header, ImageDebugType.EmbeddedPortablePdb);
        }

        public static ImageDebugHeaderEntry GetPdbChecksumEntry(this ImageDebugHeader header)
        {
            return GetEntry(header, ImageDebugType.PdbChecksum);
        }

        private static ImageDebugHeaderEntry GetEntry(this ImageDebugHeader header, ImageDebugType type)
        {
            if (!header.HasEntries)
                return null;

            for (var i = 0; i < header.Entries.Length; i++)
            {
                var entry = header.Entries[i];
                if (entry.Directory.Type == type)
                    return entry;
            }

            return null;
        }

        public static string GetPdbFileName(string assemblyFileName)
        {
            return Path.ChangeExtension(assemblyFileName, ".pdb");
        }

        public static string GetMdbFileName(string assemblyFileName)
        {
            return assemblyFileName + ".mdb";
        }

        public static bool IsPortablePdb(string fileName)
        {
            using (var file = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                return IsPortablePdb(file);
            }
        }

        public static bool IsPortablePdb(Stream stream)
        {
            const uint ppdb_signature = 0x424a5342;

            if (stream.Length < 4) return false;
            var position = stream.Position;
            try
            {
                var reader = new BinaryReader(stream);
                return reader.ReadUInt32() == ppdb_signature;
            }
            finally
            {
                stream.Position = position;
            }
        }
    }
}