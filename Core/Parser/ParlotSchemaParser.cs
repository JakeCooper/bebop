using System;
using System.Linq;
using Core.Lexer.Extensions;
using Core.Meta;
using Parlot;
using Parlot.Fluent;
using static Parlot.Fluent.Parsers;
using static Core.Parser.CustomParsers;
using static Core.Parser.Tools;

namespace Core.Parser
{
    public class ParlotSchemaParser
    {
        // private static readonly Parser<Meta.Interfaces.ISchema> EXPRESSION;
        public static readonly Parser<string> blockComment;
        public static readonly Parser<string> lineComment;
        public static readonly Parser<(BaseType type, TextSpan ident, TextSpan value)> constDefinition;

        static ParlotSchemaParser()
        {
            var notNewLine = Literals.Pattern(c => c != '\n' && c != '\r');
            var dblQuote = Literals.Char('"');
            var semi = Literals.Char(';');
            var wellKnownType = OneOf(
                new[]
                {
                    ("bool", BaseType.Bool), ("byte", BaseType.Byte), ("uint8", BaseType.Byte),
                    ("int16", BaseType.Int16), ("uint16", BaseType.UInt16), ("int32", BaseType.Int32),
                    ("uint32", BaseType.UInt32), ("int64", BaseType.Int64), ("uint64", BaseType.UInt64),
                    ("float32", BaseType.Float32), ("float64", BaseType.Float64), ("string", BaseType.String),
                    ("guid", BaseType.Guid), ("date", BaseType.Date),
                }.Select(pair => Literals.Text(pair.Item1).Discard(pair.Item2)).ToArray()
            );

            blockComment = ReadAllBetweenTerminators("/*", "*/")
                .Then(CleanupDoc);

            lineComment = Literals.Text("//").SkipAnd(ZeroOrOne(notNewLine))
                .Then(static raw => (raw.ToString() ?? "").Trim());

            var documentation = ZeroOrMany(blockComment).Then(static l => l.Count > 0 ? l.Last() : "");

            var decimalDigits = Literals.Pattern(static c => c.IsDecimalDigit());
            var hexDigits = Literals.Pattern(static c => c.IsHexDigit());
            var hexStart = Literals.Text("0x", true);
            var unsignedIntegerLiteral = ZeroOrOne(hexStart)
                .Switch((_ctx, v) =>
                    v is null
                        ? decimalDigits
                        : hexDigits);
            var unaryNeg = Literals.Char('-');
            var signedIntegerLiteral = Capture(ZeroOrOne(unaryNeg).And(unsignedIntegerLiteral));
            var floatLiteral = OneOf(
                Capture(ZeroOrOne(unaryNeg).And(Literals.Text("inf"))),
                Capture(Literals.Text("nan")),
                Capture(ZeroOrOne(unaryNeg).And(decimalDigits).And(Literals.Char('.').And(ZeroOrOne(decimalDigits))))
            );

            constDefinition =
                Literals.Text("const")
                    .SkipAnd(SkipWhiteSpace(wellKnownType))
                    .And(SkipWhiteSpace(Literals.Identifier()))
                    .AndSkip(SkipWhiteSpace(Literals.Char('=')))
                    .Switch((_ctx, prev) =>
                        SkipWhiteSpace(prev.Item1 switch
                            {
                                BaseType.Byte or BaseType.UInt16 or BaseType.UInt32 or BaseType.UInt64 or BaseType.Date
                                    => unsignedIntegerLiteral,
                                BaseType.Int16 or BaseType.Int32 or BaseType.Int64 => signedIntegerLiteral,
                                BaseType.Float32 or BaseType.Float64 => floatLiteral,
                                BaseType.String => Literals.String(StringLiteralQuotes.Double),
                                BaseType.Guid => Between(dblQuote, Literals.Pattern(c => c.IsHexDigit() || c == '-'),
                                    dblQuote),
                                _ => throw new ArgumentOutOfRangeException()
                            })
                            // re-add the type and identifier
                            .Then(v => (prev.Item1, prev.Item2, v))
                    ).AndSkip(SkipWhiteSpace(semi));
        }
    }
}