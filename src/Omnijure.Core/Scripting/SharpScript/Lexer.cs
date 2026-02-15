using System;
using System.Collections.Generic;
using System.Globalization;

namespace Omnijure.Core.Scripting.SharpScript;

public class Lexer
{
    private readonly string _source;
    private int _pos;
    private int _line = 1;
    private int _col = 1;

    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "if", "else", "and", "or", "not", "true", "false"
    };

    public Lexer(string source)
    {
        _source = source ?? "";
    }

    public List<Token> Tokenize()
    {
        var tokens = new List<Token>();
        bool lastWasNewline = true; // suppress leading newlines

        while (_pos < _source.Length)
        {
            char c = _source[_pos];

            // Skip spaces and tabs (NOT newlines)
            if (c is ' ' or '\t')
            {
                Advance();
                continue;
            }

            // Carriage return
            if (c == '\r')
            {
                Advance();
                continue;
            }

            // Newline
            if (c == '\n')
            {
                if (!lastWasNewline && tokens.Count > 0)
                {
                    tokens.Add(new Token(TokenType.Newline, "\\n", _line, _col));
                }
                _line++;
                _col = 1;
                _pos++;
                lastWasNewline = true;
                continue;
            }

            lastWasNewline = false;

            // Comments
            if (c == '/' && Peek(1) == '/')
            {
                // Version comment: //@version=N
                if (_pos + 2 < _source.Length && _source[_pos + 2] == '@')
                {
                    int start = _pos;
                    while (_pos < _source.Length && _source[_pos] != '\n') _pos++;
                    string comment = _source[start.._pos];
                    tokens.Add(new Token(TokenType.VersionComment, comment, _line, _col));
                    continue;
                }
                // Regular comment — skip to end of line
                while (_pos < _source.Length && _source[_pos] != '\n') _pos++;
                continue;
            }

            // Numbers
            if (char.IsDigit(c) || (c == '.' && _pos + 1 < _source.Length && char.IsDigit(_source[_pos + 1])))
            {
                tokens.Add(ReadNumber());
                continue;
            }

            // Strings
            if (c == '"')
            {
                tokens.Add(ReadString());
                continue;
            }

            // Colors #RRGGBB or #RRGGBBAA
            if (c == '#' && _pos + 1 < _source.Length && IsHexChar(_source[_pos + 1]))
            {
                tokens.Add(ReadColor());
                continue;
            }

            // Identifiers & keywords
            if (char.IsLetter(c) || c == '_')
            {
                tokens.Add(ReadIdentifier());
                continue;
            }

            // Two-character operators
            if (c == '=' && Peek(1) == '=') { tokens.Add(MakeToken(TokenType.EqualEqual, "==", 2)); continue; }
            if (c == '!' && Peek(1) == '=') { tokens.Add(MakeToken(TokenType.BangEqual, "!=", 2)); continue; }
            if (c == '>' && Peek(1) == '=') { tokens.Add(MakeToken(TokenType.GreaterEqual, ">=", 2)); continue; }
            if (c == '<' && Peek(1) == '=') { tokens.Add(MakeToken(TokenType.LessEqual, "<=", 2)); continue; }

            // Single-character operators and delimiters
            var singleToken = c switch
            {
                '+' => TokenType.Plus,
                '-' => TokenType.Minus,
                '*' => TokenType.Star,
                '/' => TokenType.Slash,
                '%' => TokenType.Percent,
                '=' => TokenType.Equals,
                '>' => TokenType.Greater,
                '<' => TokenType.Less,
                '?' => TokenType.Question,
                ':' => TokenType.Colon,
                '.' => TokenType.Dot,
                '(' => TokenType.LeftParen,
                ')' => TokenType.RightParen,
                ',' => TokenType.Comma,
                _ => (TokenType?)null
            };

            if (singleToken.HasValue)
            {
                tokens.Add(MakeToken(singleToken.Value, c.ToString(), 1));
                continue;
            }

            // Unknown character — skip
            Advance();
        }

        // Remove trailing newlines
        while (tokens.Count > 0 && tokens[^1].Type == TokenType.Newline)
            tokens.RemoveAt(tokens.Count - 1);

        tokens.Add(new Token(TokenType.EOF, "", _line, _col));
        return tokens;
    }

    private Token ReadNumber()
    {
        int startCol = _col;
        int start = _pos;
        bool hasDot = false;

        while (_pos < _source.Length)
        {
            char c = _source[_pos];
            if (char.IsDigit(c)) { Advance(); }
            else if (c == '.' && !hasDot) { hasDot = true; Advance(); }
            else break;
        }

        return new Token(TokenType.Number, _source[start.._pos], _line, startCol);
    }

    private Token ReadString()
    {
        int startCol = _col;
        Advance(); // skip opening "
        int start = _pos;

        while (_pos < _source.Length && _source[_pos] != '"' && _source[_pos] != '\n')
        {
            if (_source[_pos] == '\\' && _pos + 1 < _source.Length) Advance(); // skip escape
            Advance();
        }

        string value = _source[start.._pos];
        if (_pos < _source.Length && _source[_pos] == '"') Advance(); // skip closing "

        return new Token(TokenType.String, value, _line, startCol);
    }

    private Token ReadColor()
    {
        int startCol = _col;
        int start = _pos;
        Advance(); // skip #

        while (_pos < _source.Length && IsHexChar(_source[_pos]))
            Advance();

        return new Token(TokenType.Color, _source[start.._pos], _line, startCol);
    }

    private Token ReadIdentifier()
    {
        int startCol = _col;
        int start = _pos;

        while (_pos < _source.Length && (char.IsLetterOrDigit(_source[_pos]) || _source[_pos] == '_'))
            Advance();

        string value = _source[start.._pos];

        TokenType type = value switch
        {
            "if" => TokenType.If,
            "else" => TokenType.Else,
            "and" => TokenType.And,
            "or" => TokenType.Or,
            "not" => TokenType.Not,
            "true" => TokenType.True,
            "false" => TokenType.False,
            _ => TokenType.Identifier
        };

        return new Token(type, value, _line, startCol);
    }

    private Token MakeToken(TokenType type, string value, int length)
    {
        var token = new Token(type, value, _line, _col);
        _pos += length;
        _col += length;
        return token;
    }

    private void Advance()
    {
        _pos++;
        _col++;
    }

    private char Peek(int offset)
    {
        int idx = _pos + offset;
        return idx < _source.Length ? _source[idx] : '\0';
    }

    private static bool IsHexChar(char c) =>
        char.IsDigit(c) || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
}
