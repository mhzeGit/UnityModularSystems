// Default wallet behaviour. Assigned to a GameObject that owns it. Stores a list of
// currency entries (name, default value, current value) where the names come from a
// CurrencyDatabase (ScriptableObject). Resets to the default values on Awake and
// implements IWallet for getting, adding, removing, and setting currency.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace MHZE.CurrencySystem
{
    public class Wallet : MonoBehaviour, IWallet
    {
        [Serializable]
        public class CurrencyEntry
        {
            [SerializeField, Currency] private string currencyName = "Coins";
            [SerializeField] private float defaultValue = 100f;
            [SerializeField] private float currentValue;

            public string CurrencyName => currencyName;
            public float DefaultValue => defaultValue;
            public float CurrentValue { get => currentValue; internal set => currentValue = value; }
        }

        [Tooltip("Defines the currency names available in the editor dropdowns. Falls back to the Resources/CurrencyDatabase asset when null.")]
        [SerializeField] private CurrencyDatabase database;
        [SerializeField] private List<CurrencyEntry> currencies = new List<CurrencyEntry>();

        public event Action<string, float> OnCurrencyChanged;

        /// <summary>The database this wallet resolves currency names against (may be null — entries are stored by name regardless).</summary>
        public CurrencyDatabase Database => database;

        void Awake()
        {
            ResetToDefaults();
        }

        public void ResetToDefaults()
        {
            foreach (CurrencyEntry entry in currencies)
            {
                if (entry == null) continue;

                SetEntryValue(entry, entry.DefaultValue);
            }

            WarnOnDuplicateTypes();
        }

        public bool HasCurrency(string name, float amount)
        {
            if (amount < 0f)
            {
                Debug.LogWarning($"HasCurrency called with a negative amount on '{gameObject.name}'.", this);
                return false;
            }

            return TryGetCurrency(name, out float current) && current >= amount;
        }

        public float GetCurrency(string name)
        {
            return TryGetCurrency(name, out float amount) ? amount : 0f;
        }

        public bool TryGetCurrency(string name, out float amount)
        {
            CurrencyEntry entry = FindEntry(name);
            if (entry == null)
            {
                amount = 0f;
                return false;
            }

            amount = entry.CurrentValue;
            return true;
        }

        public bool TryAddCurrency(string name, float amount)
        {
            if (amount < 0f)
            {
                Debug.LogWarning($"TryAddCurrency on '{gameObject.name}' rejected a negative amount ({name}: {amount}). Use TryRemoveCurrency to remove currency.", this);
                return false;
            }

            CurrencyEntry entry = FindEntry(name);
            if (entry == null)
            {
                Debug.LogWarning($"TryAddCurrency on '{gameObject.name}' failed: currency '{name}' is not configured on this wallet. Add it in the Inspector.", this);
                return false;
            }

            SetEntryValue(entry, entry.CurrentValue + amount);
            return true;
        }

        public bool TryRemoveCurrency(string name, float amount)
        {
            if (amount < 0f)
            {
                Debug.LogWarning($"TryRemoveCurrency on '{gameObject.name}' rejected a negative amount ({name}: {amount}). Use TryAddCurrency to add currency.", this);
                return false;
            }

            CurrencyEntry entry = FindEntry(name);
            if (entry == null)
            {
                Debug.LogWarning($"TryRemoveCurrency on '{gameObject.name}' failed: currency '{name}' is not configured on this wallet. Add it in the Inspector.", this);
                return false;
            }

            if (entry.CurrentValue < amount)
            {
                Debug.LogWarning($"TryRemoveCurrency on '{gameObject.name}' failed: not enough {name} ({entry.CurrentValue} available, {amount} requested).", this);
                return false;
            }

            SetEntryValue(entry, entry.CurrentValue - amount);
            return true;
        }

        public bool TrySetCurrency(string name, float amount)
        {
            if (amount < 0f)
            {
                Debug.LogWarning($"TrySetCurrency on '{gameObject.name}' rejected a negative amount ({name}: {amount}).", this);
                return false;
            }

            CurrencyEntry entry = FindEntry(name);
            if (entry == null)
            {
                Debug.LogWarning($"TrySetCurrency on '{gameObject.name}' failed: currency '{name}' is not configured on this wallet. Add it in the Inspector.", this);
                return false;
            }

            SetEntryValue(entry, amount);
            return true;
        }

        public IReadOnlyList<string> GetSupportedCurrencyTypes()
        {
            List<string> names = new List<string>(currencies.Count);
            foreach (CurrencyEntry entry in currencies)
            {
                if (entry == null || string.IsNullOrEmpty(entry.CurrencyName)) continue;

                names.Add(entry.CurrencyName);
            }

            return names;
        }

        CurrencyEntry FindEntry(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            foreach (CurrencyEntry entry in currencies)
            {
                if (entry != null && string.Equals(entry.CurrencyName, name, StringComparison.Ordinal))
                    return entry;
            }

            return null;
        }

        void SetEntryValue(CurrencyEntry entry, float newValue)
        {
            entry.CurrentValue = newValue;
            OnCurrencyChanged?.Invoke(entry.CurrencyName, newValue);
        }

        void WarnOnDuplicateTypes()
        {
            HashSet<string> seenNames = new HashSet<string>();
            foreach (CurrencyEntry entry in currencies)
            {
                if (entry == null || string.IsNullOrEmpty(entry.CurrencyName)) continue;

                if (!seenNames.Add(entry.CurrencyName))
                {
                    Debug.LogWarning($"Wallet on '{gameObject.name}' has duplicate currency '{entry.CurrencyName}' in its Inspector list. Only the first entry is used.", this);
                }
            }
        }
    }
}
