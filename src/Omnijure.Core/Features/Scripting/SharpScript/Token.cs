namespace Omnijure.Core.Features.Scripting.SharpScript;

public enum TokenType
{
    // Literals
    Number,         // 14, 2.0, .5
    String,         // "RSI Length"
    Color,          // #FF5252, #FF525220
    True,           // true
    False,          // false

    // Identifiers & keywords
    Identifier,     // myVar, close, sma
    If,
    Else,
    And,
    Or,
    Not,

    // Operators
    Plus,           // +
    Minus,          // -
    Star,           // *
    Slash,          // /
    Percent,        // %
    Equals,         // =
    EqualEqual,     // ==
    BangEqual,      // !=
    Greater,        // >
    GreaterEqual,   // >=
    Less,           // <
    LessEqual,      // <=
    Question,       // ?
    Colon,          // :
    Dot,            // .

    // Delimiters
    LeftParen,      // (
    RightParen,     // )
    Comma,          // ,
    Newline,        // significant newline (statement separator)

    // Special
    VersionComment, // //@version=1
    EOF
}

public readonly struct Token
{
    public TokenType Type { get; }
    public string Value { get; }
    public int Line { get; }
    public int Column { get; }

    public Token(TokenType type, string value, int line, int column)
    {
        Type = type;
        Value = value;
        Line = line;
        Column = column;
    }

    public override string ToString() => $"{Type}({Value}) @{Line}:{Column}";
}
