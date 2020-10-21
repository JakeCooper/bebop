﻿using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Compiler.Meta;
using Compiler.Meta.Extensions;
using Compiler.Meta.Interfaces;

namespace Compiler.Generators.TypeScript
{
    public class TypeScriptGenerator : IGenerator
    {
        private ISchema _schema;

        /// <summary>
        /// Generate the body of the <c>encode</c> function for the given <see cref="IDefinition"/>.
        /// </summary>
        /// <param name="definition">The definition to generate code for.</param>
        /// <returns>The generated TypeScript <c>encode</c> function body.</returns>
        public string CompileEncode(IDefinition definition)
        {
            return definition.Kind switch
            {
                AggregateKind.Message => CompileEncodeMessage(definition),
                AggregateKind.Struct => CompileEncodeStruct(definition),
                _ => throw new InvalidOperationException($"invalid CompileEncode kind: {definition.Kind} in {definition}"),
            };
        }

        private string CompileEncodeMessage(IDefinition definition)
        { 
            var builder = new IndentedStringBuilder(6);
            builder.AppendLine($"const pos = view.reserveMessageLength();");
            builder.AppendLine($"const start = view.length;");
            foreach (var field in definition.Fields)
            {
                if (field.DeprecatedAttribute.HasValue)
                {
                    continue;
                }
                builder.AppendLine($"if (message.{field.Name.ToCamelCase()} != null) {{");
                builder.AppendLine($"  view.writeByte({field.ConstantValue});");
                builder.AppendLine($"  {CompileEncodeField(field.Type, $"message.{field.Name.ToCamelCase()}")}");
                builder.AppendLine($"}}");
            }
            builder.AppendLine("view.writeByte(0);");
            builder.AppendLine("const end = view.length;");
            builder.AppendLine("view.fillMessageLength(pos, end - start);");
            return builder.ToString();
        }

        private string CompileEncodeStruct(IDefinition definition)
        {
            var builder = new IndentedStringBuilder(6);
            foreach (var field in definition.Fields)
            {
                builder.AppendLine(CompileEncodeField(field.Type, $"message.{field.Name.ToCamelCase()}"));
            }
            return builder.ToString();
        }

        private string CompileEncodeField(IType type, string target, int depth = 0)
        {
            switch (type)
            {
                case ArrayType at when at.IsBytes():
                    return $"view.writeBytes({target});";
                case ArrayType at when at.IsFloat32s():
                    return $"view.writeFloat32s({target});";
                case ArrayType at when at.IsFloat64s():
                    return $"view.writeFloat64s({target});";
                case ArrayType at:
                    var indent = new string(' ', (depth + 4) * 2);
                    var i = GeneratorUtils.LoopVariable(depth);
                    return $"var length{depth} = {target}.length;\n"
                        + indent + $"view.writeUint32(length{depth});\n"
                        + indent + $"for (var {i} = 0; {i} < length{depth}; {i}++) {{\n"
                        + indent + $"  {CompileEncodeField(at.MemberType, $"{target}[{i}]", depth + 1)}\n"
                        + indent + "}";
                case ScalarType st:
                    switch (st.BaseType)
                    {
                        case BaseType.Bool: return $"view.writeByte({target});";
                        case BaseType.Byte: return $"view.writeByte({target});";
                        case BaseType.UInt16: return $"view.writeUint16({target});";
                        case BaseType.Int16: return $"view.writeInt16({target});";
                        case BaseType.UInt32: return $"view.writeUint32({target});";
                        case BaseType.Int32: return $"view.writeInt32({target});";
                        case BaseType.UInt64: return $"view.writeUint64({target});";
                        case BaseType.Int64: return $"view.writeInt64({target});";
                        case BaseType.Float32: return $"view.writeFloat32({target});";
                        case BaseType.Float64: return $"view.writeFloat64({target});";
                        case BaseType.String: return $"view.writeString({target});";
                        case BaseType.Guid: return $"view.writeGuid({target});";
                    }
                    break;
                case DefinedType dt when _schema.Definitions[dt.Name].Kind == AggregateKind.Enum:
                    return $"view.writeEnum({target});";
                case DefinedType dt:
                    return $"{dt.Name}.encodeInto({target}, view)";
            }
            throw new InvalidOperationException($"CompileEncodeField: {type}");
        }

        /// <summary>
        /// Generate the body of the <c>decode</c> function for the given <see cref="IDefinition"/>.
        /// </summary>
        /// <param name="definition">The definition to generate code for.</param>
        /// <returns>The generated TypeScript <c>decode</c> function body.</returns>
        public string CompileDecode(IDefinition definition)
        {
            return definition.Kind switch
            {
                AggregateKind.Message => CompileDecodeMessage(definition),
                AggregateKind.Struct => CompileDecodeStruct(definition),
                _ => throw new InvalidOperationException($"invalid CompileDecode kind: {definition.Kind} in {definition}"),
            };
        }

        /// <summary>
        /// Generate the body of the <c>decode</c> function for the given <see cref="IDefinition"/>,
        /// given that its "kind" is Message.
        /// </summary>
        /// <param name="definition">The message definition to generate code for.</param>
        /// <returns>The generated TypeScript <c>decode</c> function body.</returns>
        private string CompileDecodeMessage(IDefinition definition)
        {
            var builder = new IndentedStringBuilder(6);
            builder.AppendLine("if (!(view instanceof PierogiView)) {");
            builder.AppendLine("  view = new PierogiView(view);");
            builder.AppendLine("}");
            builder.AppendLine("");
            builder.AppendLine($"let message: I{definition.Name} = {{}};");
            builder.AppendLine("const length = view.readMessageLength();");
            builder.AppendLine("const end = view.index + length;");
            builder.AppendLine("while (true) {");
            builder.Indent(2);
            builder.AppendLine("switch (view.readByte()) {");
            builder.AppendLine("  case 0:");
            builder.AppendLine("    return message;");
            builder.AppendLine("");
            foreach (var field in definition.Fields)
            {
                builder.AppendLine($"  case {field.ConstantValue}:");
                builder.AppendLine($"    message.{field.Name.ToCamelCase()} = {CompileDecodeField(field.Type)};");
                builder.AppendLine("    break;");
                builder.AppendLine("");
            }
            builder.AppendLine("  default:");
            builder.AppendLine("    view.index = end;");
            builder.AppendLine("    return message;");
            builder.AppendLine("}");
            builder.Dedent(2);
            builder.AppendLine("}");
            return builder.ToString();
        }
        
        private string CompileDecodeStruct(IDefinition definition)
        {
            var builder = new IndentedStringBuilder(6);
            builder.AppendLine("if (!(view instanceof PierogiView)) {");
            builder.AppendLine("  view = new PierogiView(view);");
            builder.AppendLine("}");
            builder.AppendLine("");
            builder.AppendLine($"var message: I{definition.Name} = {{");
            foreach (var field in definition.Fields)
            {
                builder.AppendLine($"  {field.Name.ToCamelCase()}: {CompileDecodeField(field.Type)},");
            }
            builder.AppendLine("};");
            builder.AppendLine("return message;");
            return builder.ToString();
        }

        private string CompileDecodeField(IType type)
        {
            switch (type)
            {
                case ArrayType at when at.IsBytes():
                    return "view.readBytes()";
                case ArrayType at when at.IsFloat32s():
                    return "view.readFloat32s()";
                case ArrayType at when at.IsFloat64s():
                    return "view.readFloat64s()";
                case ArrayType at:
                    return @$"(() => {{
                        let length = view.readUint32();
                        const collection = new {TypeName(at)}(length);
                        for (var i = 0; i < length; i++) collection[i] = {CompileDecodeField(at.MemberType)};
                        return collection;
                    }})()";
                case ScalarType st:
                    switch (st.BaseType)
                    {
                        case BaseType.Bool: return "!!view.readByte()";
                        case BaseType.Byte: return "view.readByte()";
                        case BaseType.UInt16: return "view.readUint16()";
                        case BaseType.Int16: return "view.readInt16()";
                        case BaseType.UInt32: return "view.readUint32()";
                        case BaseType.Int32: return "view.readInt32()";
                        case BaseType.UInt64: return "view.readUint64()";
                        case BaseType.Int64: return "view.readInt64()";
                        case BaseType.Float32: return "view.readFloat32()";
                        case BaseType.Float64: return "view.readFloat64()";
                        case BaseType.String: return "view.readString()";
                        case BaseType.Guid: return "view.readGuid()";
                    }
                    break;
                case DefinedType dt when _schema.Definitions[dt.Name].Kind == AggregateKind.Enum:
                    return $"view.readUint32() as {dt.Name}";
                case DefinedType dt:
                    return $"{dt.Name}.decode(view)";
            }
            throw new InvalidOperationException($"CompileDecodeField: {type}");
        }

        /// <summary>
        /// Generate a TypeScript type name for the given <see cref="IType"/>.
        /// </summary>
        /// <param name="type">The field type to generate code for.</param>
        /// <returns>The TypeScript type name.</returns>
        private string TypeName(in IType type)
        {
            switch (type)
            {
                case ScalarType st:
                    switch (st.BaseType)
                    {
                        case BaseType.Bool:
                            return "boolean";
                        case BaseType.Byte:
                        case BaseType.UInt16:
                        case BaseType.Int16:
                        case BaseType.UInt32:
                        case BaseType.Int32:
                        case BaseType.Float32:
                        case BaseType.Float64:
                            return "number";
                        case BaseType.UInt64:
                        case BaseType.Int64:
                            return "bigint";
                        case BaseType.String:
                        case BaseType.Guid:
                            return "string";
                    }
                    break;
                case ArrayType at when at.IsBytes():
                    return "Uint8Array";
                case ArrayType at when at.IsFloat32s():
                    return "Float32Array";
                case ArrayType at when at.IsFloat64s():
                    return "Float64Array";
                case ArrayType at:
                    return $"Array<{TypeName(at.MemberType)}>";
                case DefinedType dt:
                    var isEnum = _schema.Definitions[dt.Name].Kind == AggregateKind.Enum;
                    return (isEnum ? "" : "I") + dt.Name;
            }
            throw new InvalidOperationException($"GetTypeName: {type}");
        }

        /// <summary>
        /// Generate code for a Pierogi schema.
        /// </summary>
        /// <returns>The generated code.</returns>
        public string Compile(ISchema schema)
        {
            _schema = schema;

            var builder = new StringBuilder();
            builder.AppendLine("import { PierogiView } from \"./PierogiView\";");
            builder.AppendLine("");
            if (!string.IsNullOrWhiteSpace(_schema.Namespace))
            {
                builder.AppendLine($"export namespace {_schema.Namespace} {{");
            }

            foreach (var definition in _schema.Definitions.Values)
            {
                if (definition.Kind == AggregateKind.Enum)
                {
                    builder.AppendLine($"  export enum {definition.Name} {{");
                    for (var i = 0; i < definition.Fields.Count; i++)
                    {
                        var field = definition.Fields.ElementAt(i);
                        var comma = i + 1 < definition.Fields.Count ? "," : "";
                        builder.AppendLine($"      {field.Name} = {field.ConstantValue}{comma}");
                    }
                    builder.AppendLine("  }");
                }

                if (definition.Kind == AggregateKind.Message || definition.Kind == AggregateKind.Struct)
                {
                    builder.AppendLine($"  export interface I{definition.Name} {{");
                    for (var i = 0; i < definition.Fields.Count; i++)
                    {
                        var field = definition.Fields.ElementAt(i);
                        var type = TypeName(field.Type);
                        if (field.DeprecatedAttribute.HasValue && !string.IsNullOrWhiteSpace(field.DeprecatedAttribute.Value.Message))
                        {
                            builder.AppendLine("    /**");
                            builder.AppendLine($"     * @deprecated {field.DeprecatedAttribute.Value.Message}");
                            builder.AppendLine($"     */");
                        }
                        builder.AppendLine($"    {(definition.IsReadOnly ? "readonly " : "")}{field.Name.ToCamelCase()}{(definition.Kind == AggregateKind.Message ? "?" : "")}: {type}");
                    }
                    builder.AppendLine("  }");
                    builder.AppendLine("");

                    builder.AppendLine($"  export const {definition.Name} = {{");
                    builder.AppendLine($"    encode(message: I{definition.Name}): Uint8Array {{");
                    builder.AppendLine("      const view = new PierogiView();");
                    builder.AppendLine("      this.encodeInto(message, view);");
                    builder.AppendLine("      return view.toArray();");
                    builder.AppendLine("    },");
                    builder.AppendLine("");
                    builder.AppendLine($"    encodeInto(message: I{definition.Name}, view: PierogiView): void {{");
                    builder.Append(CompileEncode(definition));
                    builder.AppendLine("    },");
                    builder.AppendLine("");

                    builder.AppendLine($"    decode(view: PierogiView | Uint8Array): I{definition.Name} {{");
                    builder.Append(CompileDecode(definition));
                    builder.AppendLine("    },");
                    builder.AppendLine("  };");
                }
            }
            if (!string.IsNullOrWhiteSpace(_schema.Namespace))
            {
                builder.AppendLine("}");
            }
            builder.AppendLine("");


            return builder.ToString().TrimEnd();
        }

        public void WriteAuxiliaryFiles(string outputPath)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using Stream stream = assembly.GetManifestResourceStream("Compiler.Generators.TypeScript.PierogiView.ts");
            using StreamReader reader = new StreamReader(stream);
            string result = reader.ReadToEnd();
            File.WriteAllText(Path.Join(outputPath, "PierogiView.ts"), result);
        }
    }
}