using System;

using Omnijure.Core.Features.Settings.Model;

namespace Omnijure.Core.Features.Settings.Api;

public interface ISettingsProvider
{
    AppSettings Current { get; }
    void Load();
    void Save();
    void AddCredential(ExchangeCredential cred);
    void RemoveCredential(string id);
    void UpdateCredential(ExchangeCredential updated);
}
