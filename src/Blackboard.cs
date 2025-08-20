using System;
using System.Collections.Generic;

namespace GirlsDevGames.MassiveAI
{
    public enum BlackboardValueType
    {
        Trigger,
        Condition,
        Evaluation
    }

	public struct BlackboardEntry
	{
		public BlackboardValueType Type { get; }
		public object Value { get; }

		public BlackboardEntry(BlackboardValueType type, object value)
		{
			Type = type;
			Value = value;
		}
	}

    public class Blackboard
    {
        private readonly Dictionary<string, BlackboardEntry> _entries = new();

        // Indexer: get or set values safely by key
        public object this[string key]
        {
            get
            {
                if (_entries.TryGetValue(key, out var entry))
                    return entry.Value;

                throw new KeyNotFoundException($"Key '{key}' not found in Blackboard.");
            }
            set
            {                  
				if (!_entries.ContainsKey(key)) {
					if (value is bool b) 
						_entries[key] = new (
							BlackboardValueType.Condition,
							b);
					else if(value is float f)
						_entries[key] = new (
							BlackboardValueType.Evaluation,
							f);
					else if(value is float i)
						_entries[key] = new (
							BlackboardValueType.Evaluation,
							(float)i);		
				}

                var entry = _entries[key];

                switch (entry.Type)
                {
                    case BlackboardValueType.Trigger:
                    case BlackboardValueType.Condition:
                        if (value is bool b)
                            _entries[key] = new BlackboardEntry(entry.Type, b);
                        else
                            throw new ArgumentException($"Key '{key}' expects a bool.");
                        break;

                    case BlackboardValueType.Evaluation:
                        if (value is float f)
                            _entries[key] = new BlackboardEntry(entry.Type, f);
                        else if (value is int i)
                            _entries[key] = new BlackboardEntry(entry.Type, (float)i);
                        else
                            throw new ArgumentException($"Key '{key}' expects a float.");
                        break;

                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        // Add methods (explicit for clarity)
        public void AddTrigger(string key)    => _entries[key] = new(BlackboardValueType.Trigger, false);
        public void AddCondition(string key)  => _entries[key] = new(BlackboardValueType.Condition, false);
        public void AddEvaluation(string key) => _entries[key] = new(BlackboardValueType.Evaluation, 0f);

        // Update multiple values at once
        public void Update(Dictionary<string, object> updates)
        {
            foreach (var (key, val) in updates)
            {
                this[key] = val; // reuse indexer validation
            }
        }

        // Type-safe getter
        public T Get<T>(string key)
        {
            var obj = this[key];
            if (obj is T typed)
                return typed;

            throw new InvalidCastException($"Value for '{key}' is not of type {typeof(T).Name}");
        }

        // Safe TryGet
        public bool TryGet<T>(string key, out T value)
        {
            value = default;
            try
            {
                var obj = this[key];
                if (obj is T typed)
                {
                    value = typed;
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        // Optional: check if a key exists
        public bool Contains(string key) => _entries.ContainsKey(key);
    }
}
