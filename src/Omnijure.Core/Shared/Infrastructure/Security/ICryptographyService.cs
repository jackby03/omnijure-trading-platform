using System;

namespace Omnijure.Core.Shared.Infrastructure.Security;

public interface ICryptographyService
{
    string Encrypt(string plaintext);
    string Decrypt(string encrypted);
}
