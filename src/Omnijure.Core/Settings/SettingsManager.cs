using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Omnijure.Core.Settings;

public class SettingsManager
{
    private static readonly string SettingsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OmnijureTDS");

    private static readonly string SettingsPath = Path.Combine(SettingsDir, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public AppSettings Current { get; private set; } = new();

    public void Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return; // Use defaults

            var json = File.ReadAllText(SettingsPath);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            if (loaded != null)
            {
                Current = loaded;
                DecryptAllCredentials();
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Settings] Failed to load: {ex.Message}");
            Current = new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);

            // Encrypt credentials before serialization
            EncryptAllCredentials();

            var json = JsonSerializer.Serialize(Current, JsonOptions);
            var tmpPath = SettingsPath + ".tmp";

            File.WriteAllText(tmpPath, json);
            File.Move(tmpPath, SettingsPath, overwrite: true);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Settings] Failed to save: {ex.Message}");
        }
    }

    private void EncryptAllCredentials()
    {
        foreach (var cred in Current.Exchange.Credentials)
        {
            if (!string.IsNullOrEmpty(cred.ApiKey))
                cred.EncryptedApiKey = Encrypt(cred.ApiKey);
            if (!string.IsNullOrEmpty(cred.Secret))
                cred.EncryptedSecret = Encrypt(cred.Secret);
        }
    }

    private void DecryptAllCredentials()
    {
        foreach (var cred in Current.Exchange.Credentials)
        {
            try
            {
                if (!string.IsNullOrEmpty(cred.EncryptedApiKey))
                    cred.ApiKey = Decrypt(cred.EncryptedApiKey);
                if (!string.IsNullOrEmpty(cred.EncryptedSecret))
                    cred.Secret = Decrypt(cred.EncryptedSecret);
            }
            catch
            {
                // Decryption failed (different user profile, corrupted data)
                cred.ApiKey = "";
                cred.Secret = "";
            }
        }
    }

    public void AddCredential(ExchangeCredential cred)
    {
        Current.Exchange.Credentials.Add(cred);
    }

    public void RemoveCredential(string id)
    {
        Current.Exchange.Credentials.RemoveAll(c => c.Id == id);
        if (Current.Exchange.ActiveCredentialId == id)
            Current.Exchange.ActiveCredentialId = "";
    }

    public void UpdateCredential(ExchangeCredential updated)
    {
        var idx = Current.Exchange.Credentials.FindIndex(c => c.Id == updated.Id);
        if (idx >= 0)
            Current.Exchange.Credentials[idx] = updated;
    }

    public static string Encrypt(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext)) return "";
        var bytes = Encoding.UTF8.GetBytes(plaintext);
        var encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(encrypted);
    }

    public static string Decrypt(string encrypted)
    {
        if (string.IsNullOrEmpty(encrypted)) return "";
        var bytes = Convert.FromBase64String(encrypted);
        var decrypted = ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(decrypted);
    }
}
