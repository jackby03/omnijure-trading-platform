using System.Text.Json.Serialization;

namespace Omnijure.Core.Features.Settings.Model;

public enum ExchangeType
{
    Binance,
    BinanceUS,
    Coinbase,
    Kraken,
    Bitfinex
}

public class ExchangeCredential
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "";
    public ExchangeType Exchange { get; set; } = ExchangeType.Binance;

    /// <summary>Plaintext API key — transient, never serialized to disk.</summary>
    [JsonIgnore]
    public string ApiKey { get; set; } = "";

    /// <summary>Plaintext secret — transient, never serialized to disk.</summary>
    [JsonIgnore]
    public string Secret { get; set; } = "";

    /// <summary>Base64(DPAPI(apiKey)) — persisted in settings.json.</summary>
    public string EncryptedApiKey { get; set; } = "";

    /// <summary>Base64(DPAPI(secret)) — persisted in settings.json.</summary>
    public string EncryptedSecret { get; set; } = "";

    public bool IsTestnet { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
