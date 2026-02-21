using System;
using System.Collections.Generic;
using SkiaSharp;
using Omnijure.Visual.Rendering;

namespace Omnijure.Visual.Widgets.Panels;

public class ScriptEditorRenderer : IPanelRenderer
{
    public string PanelId => PanelDefinitions.SCRIPT_EDITOR;
    private const float ScriptTabBarH = 28;
    private const float ScriptToolbarH = 34;
    private const float ScriptErrorBarH = 24;
    private const float ScriptLineH = 18;
    private const float ScriptGutterW = 40;

    public ScriptManager? ActiveScriptManager { get; set; }
    public int EditorActiveScript { get; set; }
    public int EditorCursorPos { get; set; }
    public bool IsEditorFocused { get; set; }
    private int _cursorBlinkTicks;

    private readonly List<(int index, SKRect tabRect, SKRect closeRect)> _scriptTabRects = new();
    private SKRect _scriptAddTabRect;

    public IReadOnlyList<(int index, SKRect tabRect, SKRect closeRect)> ScriptTabRects => _scriptTabRects;
    public SKRect ScriptAddTabRect => _scriptAddTabRect;
    
    // For handling input
    public float GetCodeAreaTop() => ScriptTabBarH + ScriptToolbarH;

    public void Render(SKCanvas canvas, SKRect rect, float scrollY)
    {
        float width = rect.Width;
        float height = rect.Height;

        if (ActiveScriptManager == null)
        {
            RenderPlaceholderPanel(canvas, width, height, "Script Editor", "No active chart tab");
            return;
        }

        var scripts = ActiveScriptManager;
        var paint = PaintPool.Instance.Rent();
        try
        {
            paint.IsAntialias = true;

            RenderScriptTabBar(canvas, paint, width, scripts);

            float toolbarY = ScriptTabBarH;
            RenderScriptToolbar(canvas, paint, width, toolbarY, scripts);

            float codeY = ScriptTabBarH + ScriptToolbarH;
            float codeH = height - codeY;

            string source = "";
            string? error = null;
            if (scripts.Count > 0 && EditorActiveScript < scripts.Count)
            {
                var active = scripts.Scripts[EditorActiveScript];
                source = active.Source;
                error = active.LastOutput?.Error;
            }

            if (error != null && error != "Disabled")
            {
                codeH -= ScriptErrorBarH;
                RenderScriptErrorBar(canvas, paint, width, codeY + codeH, error);
            }

            canvas.Save();
            canvas.ClipRect(new SKRect(0, codeY, width, codeY + codeH));
            canvas.Translate(0, codeY);
            RenderScriptCode(canvas, paint, width, codeH, source, scrollY);
            canvas.Restore();
        }
        finally
        {
            PaintPool.Instance.Return(paint);
        }
    }

    public float GetContentHeight()
    {
        if (ActiveScriptManager == null) return 0;
        var scripts = ActiveScriptManager;
        string source = "";
        if (scripts.Count > 0 && EditorActiveScript < scripts.Count)
        {
            source = scripts.Scripts[EditorActiveScript].Source;
        }

        int lineCount = 1;
        foreach (char c in source) if (c == '\n') lineCount++;

        return lineCount * ScriptLineH + 40; // Add some padding
    }

    private void RenderScriptTabBar(SKCanvas canvas, SKPaint paint, float width, ScriptManager scripts)
    {
        paint.Color = new SKColor(18, 20, 26);
        paint.Style = SKPaintStyle.Fill;
        canvas.DrawRect(0, 0, width, ScriptTabBarH, paint);

        paint.Color = new SKColor(40, 45, 55);
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = 1;
        canvas.DrawLine(0, ScriptTabBarH, width, ScriptTabBarH, paint);

        _scriptTabRects.Clear();
        using var tabFont = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), 10);
        float tabX = 4;
        float tabY = 2;
        float tabH = ScriptTabBarH - 4;

        for (int i = 0; i < scripts.Count; i++)
        {
            var script = scripts.Scripts[i];
            bool isActive = i == EditorActiveScript;
            string label = script.Name;
            float labelW = tabFont.MeasureText(label);
            float closeW = 14;
            float tabW = 10 + labelW + 8 + closeW + 6;

            var tabRect = new SKRect(tabX, tabY, tabX + tabW, tabY + tabH);
            var closeRect = new SKRect(tabX + tabW - closeW - 4, tabY + (tabH - 10) / 2,
                tabX + tabW - 4, tabY + (tabH - 10) / 2 + 10);
            _scriptTabRects.Add((i, tabRect, closeRect));

            paint.Style = SKPaintStyle.Fill;
            if (isActive)
            {
                paint.Color = new SKColor(30, 34, 42);
                canvas.DrawRoundRect(new SKRoundRect(tabRect, 3, 3), paint);

                paint.Color = new SKColor(56, 139, 253);
                canvas.DrawRect(tabX + 4, tabY + tabH - 2, tabW - 8, 2, paint);
            }

            paint.Color = script.IsEnabled ? new SKColor(46, 204, 113) : new SKColor(100, 105, 115);
            canvas.DrawCircle(tabX + 7, ScriptTabBarH / 2, 3, paint);

            paint.Color = isActive ? new SKColor(200, 205, 215) : new SKColor(100, 105, 115);
            canvas.DrawText(label, tabX + 14, ScriptTabBarH / 2 + 4, tabFont, paint);

            paint.Color = isActive ? new SKColor(140, 145, 155) : new SKColor(80, 85, 95);
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = 1.2f;
            float cx = closeRect.MidX, cy = closeRect.MidY, cs = 3f;
            canvas.DrawLine(cx - cs, cy - cs, cx + cs, cy + cs, paint);
            canvas.DrawLine(cx + cs, cy - cs, cx - cs, cy + cs, paint);

            tabX += tabW + 2;
        }

        float addSize = tabH;
        _scriptAddTabRect = new SKRect(tabX, tabY, tabX + addSize, tabY + addSize);
        paint.Style = SKPaintStyle.Fill;
        paint.Color = new SKColor(25, 29, 36);
        canvas.DrawRoundRect(new SKRoundRect(_scriptAddTabRect, 4, 4), paint);

        paint.Color = new SKColor(100, 108, 118);
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = 1.5f;
        float plusCx = _scriptAddTabRect.MidX, plusCy = _scriptAddTabRect.MidY, plusSz = 5f;
        canvas.DrawLine(plusCx - plusSz, plusCy, plusCx + plusSz, plusCy, paint);
        canvas.DrawLine(plusCx, plusCy - plusSz, plusCx, plusCy + plusSz, paint);
    }

    private void RenderScriptToolbar(SKCanvas canvas, SKPaint paint, float width, float y, ScriptManager scripts)
    {
        paint.Style = SKPaintStyle.Fill;
        paint.Color = new SKColor(22, 25, 32);
        canvas.DrawRect(0, y, width, ScriptToolbarH, paint);

        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = 1;
        paint.Color = new SKColor(35, 40, 50);
        canvas.DrawLine(0, y + ScriptToolbarH, width, y + ScriptToolbarH, paint);

        using var btnFont = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), 10);
        float btnX = 6;
        float btnY = y + 4;
        float btnH = ScriptToolbarH - 8;

        bool enabled = scripts.Count > 0 && EditorActiveScript < scripts.Count && scripts.Scripts[EditorActiveScript].IsEnabled;
        paint.Style = SKPaintStyle.Fill;
        paint.Color = enabled ? new SKColor(30, 80, 50) : new SKColor(50, 35, 35);
        float toggleW = btnFont.MeasureText(enabled ? "ON" : "OFF") + 14;
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(btnX, btnY, btnX + toggleW, btnY + btnH), 3, 3), paint);
        paint.Color = enabled ? new SKColor(46, 204, 113) : new SKColor(200, 80, 80);
        canvas.DrawText(enabled ? "ON" : "OFF", btnX + 7, btnY + btnH - 5, btnFont, paint);
        btnX += toggleW + 6;

        if (scripts.Count > 0 && EditorActiveScript < scripts.Count)
        {
            paint.Color = new SKColor(140, 145, 155);
            string title = scripts.Scripts[EditorActiveScript].Name;
            canvas.DrawText(title, btnX, btnY + btnH - 5, btnFont, paint);
        }

        paint.Color = new SKColor(80, 85, 95);
        string countStr = $"{scripts.Count} script(s)";
        float countW = btnFont.MeasureText(countStr);
        canvas.DrawText(countStr, width - countW - 8, btnY + btnH - 5, btnFont, paint);
    }

    private void RenderScriptErrorBar(SKCanvas canvas, SKPaint paint, float width, float y, string error)
    {
        paint.Style = SKPaintStyle.Fill;
        paint.Color = new SKColor(60, 25, 25);
        canvas.DrawRect(0, y, width, ScriptErrorBarH, paint);

        using var errFont = new SKFont(SKTypeface.FromFamilyName("Cascadia Code") ?? SKTypeface.FromFamilyName("Consolas"), 10);
        paint.Color = new SKColor(239, 83, 80);
        canvas.DrawText(error, 8, y + ScriptErrorBarH - 6, errFont, paint);
    }

    private void RenderScriptCode(SKCanvas canvas, SKPaint paint, float width, float height, string source, float scrollY)
    {
        paint.Style = SKPaintStyle.Fill;
        paint.Color = new SKColor(16, 18, 22);
        canvas.DrawRect(0, 0, width, height, paint);

        if (string.IsNullOrEmpty(source))
        {
            if (IsEditorFocused)
            {
                _cursorBlinkTicks++;
                bool cursorVisible = (_cursorBlinkTicks / 30) % 2 == 0;
                if (cursorVisible)
                {
                    paint.Color = new SKColor(200, 205, 215);
                    canvas.DrawRect(ScriptGutterW + 8, 6, 1.5f, ScriptLineH - 2, paint);
                }
            }

            paint.Color = new SKColor(80, 85, 95);
            using var hintFont = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Italic), 11);
            if (!IsEditorFocused)
                canvas.DrawText("Write your SharpScript here...", ScriptGutterW + 8, 24, hintFont, paint);
            return;
        }

        paint.Color = new SKColor(20, 22, 28);
        canvas.DrawRect(0, 0, ScriptGutterW, height, paint);

        paint.Color = new SKColor(35, 40, 50);
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = 1;
        canvas.DrawLine(ScriptGutterW, 0, ScriptGutterW, height, paint);
        paint.Style = SKPaintStyle.Fill;

        var lines = source.Split('\n');
        using var monoFont = new SKFont(SKTypeface.FromFamilyName("Cascadia Code") ?? SKTypeface.FromFamilyName("Consolas"), 11);

        int cursorLine = 0, cursorCol = 0;
        if (IsEditorFocused)
        {
            AbsToLineCol(source, EditorCursorPos, out cursorLine, out cursorCol);
        }

        int visibleLines = (int)(height / ScriptLineH) + 1;
        int startLine = (int)(scrollY / ScriptLineH);
        if (startLine < 0) startLine = 0;

        for (int i = 0; i < visibleLines && (startLine + i) < lines.Length; i++)
        {
            int lineNum = startLine + i;
            float y = i * ScriptLineH + ScriptLineH - 3;

            if (IsEditorFocused && lineNum == cursorLine)
            {
                paint.Color = new SKColor(25, 28, 36);
                canvas.DrawRect(ScriptGutterW, i * ScriptLineH, width - ScriptGutterW, ScriptLineH, paint);
            }

            paint.Color = (IsEditorFocused && lineNum == cursorLine)
                ? new SKColor(130, 135, 145) : new SKColor(65, 70, 80);
            string numStr = (lineNum + 1).ToString();
            float numW = monoFont.MeasureText(numStr);
            canvas.DrawText(numStr, ScriptGutterW - numW - 4, y, monoFont, paint);

            DrawHighlightedLine(canvas, paint, monoFont, lines[lineNum], ScriptGutterW + 8, y, width - ScriptGutterW - 8);

            if (IsEditorFocused && lineNum == cursorLine)
            {
                _cursorBlinkTicks++;
                bool cursorVisible = (_cursorBlinkTicks / 30) % 2 == 0;
                if (cursorVisible)
                {
                    string beforeCursor = cursorCol <= lines[lineNum].Length
                        ? lines[lineNum][..cursorCol] : lines[lineNum];
                    float cursorX = ScriptGutterW + 8 + monoFont.MeasureText(beforeCursor);
                    paint.Color = new SKColor(200, 205, 215);
                    canvas.DrawRect(cursorX, i * ScriptLineH + 2, 1.5f, ScriptLineH - 2, paint);
                }
            }
        }
    }

    public static void AbsToLineCol(string source, int absPos, out int line, out int col)
    {
        line = 0;
        col = 0;
        int pos = Math.Min(absPos, source.Length);
        for (int i = 0; i < pos; i++)
        {
            if (source[i] == '\n') { line++; col = 0; }
            else col++;
        }
    }

    public static int LineColToAbs(string source, int line, int col)
    {
        int currentLine = 0;
        int pos = 0;
        while (pos < source.Length && currentLine < line)
        {
            if (source[pos] == '\n') currentLine++;
            pos++;
        }
        return Math.Min(pos + col, source.Length);
    }

    private static void DrawHighlightedLine(SKCanvas canvas, SKPaint paint, SKFont font, string line, float x, float y, float maxW)
    {
        string trimmed = line.TrimStart();
        float curX = x;
        float indentW = font.MeasureText(line[..^trimmed.Length]);
        curX += indentW;

        if (trimmed.StartsWith("//"))
        {
            paint.Color = new SKColor(90, 95, 105);
            canvas.DrawText(trimmed, curX, y, font, paint);
            return;
        }

        int pos = 0;
        while (pos < trimmed.Length)
        {
            char c = trimmed[pos];

            if (c == ' ' || c == '\t')
            {
                float spW = font.MeasureText(" ");
                curX += spW;
                pos++;
                continue;
            }

            if (c == '"')
            {
                int end = trimmed.IndexOf('"', pos + 1);
                if (end < 0) end = trimmed.Length - 1;
                string str = trimmed[pos..(end + 1)];
                paint.Color = new SKColor(152, 195, 121);
                canvas.DrawText(str, curX, y, font, paint);
                curX += font.MeasureText(str);
                pos = end + 1;
                continue;
            }

            if (c == '#')
            {
                int end = pos + 1;
                while (end < trimmed.Length && IsHexChar(trimmed[end])) end++;
                string col = trimmed[pos..end];
                paint.Color = new SKColor(209, 154, 102);
                canvas.DrawText(col, curX, y, font, paint);
                curX += font.MeasureText(col);
                pos = end;
                continue;
            }

            if (char.IsDigit(c) || (c == '.' && pos + 1 < trimmed.Length && char.IsDigit(trimmed[pos + 1])))
            {
                int end = pos;
                bool hasDot = false;
                while (end < trimmed.Length && (char.IsDigit(trimmed[end]) || (trimmed[end] == '.' && !hasDot)))
                {
                    if (trimmed[end] == '.') hasDot = true;
                    end++;
                }
                string num = trimmed[pos..end];
                paint.Color = new SKColor(209, 154, 102);
                canvas.DrawText(num, curX, y, font, paint);
                curX += font.MeasureText(num);
                pos = end;
                continue;
            }

            if (char.IsLetter(c) || c == '_')
            {
                int end = pos;
                while (end < trimmed.Length && (char.IsLetterOrDigit(trimmed[end]) || trimmed[end] == '_' || trimmed[end] == '.'))
                    end++;
                string word = trimmed[pos..end];

                paint.Color = word switch
                {
                    "if" or "else" or "and" or "or" or "not" or "true" or "false" => new SKColor(198, 120, 221),
                    "indicator" or "strategy" => new SKColor(86, 182, 194),
                    "sma" or "ema" or "rsi" or "stdev" or "highest" or "lowest" or
                    "crossover" or "crossunder" or "plot" or "hline" or "bgcolor" or
                    "plotshape" or "alertcondition" or "input" or
                    "strategy.entry" or "strategy.close" or "strategy.long" or "strategy.short" or
                    "math.abs" or "math.max" or "math.min" or "math.sqrt" => new SKColor(229, 192, 123),
                    "close" or "open" or "high" or "low" or "volume" or "bar_index" or "time" => new SKColor(224, 108, 117),
                    "color" or "overlay" or "linewidth" or "linestyle" or "location" or "style" or
                    "dashed" or "dotted" or "solid" or "abovebar" or "belowbar" or
                    "triangleup" or "triangledown" or "arrowup" or "arrowdown" or "circle" or "cross" or "diamond" => new SKColor(86, 182, 194),
                    _ => new SKColor(171, 178, 191)
                };

                canvas.DrawText(word, curX, y, font, paint);
                curX += font.MeasureText(word);
                pos = end;
                continue;
            }

            string ch = c.ToString();
            paint.Color = new SKColor(140, 145, 160);
            canvas.DrawText(ch, curX, y, font, paint);
            curX += font.MeasureText(ch);
            pos++;
        }
    }

    private static bool IsHexChar(char c) =>
        char.IsDigit(c) || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

    private void RenderPlaceholderPanel(SKCanvas canvas, float width, float height, string title, string subtitle)
    {
        var paint = PaintPool.Instance.Rent();
        try
        {
            using var fontTitle = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), 13);
            using var fontSub = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), 11);

            paint.IsAntialias = true;
            paint.Color = new SKColor(110, 115, 125);
            float tw = fontTitle.MeasureText(title);
            canvas.DrawText(title, (width - tw) / 2, height / 2 - 8, fontTitle, paint);

            paint.Color = new SKColor(70, 75, 85);
            float sw = fontSub.MeasureText(subtitle);
            canvas.DrawText(subtitle, (width - sw) / 2, height / 2 + 14, fontSub, paint);
        }
        finally
        {
            PaintPool.Instance.Return(paint);
        }
    }
}
