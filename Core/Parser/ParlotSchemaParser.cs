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

        static ParlotSchemaParser()
        {
            var notNewLine = Literals.Pattern(c => c != '\n' && c != '\r');
            // commentLine => ( WhiteSpace NonWhiteSpace )* WhiteSpace?;
            // blockCommentLines => commentLine ( "\n" blockCommentLines )?;
            // blockComment => "/*" blockCommentLines "*/";

            blockComment = ReadAllBetweenTerminators("/*", "*/")
                .Then(CleanupDoc);

            lineComment = Literals.Text("//").SkipAnd(ZeroOrOne(notNewLine)).Then(raw => (raw.ToString() ?? "").Trim());
        }
    }
}
