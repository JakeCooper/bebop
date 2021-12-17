using Xunit;
using Core.Parser;
using Parlot.Fluent;

namespace Core.Parser.Tests
{
    public class ParlotSchemaParserTests
    {
        [Theory]
        [InlineData("/**/", "")]
        [InlineData("/*Hello World!*/", "Hello World!")]
        [InlineData("/* A doc comment */", "A doc comment")]
        [InlineData("/* \n    A \ndoc\n\t* comment \n*/", "A\ndoc\ncomment")]
        public void TestBlockComments(string text, string expected)
        {
            var valid = ParlotSchemaParser.blockComment.TryParse(text, out var result);
            Assert.True(valid);
            Assert.Equal(expected, result);
        }
        
        [Theory]
        [InlineData("//\n", "")]
        [InlineData("//sometext", "sometext")]
        [InlineData("//\t sometext", "sometext")]
        [InlineData("// A line comment", "A line comment")]
        [InlineData("//A line comment", "A line comment")]
        [InlineData("//\tA line // comment", "A line // comment")]
        [InlineData("// The first line\nAnd a new line that is excluded", "The first line")]
        [InlineData("// words and things! \t", "words and things!")]
        public void TestLineComments(string text, string expected)
        {
            var valid = ParlotSchemaParser.lineComment.TryParse(text, out var result);
            Assert.True(valid);
            Assert.Equal(expected, result);
        }
    }
}
