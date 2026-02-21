using System;

namespace Omnijure.Core.Security;

public interface ICryptographyService
{
    string Encrypt(string plaintext);
    string Decrypt(string encrypted);
}
