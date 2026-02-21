using System.Collections.Generic;

namespace Omnijure.Core.Features.Scripting.SharpScript;

// ═══════════════════════════════════════════════════════════════
// AST Node Types
// ═══════════════════════════════════════════════════════════════

public abstract class AstNode
{
    public int Line { get; set; }
    public int Column { get; set; }
}

// ─── Literals ────────────────────────────────────────────────

public class NumberLiteral : AstNode
{
    public float Value { get; set; }
}

public class StringLiteral : AstNode
{
    public string Value { get; set; } = "";
}

public class BoolLiteral : AstNode
{
    public bool Value { get; set; }
}

public class ColorLiteral : AstNode
{
    public uint Argb { get; set; } // 0xAARRGGBB
}

// ─── Expressions ─────────────────────────────────────────────

public class IdentifierExpr : AstNode
{
    public string Name { get; set; } = "";
}

public class MemberAccessExpr : AstNode
{
    public AstNode Object { get; set; } = null!;
    public string Member { get; set; } = "";
}

public class BinaryExpr : AstNode
{
    public AstNode Left { get; set; } = null!;
    public string Op { get; set; } = "";
    public AstNode Right { get; set; } = null!;
}

public class UnaryExpr : AstNode
{
    public string Op { get; set; } = "";
    public AstNode Operand { get; set; } = null!;
}

public class TernaryExpr : AstNode
{
    public AstNode Condition { get; set; } = null!;
    public AstNode IfTrue { get; set; } = null!;
    public AstNode IfFalse { get; set; } = null!;
}

public class FunctionCallExpr : AstNode
{
    public string Name { get; set; } = "";
    public List<AstNode> Args { get; set; } = new();
    public Dictionary<string, AstNode> NamedArgs { get; set; } = new();
}

// ─── Statements ──────────────────────────────────────────────

public class AssignmentStmt : AstNode
{
    public string Name { get; set; } = "";
    public AstNode Value { get; set; } = null!;
}

public class ExpressionStmt : AstNode
{
    public AstNode Expression { get; set; } = null!;
}

public class IfStmt : AstNode
{
    public AstNode Condition { get; set; } = null!;
    public List<AstNode> ThenBlock { get; set; } = new();
    public List<AstNode>? ElseBlock { get; set; }
}

// ─── Top Level ───────────────────────────────────────────────

public class ScriptProgram : AstNode
{
    public int Version { get; set; } = 1;
    public FunctionCallExpr? Declaration { get; set; } // indicator() or strategy()
    public List<AstNode> Statements { get; set; } = new();
}
