using SkiaSharp;

namespace Omnijure.Visual.Rendering;

/// <summary>
/// Centralized color palette and theme management for TradingView-style UI.
/// Provides consistent colors across all renderers and supports future theme switching.
/// </summary>
public static class ThemeManager
{
    // ═══════════════════════════════════════════════════════════════════════
    // BACKGROUNDS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Main application background (darkest)</summary>
    public static SKColor Background => new SKColor(13, 17, 23);

    /// <summary>Surface elements (panels, cards, sidebars)</summary>
    public static SKColor Surface => new SKColor(22, 27, 34);

    /// <summary>Surface hover state</summary>
    public static SKColor SurfaceHover => new SKColor(30, 36, 44);

    /// <summary>Elevated surface (modals, dropdowns)</summary>
    public static SKColor SurfaceElevated => new SKColor(27, 33, 42);

    // ═══════════════════════════════════════════════════════════════════════
    // PRIMARY COLORS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Primary brand color (blue)</summary>
    public static SKColor Primary => new SKColor(56, 139, 253);

    /// <summary>Primary hover state</summary>
    public static SKColor PrimaryHover => new SKColor(79, 156, 255);

    /// <summary>Primary active/pressed state</summary>
    public static SKColor PrimaryActive => new SKColor(42, 120, 240);

    // ═══════════════════════════════════════════════════════════════════════
    // ACCENT COLORS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Bullish/positive color (green)</summary>
    public static SKColor BullishGreen => new SKColor(38, 166, 154);

    /// <summary>Bearish/negative color (red)</summary>
    public static SKColor BearishRed => new SKColor(239, 83, 80);

    /// <summary>Success color</summary>
    public static SKColor Success => new SKColor(14, 203, 129);

    /// <summary>Warning color</summary>
    public static SKColor Warning => new SKColor(255, 185, 0);

    /// <summary>Error color</summary>
    public static SKColor Error => new SKColor(246, 70, 93);

    // ═══════════════════════════════════════════════════════════════════════
    // TEXT COLORS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Primary text (highest contrast)</summary>
    public static SKColor TextPrimary => new SKColor(230, 237, 243);

    /// <summary>Secondary text (medium contrast)</summary>
    public static SKColor TextSecondary => new SKColor(139, 148, 158);

    /// <summary>Muted/disabled text (low contrast)</summary>
    public static SKColor TextMuted => new SKColor(88, 96, 105);

    /// <summary>Pure white for emphasis</summary>
    public static SKColor TextWhite => SKColors.White;

    // ═══════════════════════════════════════════════════════════════════════
    // BORDERS & DIVIDERS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Standard border color</summary>
    public static SKColor Border => new SKColor(48, 54, 61);

    /// <summary>Subtle divider lines</summary>
    public static SKColor Divider => new SKColor(48, 54, 61);

    /// <summary>Focused border (active input)</summary>
    public static SKColor BorderFocused => new SKColor(56, 139, 253);

    // ═══════════════════════════════════════════════════════════════════════
    // UI ELEMENTS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Default button background</summary>
    public static SKColor ButtonDefault => new SKColor(45, 52, 65);

    /// <summary>Button hover state</summary>
    public static SKColor ButtonHover => new SKColor(60, 70, 85);

    /// <summary>Active/selected button</summary>
    public static SKColor ButtonActive => new SKColor(56, 139, 253);

    /// <summary>Button pressed state</summary>
    public static SKColor ButtonPressed => new SKColor(42, 120, 240);

    // ═══════════════════════════════════════════════════════════════════════
    // CHART COLORS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Chart grid lines</summary>
    public static SKColor ChartGrid => new SKColor(48, 54, 61);

    /// <summary>Chart background</summary>
    public static SKColor ChartBackground => new SKColor(13, 17, 23);

    /// <summary>Crosshair lines</summary>
    public static SKColor Crosshair => new SKColor(139, 148, 158);

    /// <summary>SMA 20 indicator (yellow/gold)</summary>
    public static SKColor Indicator20 => new SKColor(255, 200, 50);

    /// <summary>SMA 50 indicator (cyan/blue)</summary>
    public static SKColor Indicator50 => new SKColor(100, 200, 255);

    /// <summary>Volume bars (bullish)</summary>
    public static SKColor VolumeBullish => new SKColor(38, 166, 154, 100);

    /// <summary>Volume bars (bearish)</summary>
    public static SKColor VolumeBearish => new SKColor(239, 83, 80, 100);

    // ═══════════════════════════════════════════════════════════════════════
    // DRAWING TOOLS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Default drawing color</summary>
    public static SKColor DrawingDefault => new SKColor(120, 130, 255);

    /// <summary>Selected drawing</summary>
    public static SKColor DrawingSelected => new SKColor(56, 139, 253);

    /// <summary>Drawing handles/control points</summary>
    public static SKColor DrawingHandle => new SKColor(255, 255, 255);

    // ═══════════════════════════════════════════════════════════════════════
    // HELPER METHODS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Creates a color with modified alpha transparency.
    /// </summary>
    /// <param name="color">Base color</param>
    /// <param name="alpha">Alpha value (0-255)</param>
    /// <returns>Color with specified alpha</returns>
    public static SKColor WithAlpha(SKColor color, byte alpha)
    {
        return color.WithAlpha(alpha);
    }

    /// <summary>
    /// Creates a semi-transparent version of a color (50% opacity).
    /// </summary>
    public static SKColor SemiTransparent(SKColor color)
    {
        return color.WithAlpha(128);
    }

    /// <summary>
    /// Creates a subtle version of a color (20% opacity).
    /// </summary>
    public static SKColor Subtle(SKColor color)
    {
        return color.WithAlpha(51);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SHADOW & DEPTH
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Subtle shadow for depth (black at 10% opacity)</summary>
    public static SKColor ShadowSubtle => new SKColor(0, 0, 0, 25);

    /// <summary>Medium shadow (black at 20% opacity)</summary>
    public static SKColor ShadowMedium => new SKColor(0, 0, 0, 51);

    /// <summary>Strong shadow (black at 30% opacity)</summary>
    public static SKColor ShadowStrong => new SKColor(0, 0, 0, 77);

    // ═══════════════════════════════════════════════════════════════════════
    // SPACING & SIZES (CONSTANTS)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Base spacing unit (8px grid)</summary>
    public const float SpacingUnit = 8f;

    /// <summary>Small spacing (4px)</summary>
    public const float SpacingSmall = SpacingUnit / 2;

    /// <summary>Medium spacing (16px)</summary>
    public const float SpacingMedium = SpacingUnit * 2;

    /// <summary>Large spacing (24px)</summary>
    public const float SpacingLarge = SpacingUnit * 3;

    /// <summary>Default border radius for rounded corners</summary>
    public const float BorderRadius = 6f;

    /// <summary>Large border radius</summary>
    public const float BorderRadiusLarge = 8f;

    /// <summary>Small border radius</summary>
    public const float BorderRadiusSmall = 4f;
}
