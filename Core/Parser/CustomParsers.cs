using Parlot;
using Parlot.Fluent;
using static Parlot.Fluent.Parsers;

namespace Core.Parser
{
    public static class CustomParsers
    {
        public static Parser<TextSpan> ReadUntilTerminator(string terminator) =>
            new ReadUntilTerminatorParser(terminator);

        public static Parser<TextSpan> ReadAllBetweenTerminators(string startTerm, string endTerm) =>
            Terms.Text(startTerm).SkipAnd(ReadUntilTerminator(endTerm)).AndSkip(Literals.Text(endTerm));
    }
}
