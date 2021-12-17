using Parlot;
using Parlot.Fluent;

namespace Core.Parser
{
    internal class ReadUntilTerminatorParser : Parser<TextSpan>
    {
        private readonly string _terminator;
        
        public ReadUntilTerminatorParser(string terminator)
        {
            if (terminator.Length < 1)
            {
                throw new System.ArgumentException("Terminator must be at least one character");
            }
            _terminator = terminator;
        }
        
        
        public override bool Parse(ParseContext context, ref ParseResult<TextSpan> result)
        {
            context.EnterParser(this);
            var start = context.Scanner.Cursor.Offset;
            var terminatorFound = false;
            
            while (!terminatorFound && context.Scanner.Cursor.Offset < context.Scanner.Buffer.Length)
            {
                context.Scanner.ReadWhile(c => c != _terminator[0]);
                var matchesSoFar = true;
                for (var i = 0; matchesSoFar && i < _terminator.Length; ++i)
                {
                    if (_terminator[i] != context.Scanner.Cursor.PeekNext(i))
                    {
                        matchesSoFar = false;
                    }
                }
                
                if (matchesSoFar) {
                    terminatorFound = true;
                }
                else
                {
                    // we found something that looks similar but is not
                    context.Scanner.Cursor.Advance(1);
                }
            }

            var end = context.Scanner.Cursor.Offset;
            
            if (!terminatorFound)
            {
                // we read nothing, or did not find the terminator
                return false;
            }
            
            result.Set(start, context.Scanner.Cursor.Offset, new TextSpan(context.Scanner.Buffer, start, end - start));
            return true;
        }
    }
}