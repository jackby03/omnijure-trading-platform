using SkiaSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace Omnijure.Visual.Shared.UI.Rendering;

/// <summary>
/// Downloads and caches cryptocurrency token icons for display in the UI.
/// Uses the cryptocurrency-icons repository for consistent, high-quality icons.
/// </summary>
public class CryptoIconProvider
{
    private static readonly HttpClient _httpClient = new HttpClient();
    private static readonly ConcurrentDictionary<string, SKBitmap?> _iconCache = new();
    private static readonly HashSet<string> _pendingDownloads = new();

    // Known symbol -> base asset mappings (Binance pairs to base symbol)
    private static readonly Dictionary<string, string> _symbolMap = new()
    {
        // Major pairs
        ["BTC"] = "btc", ["ETH"] = "eth", ["BNB"] = "bnb",
        ["SOL"] = "sol", ["XRP"] = "xrp", ["ADA"] = "ada",
        ["DOGE"] = "doge", ["DOT"] = "dot", ["AVAX"] = "avax",
        ["MATIC"] = "matic", ["LINK"] = "link", ["SHIB"] = "shib",
        ["UNI"] = "uni", ["ATOM"] = "atom", ["LTC"] = "ltc",
        ["ETC"] = "etc", ["FIL"] = "fil", ["APT"] = "apt",
        ["NEAR"] = "near", ["ARB"] = "arb", ["OP"] = "op",
        ["SUI"] = "sui", ["SEI"] = "sei", ["TIA"] = "tia",
        ["PEPE"] = "pepe", ["WIF"] = "wif", ["FET"] = "fet",
        ["RENDER"] = "rndr", ["INJ"] = "inj", ["TRX"] = "trx",
        ["BCH"] = "bch", ["AAVE"] = "aave", ["MKR"] = "mkr",
        ["CRV"] = "crv", ["ALGO"] = "algo", ["XLM"] = "xlm",
        ["VET"] = "vet", ["HBAR"] = "hbar", ["FTM"] = "ftm",
        ["SAND"] = "sand", ["MANA"] = "mana", ["AXS"] = "axs",
        ["GALA"] = "gala", ["IMX"] = "imx", ["APE"] = "ape",
        ["USDT"] = "usdt", ["USDC"] = "usdc", ["BUSD"] = "busd",
        ["DAI"] = "dai", ["TUSD"] = "tusd",
        ["BONK"] = "bonk", ["JUP"] = "jup", ["PYTH"] = "pyth",
        ["WLD"] = "wld", ["STRK"] = "strk", ["JTO"] = "jto",
    };

    /// <summary>
    /// Gets the icon for a trading pair symbol (e.g., "BTCUSDT" -> BTC icon)
    /// Returns null if not yet loaded (triggers async download)
    /// </summary>
    public static SKBitmap? GetIcon(string symbol)
    {
        string baseSymbol = ExtractBaseSymbol(symbol);

        if (_iconCache.TryGetValue(baseSymbol, out var cached))
            return cached;

        // Trigger async download if not already pending
        if (!_pendingDownloads.Contains(baseSymbol))
        {
            _pendingDownloads.Add(baseSymbol);
            _ = DownloadIconAsync(baseSymbol);
        }

        return null;
    }

    /// <summary>
    /// Draws a crypto icon at the specified position, with a fallback colored circle + letter
    /// </summary>
    public static void DrawCryptoIcon(SKCanvas canvas, string symbol, float x, float y, float size)
    {
        string baseSymbol = ExtractBaseSymbol(symbol);
        var icon = GetIcon(symbol);

        if (icon != null)
        {
            // Draw the downloaded icon
            var destRect = new SKRect(x, y, x + size, y + size);
            using var paint = new SKPaint { IsAntialias = true };
            canvas.DrawBitmap(icon, destRect, paint);
        }
        else
        {
            // Fallback: colored circle with first letter
            DrawFallbackIcon(canvas, baseSymbol, x, y, size);
        }
    }

    /// <summary>
    /// Draws a crypto icon centered in a rectangle
    /// </summary>
    public static void DrawCryptoIconCentered(SKCanvas canvas, string symbol, SKRect rect, float size)
    {
        float x = rect.Left + (rect.Width - size) / 2;
        float y = rect.Top + (rect.Height - size) / 2;
        DrawCryptoIcon(canvas, symbol, x, y, size);
    }

    /// <summary>
    /// Draws a fallback icon with a colored circle and the first letter of the symbol
    /// </summary>
    private static void DrawFallbackIcon(SKCanvas canvas, string baseSymbol, float x, float y, float size)
    {
        // Generate a consistent color based on symbol name
        SKColor bgColor = GetSymbolColor(baseSymbol);

        float cx = x + size / 2;
        float cy = y + size / 2;
        float radius = size / 2;

        // Draw circle background
        using var bgPaint = new SKPaint
        {
            Color = bgColor,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        canvas.DrawCircle(cx, cy, radius, bgPaint);

        // Draw letter
        string letter = baseSymbol.Length > 0 ? baseSymbol[0].ToString().ToUpper() : "?";
        using var font = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), size * 0.5f);
        using var textPaint = new SKPaint
        {
            Color = SKColors.White,
            IsAntialias = true
        };

        float textWidth = font.MeasureText(letter);
        canvas.DrawText(letter, cx - textWidth / 2, cy + size * 0.18f, font, textPaint);
    }

    /// <summary>
    /// Generates a consistent color for a symbol
    /// </summary>
    private static SKColor GetSymbolColor(string symbol)
    {
        // Known brand colors
        return symbol.ToUpper() switch
        {
            "BTC" => new SKColor(247, 147, 26),     // Bitcoin orange
            "ETH" => new SKColor(98, 126, 234),      // Ethereum blue
            "BNB" => new SKColor(243, 186, 47),      // Binance yellow
            "SOL" => new SKColor(156, 106, 222),     // Solana purple
            "XRP" => new SKColor(0, 168, 224),       // Ripple blue
            "ADA" => new SKColor(0, 51, 173),        // Cardano blue
            "DOGE" => new SKColor(196, 164, 50),     // Doge gold
            "DOT" => new SKColor(230, 0, 122),       // Polkadot pink
            "AVAX" => new SKColor(232, 65, 66),      // Avalanche red
            "MATIC" => new SKColor(130, 71, 229),    // Polygon purple
            "LINK" => new SKColor(43, 95, 218),      // Chainlink blue
            "UNI" => new SKColor(255, 0, 122),       // Uniswap pink
            "ATOM" => new SKColor(46, 49, 72),       // Cosmos dark
            "LTC" => new SKColor(190, 190, 190),     // Litecoin silver
            "NEAR" => new SKColor(0, 236, 151),      // Near green
            "ARB" => new SKColor(40, 159, 237),      // Arbitrum blue
            "OP" => new SKColor(255, 4, 32),         // Optimism red
            "PEPE" => new SKColor(60, 150, 50),      // Pepe green
            "SHIB" => new SKColor(255, 165, 0),      // Shiba orange
            "TRX" => new SKColor(255, 6, 10),        // Tron red
            _ => GenerateColorFromHash(symbol)
        };
    }

    /// <summary>
    /// Generates a color from a string hash for unknown symbols
    /// </summary>
    private static SKColor GenerateColorFromHash(string text)
    {
        int hash = 0;
        foreach (char c in text)
            hash = c + ((hash << 5) - hash);

        byte r = (byte)(((hash >> 0) & 0xFF) % 200 + 55);
        byte g = (byte)(((hash >> 8) & 0xFF) % 200 + 55);
        byte b = (byte)(((hash >> 16) & 0xFF) % 200 + 55);

        return new SKColor(r, g, b);
    }

    /// <summary>
    /// Extracts the base symbol from a trading pair (e.g., "BTCUSDT" -> "BTC")
    /// </summary>
    private static string ExtractBaseSymbol(string symbol)
    {
        string upper = symbol.ToUpper();
        string[] suffixes = { "USDT", "BUSD", "USDC", "BTC", "ETH", "BNB", "TUSD", "DAI", "FDUSD" };

        foreach (var suffix in suffixes)
        {
            if (upper.EndsWith(suffix) && upper.Length > suffix.Length)
            {
                return upper[..^suffix.Length];
            }
        }

        return upper;
    }

    /// <summary>
    /// Downloads an icon from the cryptocurrency-icons GitHub repository
    /// </summary>
    private static async Task DownloadIconAsync(string baseSymbol)
    {
        try
        {
            // Map to lowercase ID
            string iconId = baseSymbol.ToLower();
            if (_symbolMap.TryGetValue(baseSymbol.ToUpper(), out var mapped))
                iconId = mapped;

            // Try cryptocurrency-icons (GitHub) - 32px color PNGs
            string url = $"https://raw.githubusercontent.com/spothq/cryptocurrency-icons/master/32/color/{iconId}.png";

            var response = await _httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var bytes = await response.Content.ReadAsByteArrayAsync();
                var bitmap = SKBitmap.Decode(bytes);
                if (bitmap != null)
                {
                    _iconCache[baseSymbol] = bitmap;
                    Console.WriteLine($"[Icons] Loaded icon for {baseSymbol}");
                    return;
                }
            }

            // Fallback: try CoinGecko-compatible icon URL
            string fallbackUrl = $"https://assets.coingecko.com/coins/images/1/small/{iconId}.png";
            response = await _httpClient.GetAsync(fallbackUrl);

            if (response.IsSuccessStatusCode)
            {
                var bytes = await response.Content.ReadAsByteArrayAsync();
                var bitmap = SKBitmap.Decode(bytes);
                if (bitmap != null)
                {
                    _iconCache[baseSymbol] = bitmap;
                    Console.WriteLine($"[Icons] Loaded icon for {baseSymbol} (fallback)");
                    return;
                }
            }

            // Mark as null so we don't try again
            _iconCache[baseSymbol] = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Icons] Failed to load icon for {baseSymbol}: {ex.Message}");
            _iconCache[baseSymbol] = null;
        }
        finally
        {
            _pendingDownloads.Remove(baseSymbol);
        }
    }

    /// <summary>
    /// Preloads icons for a list of symbols
    /// </summary>
    public static void PreloadIcons(IEnumerable<string> symbols)
    {
        foreach (var symbol in symbols)
        {
            GetIcon(symbol); // Triggers async download
        }
    }
}
