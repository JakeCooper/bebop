using System.Linq;
using Core.Meta.Extensions;
using Parlot;

namespace Core.Parser
{
    public static class Tools
    {
        /// <summary>
        /// Clean multiple lines of docs into a simple \n separated string without leading whitespace or '*' chars.
        /// </summary>
        public static string CleanupDoc(TextSpan raw) =>
            string.Concat(
                raw.ToString().GetLines().Select(l => l.TrimWhitespaceAnd('*') + '\n')
            ).Trim();
    }
}