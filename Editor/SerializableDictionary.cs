using System;
using System.Collections.Generic;
using UnityEngine;

namespace MaliOC.Editor
{
    [Serializable]
    public class SerializableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, ISerializationCallbackReceiver
    {
        [SerializeField] private List<TKey> keys = new();
        [SerializeField] private List<TValue> values = new();

        public void OnBeforeSerialize()
        {
            keys.Clear();
            values.Clear();
            foreach (KeyValuePair<TKey, TValue> pair in this)
            {
                keys.Add(pair.Key);
                values.Add(pair.Value);
            }
        }

        public void OnAfterDeserialize()
        {
            Clear();

            if (keys.Count != values.Count)
            {
                Debug.LogError($"There are {keys.Count} keys and {values.Count} values after deserialization!");
            }

            var count = Mathf.Min(keys.Count, values.Count);
            for (int i = 0; i < count; i++)
            {
                Add(keys[i], values[i]);
            }
        }
    }
}