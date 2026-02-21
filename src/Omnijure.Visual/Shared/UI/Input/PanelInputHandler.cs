using System;
using System.Collections.Generic;
using Omnijure.Visual.Rendering;

namespace Omnijure.Visual.Shared.UI.Input;

public class PanelInputHandler
{
    private readonly PanelSystem _panelSystem;
    private readonly PanelContentRenderer _renderer;

    public PanelInputHandler(PanelSystem panelSystem, PanelContentRenderer renderer)
    {
        _panelSystem = panelSystem;
        _renderer = renderer;
    }

    public bool HandlePanelScroll(float x, float y, float deltaY)
    {
        return _renderer.HandlePanelScroll(x, y, deltaY);
    }

    public bool IsScriptEditorFocused
    {
        get => _renderer.IsEditorFocused;
        set => _renderer.IsEditorFocused = value;
    }

    public int ScriptEditorActiveScript
    {
        get => _renderer.EditorActiveScript;
        set => _renderer.EditorActiveScript = value;
    }

    public bool HandleScriptEditorClick(float x, float y)
    {
        return _renderer.HandleScriptEditorClick(x, y);
    }

    public void ScriptEditorInsertChar(char ch)
    {
        _renderer.InsertChar(ch);
    }

    public void ScriptEditorHandleKey(PanelContentRenderer.EditorKey key)
    {
        _renderer.HandleEditorKey(key);
    }
}
