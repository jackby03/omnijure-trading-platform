using System;

namespace Omnijure.Core.Settings;

public interface ISettingsProvider
{
    AppSettings Current { get; }
    void Load();
    void Save();
    void AddCredential(ExchangeCredential cred);
    void RemoveCredential(string id);
    void UpdateCredential(ExchangeCredential updated);
}
