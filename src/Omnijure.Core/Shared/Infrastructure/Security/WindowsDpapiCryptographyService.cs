using System;
using System.Security.Cryptography;
using System.Text;

namespace Omnijure.Core.Shared.Infrastructure.Security;

public class WindowsDpapiCryptographyService : ICryptographyService
{
    public string Encrypt(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext)) return "";
        var bytes = Encoding.UTF8.GetBytes(plaintext);
        var encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(encrypted);
    }

    public string Decrypt(string encrypted)
    {
        if (string.IsNullOrEmpty(encrypted)) return "";
        var bytes = Convert.FromBase64String(encrypted);
        var decrypted = ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(decrypted);
    }
}
