using System;
using System.Collections.Generic;

namespace Omnijure.Core.Features.Scripting.SharpScript;

/// <summary>
/// High-level engine: parses SharpScript source and executes against candle data.
/// Caches parsed AST for performance (only re-parses when source changes).
/// </summary>
public class SharpScriptEngine
{
    private string? _cachedSource;
    private int _cachedSourceHash;
    private ScriptProgram? _cachedAst;

    public ScriptOutput Execute(string source, RingBuffer<Candle> buffer, Dictionary<string, float>? inputValues = null)
    {
        try
        {
            // Parse (with caching)
            var ast = ParseCached(source);

            // Execute
            var interpreter = new Interpreter(ast, buffer);
            if (inputValues != null)
                interpreter.SetInputValues(inputValues);

            return interpreter.Execute();
        }
        catch (SharpScriptException ex)
        {
            return new ScriptOutput { Error = ex.Message };
        }
        catch (Exception ex)
        {
            return new ScriptOutput { Error = $"Runtime error: {ex.Message}" };
        }
    }

    private ScriptProgram ParseCached(string source)
    {
        int hash = source.GetHashCode();
        if (_cachedAst != null && _cachedSourceHash == hash && _cachedSource == source)
            return _cachedAst;

        var tokens = new Lexer(source).Tokenize();
        var ast = new Parser(tokens).Parse();

        _cachedSource = source;
        _cachedSourceHash = hash;
        _cachedAst = ast;
        return ast;
    }
}
