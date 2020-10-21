﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Compiler.Exceptions;
using Compiler.IO.Interfaces;
using Compiler.Lexer.Extensions;
using Compiler.Lexer.Tokenization.Interfaces;
using Compiler.Lexer.Tokenization.Models;

namespace Compiler.Lexer.Tokenization
{
    public class Tokenizer : ITokenizer
    {
        private ISchemaReader _reader;

        protected int TokenCount { get; private set; }

        protected Span CurrentTokenPosition { get; private set; }

        public void Dispose()
        {
            _reader?.Dispose();
        }

        /// <summary>
        /// Assigns a reader for working on a schema
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="reader"></param>
        public void AssignReader<T>(T reader) where T : ISchemaReader
        {
            _reader = reader;
        }

        /// <summary>
        /// Yields back a a stream of tokens asynchronously 
        /// </summary>
        /// <returns></returns>
        public async IAsyncEnumerable<Token> TokenStream()
        {
            CurrentTokenPosition = Span.Empty;
            TokenCount = 0;
            while (true)
            {
                var current = GetCharSkippingTrivia();
                if (current == '\0') break;
                Token? scan = TryScan(current);
                if (!scan.HasValue)
                {
                    throw new UnrecognizedTokenException(current, CurrentTokenPosition, _reader.SourcePath);
                }

                CurrentTokenPosition = scan.Value.Span.End;
                yield return await Task.FromResult(scan.Value);
                TokenCount++;
            }
            ++TokenCount;
            yield return new Token(TokenKind.EndOfFile, string.Empty, CurrentTokenPosition, TokenCount);
        }

        /// <summary>
        /// Skip over whitespace and comments, then return the first char of the next token.
        /// (This may be '\0' if the end of file is reached.)
        /// </summary>
        /// <returns>The first char of the next token.</returns>
        public char GetCharSkippingTrivia()
        {
            var inLineComment = false;
            while (true)
            {
                // Parse \r or \n or \r\n as a newline.
                var c = _reader.PeekChar();
                var isNewLine = false;
                if (c == '\r')
                {
                    _reader.GetChar();
                    c = _reader.PeekChar();
                    isNewLine = true;
                }
                if (c == '\n')
                {
                    _reader.GetChar();
                    isNewLine = true;
                }
                if (isNewLine)
                {
                    CurrentTokenPosition = CurrentTokenPosition.StartOfNextLine;
                    inLineComment = false;
                    continue;
                }

                // Skip over non-newline whitespace.
                // While in a line comment, skip over anything that isn't a newline.
                if (c.IsWhitespace() || inLineComment)
                {
                    _reader.GetChar();
                    CurrentTokenPosition = CurrentTokenPosition.Next;
                    continue;
                }

                // This character starts the next token, unless it's the start of a line comment.
                c = _reader.GetChar();
                if (c == '/' && _reader.PeekChar() == '/')
                {
                    _reader.GetChar();
                    inLineComment = true;
                    continue;
                }
                return c;
            }
        }

        /// <summary>
        /// Tries to assign a token to the current <paramref name="surrogate"/>
        /// </summary>
        /// <param name="surrogate"></param>
        /// <returns></returns>
        public Token? TryScan(char surrogate) => surrogate switch
        {
            _ when IsBlockComment(surrogate, out var b) => b,
            _ when IsSymbol(surrogate, out var s) => s,
            _ when IsIdentifier(surrogate, out var i) => i,
            _ when IsLiteral(surrogate, out var l) => l,
            _ when IsNumber(surrogate, out var n) => n,
            _ => null
        };


      
      
        /// <summary>
        /// Determines if a surrogate leads into a block comment.
        /// </summary>
        /// <param name="surrogate"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        private bool IsBlockComment(char surrogate, out Token token)
        {
            token = default;
            if (surrogate != '/' || _reader.PeekChar() != '*')
            {
                return false;
            }
           
            _reader.GetChar();
            var builder = new StringBuilder();
            var currentChar = _reader.GetChar();
            while (currentChar != '\0')
            {
                
                var isNewLine = false;
 
                if (currentChar == '\r')
                {
                    builder.Append(currentChar);
                    currentChar = _reader.GetChar();
                    isNewLine = true;
                }
                if (currentChar == '\n')
                {
                    builder.Append(currentChar);
                    isNewLine = true;
                }
                if (isNewLine)
                {
                    // skip over all whitespace after a newline and find the beginning of the next line.
                    if (_reader.PeekChar().IsWhitespace())
                    {
                        currentChar = _reader.GetChar();
                        builder.Append(currentChar);
                        continue;
                    }
                    currentChar = _reader.GetChar();
                    // the next character is the end of the block comment
                    // skip so we consume only at the end of the method
                    if (_reader.PeekChar() == '/')
                    {
                        continue;
                    }
                }

                // skip over the left edges of aligned block comments
                if (currentChar == '*' && _reader.PeekChar() == '*')
                {
                    currentChar = _reader.GetChar();
                    if (currentChar == '*')
                    {
                        currentChar = _reader.GetChar();
                    }
                }
                // we have reached the end of the block comment
                if (currentChar == '*' && _reader.PeekChar() == '/')
                {
                    _reader.GetChar();
                    break;
                }
                builder.Append(currentChar);
                currentChar = _reader.GetChar();
            }
            token = new Token(TokenKind.BlockComment, builder.ToString().Trim(), CurrentTokenPosition, TokenCount);
            return true;
        }


        /// <summary>
        /// Determines if a surrogate is a integral token
        /// </summary>
        /// <param name="surrogate"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        private bool IsNumber(char surrogate, out Token token)
        {
            token = default;
            if (surrogate != '-' && !surrogate.IsDecimalDigit())
            {
                return false;
            }
            var builder = new StringBuilder();
            builder.Append(surrogate);
            while (_reader.PeekChar().IsDecimalDigit())
            {
                builder.Append(_reader.GetChar());
            }
            token = new Token(TokenKind.Number, builder.ToString(), CurrentTokenPosition, TokenCount);
            return true;
        }

        /// <summary>
        /// Determines if a surrogate is the beginning of a literal token
        /// </summary>
        /// <param name="surrogate"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        private bool IsLiteral(char surrogate, out Token token)
        {
            token = default;
            return surrogate switch
            {
                _ when surrogate.IsSingleQuote() => ScanStringLiteral(out token),
                _ when surrogate.IsDoubleQuote() => ScanStringExpandable(out token),
                _ => false
            };
        }

        /// <summary>
        /// Reads a string that is wrapped in double quotes
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        private bool ScanStringExpandable(out Token token)
        {
            token = default;
            var builder = new StringBuilder();
            var currentChar = _reader.GetChar();
            while (currentChar != '\0')
            {
                if (currentChar.IsDoubleQuote())
                {
                    if (!_reader.PeekChar().IsDoubleQuote())
                    {
                        break;
                    }
                    currentChar = _reader.GetChar();
                }
                builder.Append(currentChar);
                currentChar = _reader.GetChar();
            }
            if (currentChar == '\0')
            {
                // EOF
                return false;
            }
            token = new Token(TokenKind.StringExpandable, builder.ToString(), CurrentTokenPosition, TokenCount);
            return true;
        }

        /// <summary>
        /// Reads a string that is wrapped in single quotes.
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        private bool ScanStringLiteral(out Token token)
        {
            token = default;
            var builder = new StringBuilder();
            var currentChar = _reader.GetChar();
            while (currentChar != '\0')
            {
                if (currentChar.IsSingleQuote())
                {
                    if (!_reader.PeekChar().IsSingleQuote())
                    {
                        break;
                    }
                    currentChar = _reader.GetChar();
                }
                builder.Append(currentChar);
                currentChar = _reader.GetChar();
            }
            if (currentChar == '\0')
            {
                // EOF
                return false;
            }
            token = new Token(TokenKind.StringLiteral, builder.ToString(), CurrentTokenPosition, TokenCount);
            return true;
        }


        /// <summary>
        /// Determines if a surrogate is one that is defined with a <see cref="Attributes.SymbolAttribute"/>
        /// </summary>
        /// <param name="surrogate"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        private bool IsSymbol(char surrogate, out Token token)
        {
            if (TokenizerExtensions.TryGetSymbol(surrogate, out var kind))
            {
                token = new Token(kind, surrogate.ToString(), CurrentTokenPosition, TokenCount);
                return true;
            }
            token = default;
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="surrogate"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        private bool IsIdentifier(char surrogate, out Token token)
        {
            if (!surrogate.IsIdentifierStart())
            {
                token = default;
                return false;
            }

            var builder = new StringBuilder();
            builder.Append(surrogate);
            while (_reader.PeekChar().IsIdentifierFollow())
            {
                builder.Append(_reader.GetChar());
            }
            var lexeme = builder.ToString();

            token = TokenizerExtensions.TryGetKeyword(lexeme, out var kind)
                ? new Token(kind, lexeme, CurrentTokenPosition, TokenCount)
                : new Token(TokenKind.Identifier, lexeme, CurrentTokenPosition, TokenCount);
            return true;
        }
    }
}