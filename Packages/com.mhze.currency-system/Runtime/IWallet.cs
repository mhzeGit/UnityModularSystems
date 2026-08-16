// Interface for interacting with any wallet's currency system. Implement it to create custom wallet behaviours, or use the built-in Wallet component.

using System;
using System.Collections.Generic;

namespace MHZE.CurrencySystem
{
    public interface IWallet
    {
        event Action<CurrencyType, float> OnCurrencyChanged;

        bool HasCurrency(CurrencyType type, float amount);
        float GetCurrency(CurrencyType type);
        bool TryGetCurrency(CurrencyType type, out float amount);

        bool TryAddCurrency(CurrencyType type, float amount);
        bool TryRemoveCurrency(CurrencyType type, float amount);
        bool TrySetCurrency(CurrencyType type, float amount);

        IReadOnlyList<CurrencyType> GetSupportedCurrencyTypes();
    }
}
