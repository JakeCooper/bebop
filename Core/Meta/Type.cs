﻿using System;
using System.Collections.Generic;
using System.Linq;
using Core.Lexer.Tokenization.Models;
using Core.Meta.Interfaces;

namespace Core.Meta
{
    /// <summary>
    /// A scalar type, like "int" or "byte". It represents *one* of its underlying base type.
    /// </summary>
    class ScalarType : TypeBase
    {
        public BaseType BaseType { get; }

        public ScalarType(BaseType baseType, Span span, string asString) : base(span, asString)
        {
            BaseType = baseType;
        }

        internal override IEnumerable<string> Dependencies() => Enumerable.Empty<string>();
    }

    /// <summary>
    /// An array type, like "int[]" or "Foo[]". It represents an array of its underlying type.
    /// </summary>
    class ArrayType : TypeBase
    {
        public TypeBase MemberType { get; }
        public ArrayType(TypeBase memberType, Span span, string asString) : base(span, asString)
        {
            MemberType = memberType;
        }

        public bool IsBytes()
        {
            return MemberType is ScalarType st && st.BaseType == BaseType.Byte;
        }

        public bool IsFloat32s()
        {
            return MemberType is ScalarType st && st.BaseType == BaseType.Float32;
        }

        public bool IsFloat64s()
        {
            return MemberType is ScalarType st && st.BaseType == BaseType.Float64;
        }

        internal override IEnumerable<string> Dependencies() => MemberType.Dependencies();
    }

    /// <summary>
    /// An option type, like "string?". It represents a possibly absent value.
    /// </summary>
    class OptionType : TypeBase
    {
        public TypeBase MemberType { get; }
        public OptionType(TypeBase memberType, Span span, string asString) : base(span, asString)
        {
            MemberType = memberType;
        }

        internal override IEnumerable<string> Dependencies() => MemberType.Dependencies();
    }

    /// <summary>
    /// A map type, like "map[int, int]" or "map[string, map[int, Foo]]".
    /// It represents a list of key-value pairs, where each key occurs only once.
    /// </summary>
    class MapType : TypeBase
    {
        public TypeBase KeyType { get; }
        public TypeBase ValueType { get; }
        public MapType(TypeBase keyType, TypeBase valueType, Span span, string asString) : base(span, asString)
        {
            KeyType = keyType;
            ValueType = valueType;
        }

        internal override IEnumerable<string> Dependencies() => KeyType.Dependencies().Concat(ValueType.Dependencies());
    }

    class DefinedType : TypeBase
    {
        /// <summary>
        /// The name of the defined type being referred to.
        /// </summary>
        public string Name { get; }

        public DefinedType(string name, Span span, string asString) : base(span, asString)
        {
            Name = name;
        }

        internal override IEnumerable<string> Dependencies() => new[] { Name };
    }

    public static class TypeExtensions
    {
        public static bool IsFixedScalar(this TypeBase type)
        {
            return type is ScalarType and { BaseType: not BaseType.String };
        }
        public static bool IsStruct(this TypeBase type, ISchema schema)
        {
            return type is DefinedType dt && schema.Definitions[dt.Name] is StructDefinition;
        }

        public static bool IsMessage(this TypeBase type, ISchema schema)
        {
            return type is DefinedType dt && schema.Definitions[dt.Name] is MessageDefinition;
        }

        public static bool IsUnion(this TypeBase type, ISchema schema)
        {
            return type is DefinedType dt && schema.Definitions[dt.Name] is UnionDefinition;
        }

        public static bool IsEnum(this TypeBase type, ISchema schema)
        {
            return type is DefinedType dt && schema.Definitions[dt.Name] is EnumDefinition;
        }

        public static int MinimalEncodedSize(this TypeBase type, ISchema schema)
        {
            return type switch
            {
                OptionType => 1,
                ArrayType or MapType => 4,
                ScalarType st => st.BaseType.Size(),
                DefinedType dt when schema is not null && schema.Definitions[dt.Name] is EnumDefinition => 4,
                DefinedType dt when schema is not null && schema.Definitions[dt.Name] is TopLevelDefinition td => td.MinimalEncodedSize(schema),
                _ => throw new ArgumentOutOfRangeException(type.ToString())
            };
        }

        public static int Size(this BaseType type)
        {
            return type switch
            {
                BaseType.Bool => 1,
                BaseType.Byte => 1,
                BaseType.UInt16 or BaseType.Int16 => 2,
                BaseType.UInt32 or BaseType.Int32 => 4,
                BaseType.UInt64 or BaseType.Int64 => 8,
                BaseType.Float32 => 4,
                BaseType.Float64 => 8,
                BaseType.String => 4,
                BaseType.Guid => 16,
                BaseType.Date => 8,
                _ => throw new ArgumentOutOfRangeException(type.ToString()),
            };
        }
    }
}
