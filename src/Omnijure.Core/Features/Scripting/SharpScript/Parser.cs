using System;
using System.Collections.Generic;
using System.Globalization;

namespace Omnijure.Core.Features.Scripting.SharpScript;

public class Parser
{
    private readonly List<Token> _tokens;
    private int _pos;

    public Parser(List<Token> tokens)
    {
        _tokens = tokens;
    }

    public ScriptProgram Parse()
    {
        var program = new ScriptProgram();

        SkipNewlines();

        // Optional //@version=N
        if (Current.Type == TokenType.VersionComment)
        {
            string v = Current.Value;
            int eqIdx = v.IndexOf('=');
            if (eqIdx >= 0 && int.TryParse(v[(eqIdx + 1)..], out int ver))
                program.Version = ver;
            Advance();
            SkipNewlines();
        }

        // Optional indicator() or strategy() declaration
        if (Current.Type == TokenType.Identifier &&
            Current.Value is "indicator" or "strategy" &&
            Peek(1).Type == TokenType.LeftParen)
        {
            program.Declaration = ParseFunctionCall();
            SkipNewlines();
        }

        // Body statements
        while (Current.Type != TokenType.EOF)
        {
            SkipNewlines();
            if (Current.Type == TokenType.EOF) break;
            program.Statements.Add(ParseStatement());
            SkipNewlines();
        }

        return program;
    }

    // ═══════════════════════════════════════════════════════════
    // Statements
    // ═══════════════════════════════════════════════════════════

    private AstNode ParseStatement()
    {
        // if statement
        if (Current.Type == TokenType.If)
            return ParseIf();

        // Assignment: identifier = expr
        if (Current.Type == TokenType.Identifier && Peek(1).Type == TokenType.Equals)
        {
            var name = Current.Value;
            int line = Current.Line, col = Current.Column;
            Advance(); // skip identifier
            Advance(); // skip =
            var value = ParseExpression();
            return new AssignmentStmt { Name = name, Value = value, Line = line, Column = col };
        }

        // Expression statement (function call, etc.)
        var expr = ParseExpression();
        return new ExpressionStmt { Expression = expr, Line = expr.Line, Column = expr.Column };
    }

    private IfStmt ParseIf()
    {
        int line = Current.Line, col = Current.Column;
        Expect(TokenType.If);

        var condition = ParseExpression();
        SkipNewlines();

        var thenBlock = ParseBlock();

        List<AstNode>? elseBlock = null;
        SkipNewlines();
        if (Current.Type == TokenType.Else)
        {
            Advance();
            SkipNewlines();
            if (Current.Type == TokenType.If)
            {
                elseBlock = new List<AstNode> { ParseIf() };
            }
            else
            {
                elseBlock = ParseBlock();
            }
        }

        return new IfStmt { Condition = condition, ThenBlock = thenBlock, ElseBlock = elseBlock, Line = line, Column = col };
    }

    private List<AstNode> ParseBlock()
    {
        var stmts = new List<AstNode>();

        // Single-line block (no indent detection needed — just parse one statement per line)
        // For simplicity: parse statements until we hit a newline followed by a non-indented line,
        // or until we hit else/EOF
        // Pine-style: blocks are indented with spaces/tabs. We use a simpler approach:
        // parse a single statement as the block.
        stmts.Add(ParseStatement());

        return stmts;
    }

    // ═══════════════════════════════════════════════════════════
    // Expressions (precedence climbing)
    // ═══════════════════════════════════════════════════════════

    private AstNode ParseExpression() => ParseTernary();

    private AstNode ParseTernary()
    {
        var expr = ParseOr();

        if (Current.Type == TokenType.Question)
        {
            int line = Current.Line, col = Current.Column;
            Advance(); // skip ?
            var ifTrue = ParseExpression();
            Expect(TokenType.Colon);
            var ifFalse = ParseExpression();
            return new TernaryExpr { Condition = expr, IfTrue = ifTrue, IfFalse = ifFalse, Line = line, Column = col };
        }

        return expr;
    }

    private AstNode ParseOr()
    {
        var left = ParseAnd();
        while (Current.Type == TokenType.Or)
        {
            int line = Current.Line, col = Current.Column;
            Advance();
            var right = ParseAnd();
            left = new BinaryExpr { Left = left, Op = "or", Right = right, Line = line, Column = col };
        }
        return left;
    }

    private AstNode ParseAnd()
    {
        var left = ParseComparison();
        while (Current.Type == TokenType.And)
        {
            int line = Current.Line, col = Current.Column;
            Advance();
            var right = ParseComparison();
            left = new BinaryExpr { Left = left, Op = "and", Right = right, Line = line, Column = col };
        }
        return left;
    }

    private AstNode ParseComparison()
    {
        var left = ParseAddSub();

        while (Current.Type is TokenType.EqualEqual or TokenType.BangEqual
               or TokenType.Greater or TokenType.GreaterEqual
               or TokenType.Less or TokenType.LessEqual)
        {
            int line = Current.Line, col = Current.Column;
            string op = Current.Value;
            Advance();
            var right = ParseAddSub();
            left = new BinaryExpr { Left = left, Op = op, Right = right, Line = line, Column = col };
        }
        return left;
    }

    private AstNode ParseAddSub()
    {
        var left = ParseMulDiv();

        while (Current.Type is TokenType.Plus or TokenType.Minus)
        {
            int line = Current.Line, col = Current.Column;
            string op = Current.Value;
            Advance();
            var right = ParseMulDiv();
            left = new BinaryExpr { Left = left, Op = op, Right = right, Line = line, Column = col };
        }
        return left;
    }

    private AstNode ParseMulDiv()
    {
        var left = ParseUnary();

        while (Current.Type is TokenType.Star or TokenType.Slash or TokenType.Percent)
        {
            int line = Current.Line, col = Current.Column;
            string op = Current.Value;
            Advance();
            var right = ParseUnary();
            left = new BinaryExpr { Left = left, Op = op, Right = right, Line = line, Column = col };
        }
        return left;
    }

    private AstNode ParseUnary()
    {
        if (Current.Type == TokenType.Minus)
        {
            int line = Current.Line, col = Current.Column;
            Advance();
            var operand = ParseUnary();
            return new UnaryExpr { Op = "-", Operand = operand, Line = line, Column = col };
        }
        if (Current.Type == TokenType.Not)
        {
            int line = Current.Line, col = Current.Column;
            Advance();
            var operand = ParseUnary();
            return new UnaryExpr { Op = "not", Operand = operand, Line = line, Column = col };
        }
        return ParsePostfix();
    }

    private AstNode ParsePostfix()
    {
        var expr = ParsePrimary();

        // Member access: strategy.entry, strategy.long, etc.
        while (Current.Type == TokenType.Dot)
        {
            int line = Current.Line, col = Current.Column;
            Advance(); // skip .
            string member = Current.Value;
            Advance(); // skip member name

            // Check if it's a method call: obj.method(args)
            if (Current.Type == TokenType.LeftParen)
            {
                var (args, namedArgs) = ParseArgList();
                // Flatten to a function call: "strategy.entry"
                string fullName = expr is IdentifierExpr id ? $"{id.Name}.{member}" : member;
                expr = new FunctionCallExpr { Name = fullName, Args = args, NamedArgs = namedArgs, Line = line, Column = col };
            }
            else
            {
                // Property access: strategy.long → treated as identifier "strategy.long"
                string fullName = expr is IdentifierExpr id2 ? $"{id2.Name}.{member}" : member;
                expr = new IdentifierExpr { Name = fullName, Line = line, Column = col };
            }
        }

        return expr;
    }

    private AstNode ParsePrimary()
    {
        var token = Current;

        switch (token.Type)
        {
            case TokenType.Number:
                Advance();
                return new NumberLiteral
                {
                    Value = float.Parse(token.Value, CultureInfo.InvariantCulture),
                    Line = token.Line, Column = token.Column
                };

            case TokenType.String:
                Advance();
                return new StringLiteral { Value = token.Value, Line = token.Line, Column = token.Column };

            case TokenType.True:
                Advance();
                return new BoolLiteral { Value = true, Line = token.Line, Column = token.Column };

            case TokenType.False:
                Advance();
                return new BoolLiteral { Value = false, Line = token.Line, Column = token.Column };

            case TokenType.Color:
                Advance();
                return new ColorLiteral { Argb = ParseColorValue(token.Value), Line = token.Line, Column = token.Column };

            case TokenType.Identifier:
                if (Peek(1).Type == TokenType.LeftParen)
                    return ParseFunctionCall();

                Advance();
                return new IdentifierExpr { Name = token.Value, Line = token.Line, Column = token.Column };

            case TokenType.LeftParen:
                Advance(); // skip (
                var expr = ParseExpression();
                Expect(TokenType.RightParen);
                return expr;

            default:
                throw new SharpScriptException($"Unexpected token '{token.Value}' ({token.Type})", token.Line, token.Column);
        }
    }

    private FunctionCallExpr ParseFunctionCall()
    {
        var nameToken = Current;
        Advance(); // skip function name

        var (args, namedArgs) = ParseArgList();

        return new FunctionCallExpr
        {
            Name = nameToken.Value,
            Args = args,
            NamedArgs = namedArgs,
            Line = nameToken.Line,
            Column = nameToken.Column
        };
    }

    private (List<AstNode> args, Dictionary<string, AstNode> namedArgs) ParseArgList()
    {
        Expect(TokenType.LeftParen);

        var args = new List<AstNode>();
        var namedArgs = new Dictionary<string, AstNode>();

        while (Current.Type != TokenType.RightParen && Current.Type != TokenType.EOF)
        {
            // Check for named argument: name=value
            if (Current.Type == TokenType.Identifier && Peek(1).Type == TokenType.Equals)
            {
                string name = Current.Value;
                Advance(); // skip name
                Advance(); // skip =
                namedArgs[name] = ParseExpression();
            }
            else
            {
                args.Add(ParseExpression());
            }

            if (Current.Type == TokenType.Comma)
                Advance();
        }

        Expect(TokenType.RightParen);
        return (args, namedArgs);
    }

    // ═══════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════

    private Token Current => _pos < _tokens.Count ? _tokens[_pos] : new Token(TokenType.EOF, "", 0, 0);

    private Token Peek(int offset)
    {
        int idx = _pos + offset;
        return idx < _tokens.Count ? _tokens[idx] : new Token(TokenType.EOF, "", 0, 0);
    }

    private void Advance() { if (_pos < _tokens.Count) _pos++; }

    private void Expect(TokenType type)
    {
        if (Current.Type != type)
            throw new SharpScriptException($"Expected {type}, got {Current.Type} ('{Current.Value}')", Current.Line, Current.Column);
        Advance();
    }

    private void SkipNewlines()
    {
        while (Current.Type == TokenType.Newline) Advance();
    }

    private static uint ParseColorValue(string colorStr)
    {
        // #RRGGBB → 0xFFRRGGBB, #RRGGBBAA → 0xAARRGGBB
        string hex = colorStr.TrimStart('#');
        if (hex.Length == 6)
        {
            uint rgb = uint.Parse(hex, NumberStyles.HexNumber);
            return 0xFF000000 | rgb;
        }
        if (hex.Length == 8)
        {
            uint rgba = uint.Parse(hex, NumberStyles.HexNumber);
            // RRGGBBAA → AARRGGBB
            uint aa = rgba & 0xFF;
            uint rrggbb = rgba >> 8;
            return (aa << 24) | rrggbb;
        }
        return 0xFFFFFFFF;
    }
}

public class SharpScriptException : Exception
{
    public int Line { get; }
    public int Column { get; }

    public SharpScriptException(string message, int line, int column)
        : base($"[{line}:{column}] {message}")
    {
        Line = line;
        Column = column;
    }
}
