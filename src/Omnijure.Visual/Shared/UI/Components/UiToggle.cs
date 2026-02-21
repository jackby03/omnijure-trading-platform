using SkiaSharp;
using System;

namespace Omnijure.Visual.Shared.UI.Components;

public class UiToggle
{
    public SKRect Rect;
    public string Label = "";
    public string Description = "";
    public bool IsOn;
    public bool IsHovered;
    public float AnimationProgress;

    public UiToggle(string label, bool defaultValue = false, string description = "")
    {
        Label = label;
        IsOn = defaultValue;
        AnimationProgress = defaultValue ? 1f : 0f;
        Description = description;
    }

    public bool Contains(float x, float y) => Rect.Contains(x, y);

    public void Toggle()
    {
        IsOn = !IsOn;
    }

    public void UpdateAnimation(float dt)
    {
        float target = IsOn ? 1f : 0f;
        AnimationProgress += (target - AnimationProgress) * Math.Min(1f, dt * 12f);
    }
}
