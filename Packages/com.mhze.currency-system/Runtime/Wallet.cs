// Default wallet behaviour. Assigned to a GameObject that owns it. Stores a list of currency entries (type, default value, current value), resets to the default values on Awake, and implements IWallet for getting, adding, removing, and setting currency.

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
            [SerializeField] private CurrencyType currencyType = CurrencyType.Gold;
            [SerializeField] private float defaultValue = 100f;
            [SerializeField] private float currentValue;

            public CurrencyType CurrencyType => currencyType;
            public float DefaultValue => defaultValue;
            public float CurrentValue { get => currentValue; internal set => currentValue = value; }
        }

        [SerializeField] private List<CurrencyEntry> currencies = new List<CurrencyEntry>();

        public event Action<CurrencyType, float> OnCurrencyChanged;

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

        public bool HasCurrency(CurrencyType type, float amount)
        {
            if (amount < 0f)
            {
                Debug.LogWarning($"HasCurrency called with a negative amount on '{gameObject.name}'.", this);
                return false;
            }

            return TryGetCurrency(type, out float current) && current >= amount;
        }

        public float GetCurrency(CurrencyType type)
        {
            return TryGetCurrency(type, out float amount) ? amount : 0f;
        }

        public bool TryGetCurrency(CurrencyType type, out float amount)
        {
            CurrencyEntry entry = FindEntry(type);
            if (entry == null)
            {
                amount = 0f;
                return false;
            }

            amount = entry.CurrentValue;
            return true;
        }

        public bool TryAddCurrency(CurrencyType type, float amount)
        {
            if (amount < 0f)
            {
                Debug.LogWarning($"TryAddCurrency on '{gameObject.name}' rejected a negative amount ({type}: {amount}). Use TryRemoveCurrency to remove currency.", this);
                return false;
            }

            CurrencyEntry entry = FindEntry(type);
            if (entry == null)
            {
                Debug.LogWarning($"TryAddCurrency on '{gameObject.name}' failed: currency type '{type}' is not configured on this wallet. Add it in the Inspector.", this);
                return false;
            }

            SetEntryValue(entry, entry.CurrentValue + amount);
            return true;
        }

        public bool TryRemoveCurrency(CurrencyType type, float amount)
        {
            if (amount < 0f)
            {
                Debug.LogWarning($"TryRemoveCurrency on '{gameObject.name}' rejected a negative amount ({type}: {amount}). Use TryAddCurrency to add currency.", this);
                return false;
            }

            CurrencyEntry entry = FindEntry(type);
            if (entry == null)
            {
                Debug.LogWarning($"TryRemoveCurrency on '{gameObject.name}' failed: currency type '{type}' is not configured on this wallet. Add it in the Inspector.", this);
                return false;
            }

            if (entry.CurrentValue < amount)
            {
                Debug.LogWarning($"TryRemoveCurrency on '{gameObject.name}' failed: not enough {type} ({entry.CurrentValue} available, {amount} requested).", this);
                return false;
            }

            SetEntryValue(entry, entry.CurrentValue - amount);
            return true;
        }

        public bool TrySetCurrency(CurrencyType type, float amount)
        {
            if (amount < 0f)
            {
                Debug.LogWarning($"TrySetCurrency on '{gameObject.name}' rejected a negative amount ({type}: {amount}).", this);
                return false;
            }

            CurrencyEntry entry = FindEntry(type);
            if (entry == null)
            {
                Debug.LogWarning($"TrySetCurrency on '{gameObject.name}' failed: currency type '{type}' is not configured on this wallet. Add it in the Inspector.", this);
                return false;
            }

            SetEntryValue(entry, amount);
            return true;
        }

        public IReadOnlyList<CurrencyType> GetSupportedCurrencyTypes()
        {
            List<CurrencyType> types = new List<CurrencyType>(currencies.Count);
            foreach (CurrencyEntry entry in currencies)
            {
                if (entry == null) continue;

                types.Add(entry.CurrencyType);
            }

            return types;
        }

        CurrencyEntry FindEntry(CurrencyType type)
        {
            foreach (CurrencyEntry entry in currencies)
            {
                if (entry != null && entry.CurrencyType == type)
                    return entry;
            }

            return null;
        }

        void SetEntryValue(CurrencyEntry entry, float newValue)
        {
            entry.CurrentValue = newValue;
            OnCurrencyChanged?.Invoke(entry.CurrencyType, newValue);
        }

        void WarnOnDuplicateTypes()
        {
            HashSet<CurrencyType> seenTypes = new HashSet<CurrencyType>();
            foreach (CurrencyEntry entry in currencies)
            {
                if (entry == null) continue;

                if (!seenTypes.Add(entry.CurrencyType))
                {
                    Debug.LogWarning($"Wallet on '{gameObject.name}' has duplicate currency type '{entry.CurrencyType}' in its Inspector list. Only the first entry is used.", this);
                }
            }
        }
    }
}
