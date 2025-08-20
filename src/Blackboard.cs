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

    public class Blackboard
    {
        private readonly Dictionary<string, bool> _bools = new();
        private readonly Dictionary<string, float> _floats = new();
        private readonly HashSet<string> _triggers = new();
        private readonly Dictionary<string, BlackboardValueType> _types = new();

        // Indexer: get or set values safely by key
        public object this[string key]
        {
            get
            {
                if (!_types.TryGetValue(key, out var type))
                    throw new KeyNotFoundException($"Key '{key}' not found in Blackboard.");

                return type switch
                {
                    BlackboardValueType.Trigger => _bools[key],
                    BlackboardValueType.Condition => _bools[key],
                    BlackboardValueType.Evaluation => _floats[key],
                    _ => throw new ArgumentOutOfRangeException()
                };
            }
            set
            {
                if (!_types.TryGetValue(key, out var type))
                {
                    // Auto-register type on first assignment
                    if (value is bool)
                    {
                        _types[key] = BlackboardValueType.Condition;
                        _bools[key] = (bool)value;
                    }
                    else if (value is float f)
                    {
                        _types[key] = BlackboardValueType.Evaluation;
                        _floats[key] = f;
                    }
                    else if (value is int i)
                    {
                        _types[key] = BlackboardValueType.Evaluation;
                        _floats[key] = i;
                    }
                    else
                    {
                        throw new ArgumentException($"Unsupported type for key '{key}'.");
                    }
                    return;
                }

                // Enforce type safety
                switch (type)
                {
                    case BlackboardValueType.Trigger:
                    case BlackboardValueType.Condition:
                        if (value is bool b)
                            _bools[key] = b;
                        else
                            throw new ArgumentException($"Key '{key}' expects a bool.");
                        break;

                    case BlackboardValueType.Evaluation:
                        if (value is float f)
                            _floats[key] = f;
                        else if (value is int i)
                            _floats[key] = i;
                        else
                            throw new ArgumentException($"Key '{key}' expects a float.");
                        break;

                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        // Add methods (explicit for clarity)
        public void AddTrigger(string key)
        {
            _types[key] = BlackboardValueType.Trigger;
            _bools[key] = false;
            _triggers.Add(key);
        }

        public void AddCondition(string key)
        {
            _types[key] = BlackboardValueType.Condition;
            _bools[key] = false;
        }

        public void AddEvaluation(string key)
        {
            _types[key] = BlackboardValueType.Evaluation;
            _floats[key] = 0f;
        }

        // Update multiple values at once
        public void Update(Dictionary<string, object> updates)
        {
            foreach (var (key, val) in updates)
                this[key] = val; // still goes through the indexer
        }

        // Reset by type
        public void Reset(BlackboardValueType entryType)
        {
            foreach (var (key, type) in _types)
            {
                if (type == entryType)
                {
                    switch (entryType)
                    {
                        case BlackboardValueType.Trigger:
                        case BlackboardValueType.Condition:
                            _bools[key] = false;
                            break;
                        case BlackboardValueType.Evaluation:
                            _floats[key] = 0f;
                            break;
                    }
                }
            }
        }
        
        public bool Remove(string key)
		{
			if (!_types.TryGetValue(key, out var type))
				return false;

			_types.Remove(key);

			switch (type)
			{
				case BlackboardValueType.Trigger:
				case BlackboardValueType.Condition:
					_bools.Remove(key);
					_triggers.Remove(key);
					break;
				case BlackboardValueType.Evaluation:
					_floats.Remove(key);
					break;
			}

			return true;
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
        public bool Contains(string key) => _types.ContainsKey(key);
    }
}
