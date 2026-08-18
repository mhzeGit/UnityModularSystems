// ScriptableObject that defines which currency types exist for the whole game.
// Add currency types as plain strings — every currency dropdown in the editor
// and every runtime lookup resolves against this list, so no enum edits are needed.

using System.Collections.Generic;
using UnityEngine;

namespace MHZE.CurrencySystem
{
    [CreateAssetMenu(fileName = "CurrencyDatabase", menuName = "MHZE/Currency Database")]
    public class CurrencyDatabase : ScriptableObject
    {
        [Tooltip("All supported currency types. Add a new entry to make it selectable everywhere.")]
        [SerializeField] private List<string> currencyTypes = new List<string> { "Coins" };

        private Dictionary<string, int> lookup;
        private bool dirty = true;

        private static CurrencyDatabase defaultInstance;

        /// <summary>
        /// The project-wide database loaded from a Resources/CurrencyDatabase asset,
        /// or explicitly assigned. Used by runtime lookups and inspector dropdowns
        /// when a component does not carry its own database reference.
        /// </summary>
        public static CurrencyDatabase Default
        {
            get
            {
                if (defaultInstance == null)
                    defaultInstance = Resources.Load<CurrencyDatabase>("CurrencyDatabase");
                return defaultInstance;
            }
            set => defaultInstance = value;
        }

        public int Count => currencyTypes != null ? currencyTypes.Count : 0;

        public string DefaultName => Count > 0 ? currencyTypes[0] : null;

        public IReadOnlyList<string> Names => currencyTypes;

        public string GetName(int index)
        {
            return index >= 0 && index < Count ? currencyTypes[index] : null;
        }

        public bool Contains(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            EnsureLookup();
            return lookup.ContainsKey(name);
        }

        public int GetIndex(string name)
        {
            if (string.IsNullOrEmpty(name))
                return -1;

            EnsureLookup();
            return lookup.TryGetValue(name, out int index) ? index : -1;
        }

        private void EnsureLookup()
        {
            if (lookup == null || dirty)
                RebuildLookup();
        }

        private void RebuildLookup()
        {
            if (lookup == null)
                lookup = new Dictionary<string, int>();

            lookup.Clear();
            if (currencyTypes != null)
            {
                for (int i = 0; i < currencyTypes.Count; i++)
                {
                    string name = currencyTypes[i];
                    if (!string.IsNullOrEmpty(name) && !lookup.ContainsKey(name))
                        lookup[name] = i;
                }
            }

            dirty = false;
        }

        private void OnValidate()
        {
            dirty = true;
        }
    }
}
