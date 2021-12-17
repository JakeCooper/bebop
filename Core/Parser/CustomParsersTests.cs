using Xunit;
using static Core.Parser.CustomParsers;

namespace Core.Parser.Tests
{
    public class CustomParsersTests
    {
        [Theory]
        [InlineData("*/", "Things \nand\n stuff*/blah", "Things \nand\n stuff")]
        [InlineData("\"#", "Things## \n#and#\n \"stuff\"\"#blah#", "Things## \n#and#\n \"stuff\"")]
        [InlineData("'", "'abc", "")]
        public void TestReadUntilTerminator(string terminator, string text, string expected)
        {
            var valid = ReadUntilTerminator(terminator).TryParse(text, out var result);
            Assert.True(valid);
            Assert.Equal(expected, result.ToString());
        }
    }
}
