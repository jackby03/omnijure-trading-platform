using System;
using System.Collections.Generic;
using Omnijure.Core.DataStructures;
using Omnijure.Core.Scripting.SharpScript;

namespace Omnijure.Core.Scripting;

/// <summary>
/// Manages multiple SharpScript scripts per chart tab.
/// </summary>
public class ScriptManager
{
    private readonly List<ActiveScript> _scripts = new();
    private List<ScriptOutput> _lastOutputs = new();

    public IReadOnlyList<ActiveScript> Scripts => _scripts;
    public IReadOnlyList<ScriptOutput> Outputs => _lastOutputs;

    public int Count => _scripts.Count;

    public ActiveScript AddScript(string source, string name = "")
    {
        var script = new ActiveScript
        {
            Name = string.IsNullOrEmpty(name) ? $"Script {_scripts.Count + 1}" : name,
            Source = source,
            IsEnabled = true,
            Engine = new SharpScriptEngine()
        };
        _scripts.Add(script);
        return script;
    }

    public void RemoveScript(int index)
    {
        if (index >= 0 && index < _scripts.Count)
            _scripts.RemoveAt(index);
    }

    public void ToggleScript(int index)
    {
        if (index >= 0 && index < _scripts.Count)
            _scripts[index].IsEnabled = !_scripts[index].IsEnabled;
    }

    public void UpdateSource(int index, string source)
    {
        if (index >= 0 && index < _scripts.Count)
            _scripts[index].Source = source;
    }

    /// <summary>
    /// Executes all enabled scripts against the buffer.
    /// Returns list of outputs for rendering.
    /// </summary>
    public List<ScriptOutput> ExecuteAll(RingBuffer<Candle> buffer)
    {
        _lastOutputs.Clear();

        foreach (var script in _scripts)
        {
            if (!script.IsEnabled)
            {
                _lastOutputs.Add(new ScriptOutput { Title = script.Name, Error = "Disabled" });
                continue;
            }

            var output = script.Engine.Execute(script.Source, buffer, script.InputValues);
            script.LastOutput = output;

            // Sync input values back from output
            foreach (var input in output.Inputs)
            {
                if (!script.InputValues.ContainsKey(input.Name))
                    script.InputValues[input.Name] = input.Value;
            }

            _lastOutputs.Add(output);
        }

        return _lastOutputs;
    }
}

public class ActiveScript
{
    public string Name { get; set; } = "";
    public string Source { get; set; } = "";
    public bool IsEnabled { get; set; } = true;
    public SharpScriptEngine Engine { get; set; } = new();
    public ScriptOutput? LastOutput { get; set; }
    public Dictionary<string, float> InputValues { get; set; } = new();
}
