// Interface for interacting with any wallet's currency system. Currency types are
// string names defined in a CurrencyDatabase (ScriptableObject), not an enum, so
// games can add currencies without recompiling. Implement this interface to create
// custom wallet behaviours, or use the built-in Wallet component.

using System;
using System.Collections.Generic;

namespace MHZE.CurrencySystem
{
    public interface IWallet
    {
        event Action<string, float> OnCurrencyChanged;

        bool HasCurrency(string name, float amount);
        float GetCurrency(string name);
        bool TryGetCurrency(string name, out float amount);

        bool TryAddCurrency(string name, float amount);
        bool TryRemoveCurrency(string name, float amount);
        bool TrySetCurrency(string name, float amount);

        IReadOnlyList<string> GetSupportedCurrencyTypes();
    }
}
