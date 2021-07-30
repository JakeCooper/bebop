﻿using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Core.Lexer.Tokenization.Models;
using Core.Meta.Attributes;
using Core.Meta.Interfaces;

namespace Core.Meta
{
    /// <summary>
    /// A base class for definitions in a schema.
    /// </summary>
    public abstract class Definition
    {
        protected Definition(string name, Span span, string documentation, Definition? parent = null)
        {
            Parent = parent;
            Name = name;
            Span = span;
            Documentation = documentation;
        }

        /// <summary>
        /// The name of the current definition.
        /// </summary>
        public string Name { get; }
        /// <summary>
        ///     The span where the definition was found.
        /// </summary>
        public Span Span { get; }
        /// <summary>
        /// The inner text of a block comment that preceded the definition.
        /// </summary>
        public string Documentation { get; set; }
        /// <summary>
        /// The names of types this definition depends on / refers to.
        /// </summary>
        public abstract IEnumerable<string> Dependencies();

        /// <summary>
        /// Immediate parent of this definition, if it is enclosed in another definition.
        /// </summary>
        public Definition? Parent { get; set; }

        /// <summary>
        /// List of definitions enclosing this definition, from outer to inner. Empty if this is a top level definition.
        /// </summary>
        public List<Definition>? Scope
        {
            get
            {
                if (Parent is null)
                {
                    return null;
                }
                var scope = new List<Definition>();
                Definition currentDefinition = this;
                while (currentDefinition.Parent is not null)
                {
                    scope.Insert(0, currentDefinition.Parent);
                    currentDefinition = currentDefinition.Parent;
                }
                return scope;
            }
        }
    }

    /// <summary>
    /// A base class for definitions that can have an opcode, and are therefore valid at the "top level" of a Bebop packet.
    /// (In other words: struct, message, union. But you can't send a raw enum over the wire.)
    /// </summary>
    public abstract class TopLevelDefinition : Definition
    {
        protected TopLevelDefinition(string name, Span span, string documentation, BaseAttribute? opcodeAttribute, Definition? parent = null) : base(name, span, documentation, parent)
        {
            OpcodeAttribute = opcodeAttribute;
        }

        public BaseAttribute? OpcodeAttribute { get; }

        /// <summary>
        /// If this definition is part of a union branch, then this is its discriminator in the parent union.
        /// Otherwise, this property is null. (This feels a bit hacky, but oh well.)
        /// </summary>
        public byte? DiscriminatorInParent { get; set; }

        /// <summary>
        /// Compute a lower bound for the size of the wire-format encoding of a packet conforming to this definition.
        /// </summary>
        /// <param name="schema">The schema this definition belongs to, used to resolve references to other definitions.</param>
        /// <returns>The lower bound, in bytes.</returns>
        public abstract int MinimalEncodedSize(ISchema schema);
    }

    /// <summary>
    /// A base class for definitions that are an aggregate of fields. (struct, message)
    /// </summary>
    public abstract class FieldsDefinition : TopLevelDefinition
    {
        protected FieldsDefinition(string name, Span span, string documentation, BaseAttribute? opcodeAttribute, ICollection<IField> fields, Definition? parent = null) : base(name, span, documentation, opcodeAttribute, parent)
        {
            Fields = fields;
        }

        public ICollection<IField> Fields { get; }

        public override IEnumerable<string> Dependencies() => Fields.SelectMany(field => field.Type.Dependencies()).Distinct();
    }

    /// <summary>
    /// A class representing a struct definition.
    /// 
    /// A struct is an aggregate of some fields that are always present in a fixed order. It promises to never grow in later versions of the schema.
    /// </summary>
    public class StructDefinition : FieldsDefinition
    {
        public StructDefinition(string name, Span span, string documentation, BaseAttribute? opcodeAttribute, ICollection<IField> fields, bool isReadOnly, Definition? parent = null) : base(name, span, documentation, opcodeAttribute, fields, parent)
        {
            IsReadOnly = isReadOnly;
        }

        /// <summary>
        /// Is this struct "read-only"? (This will mean something like: not generating setters in the codegen.)
        /// </summary>
        public bool IsReadOnly { get; }

        override public int MinimalEncodedSize(ISchema schema)
        {
            // The encoding of a struct consists of a straightforward concatenation of the encodings of its fields.
            return Fields.Sum(f => f.MinimalEncodedSize(schema));
        }
    }

    /// <summary>
    /// A class representing a message definition.
    /// 
    /// A message is an aggregate of optional fields. Each field is prefixed on the wire by its index in the message. A message may grow in a later version of the schema.
    /// </summary>
    public class MessageDefinition : FieldsDefinition
    {
        public MessageDefinition(string name, Span span, string documentation, BaseAttribute? opcodeAttribute, ICollection<IField> fields, Definition? parent = null) : base(name, span, documentation, opcodeAttribute, fields, parent)
        {
        }

        override public int MinimalEncodedSize(ISchema schema)
        {
            // If all fields are absent.
            return 5;
        }
    }

    /// <summary>
    /// Represents an enum definition in a schema.
    /// </summary>
    public class EnumDefinition : Definition
    {
        public EnumDefinition(string name, Span span, string documentation, ICollection<IField> members, Definition? parent = null) : base(name, span, documentation, parent)
        {
            Members = members;
        }
        public ICollection<IField> Members { get; }

        public override IEnumerable<string> Dependencies() => Enumerable.Empty<string>();
    }

    public readonly struct UnionBranch
    {
        public readonly byte Discriminator;
        public readonly TopLevelDefinition Definition;

        public UnionBranch(byte discriminator, TopLevelDefinition definition)
        {
            Discriminator = discriminator;
            Definition = definition;
        }
    }

    public class UnionDefinition : TopLevelDefinition
    {
        public UnionDefinition(string name, Span span, string documentation, BaseAttribute? opcodeAttribute, ICollection<UnionBranch> branches, Definition? parent = null) : base(name, span, documentation, opcodeAttribute, parent)
        {
            Branches = branches;
        }

        public ICollection<UnionBranch> Branches { get; }

        public override IEnumerable<string> Dependencies() => Branches.Select(b => b.Definition.Name);

        override public int MinimalEncodedSize(ISchema schema)
        {
            // Length + discriminator + shortest branch.
            return 4 + 1 + (Branches.Count == 0 ? 0 : Branches.Min(b => b.Definition.MinimalEncodedSize(schema)));
        }
    }

    public class ConstDefinition : Definition
    {
        public ConstDefinition(string name, Span span, string documentation, Literal value, Definition? parent = null) : base(name, span, documentation, parent)
        {
            Value = value;
        }
        public override IEnumerable<string> Dependencies() => Enumerable.Empty<string>();

        public Literal Value { get; }
    }
}
