using System;
using System.Collections;
using System.Collections.Generic;

namespace GirlsDevGames.MassiveAI
{
	public class Registry
	{
		public readonly Dictionary<string, bool>  Triggers    = new();
		public readonly Dictionary<string, bool>  Conditions  = new();
		public readonly Dictionary<string, float> Evaluations = new();
		
		public object this[string key]
		{
			get
			{
				if (Conditions.TryGetValue (key, out var b)) return b;
				if (Triggers.TryGetValue   (key, out var t)) return t;
				if (Evaluations.TryGetValue(key, out var f)) return f;
				throw new KeyNotFoundException($"Key '{key}' not found in registry.");
			}
			set
			{
				switch (value)
				{
					case bool b:
						if (Triggers.ContainsKey(key)) Triggers[key] = b;						
						else if (Conditions.ContainsKey(key)) Conditions[key] = b;
						else throw new KeyNotFoundException($"Key '{key}' not found in Registry.");
						break;
					case float f:
						if (Evaluations.ContainsKey(key)) Evaluations[key] = f;
						else throw new KeyNotFoundException($"Key '{key}' not found in Registry.");
						break;
					case int i:
						if (Evaluations.ContainsKey(key)) Evaluations[key] = i;
						else throw new KeyNotFoundException($"Key '{key}' not found in Registry.");
						break;
					default:
						throw new ArgumentException($"Unsupported value type: {value.GetType().Name}");
				}
			}
		}

		public void Update(Dictionary<string, object> updates)
		{
			foreach (var (key, val) in updates)
			{
				if      (val is bool b)  Conditions[key]  = b;
				else if (val is float f) Evaluations[key] = f;
				else if (val is int i)   Evaluations[key] = i;
			}
		}
		
		public T Get<T>(string key)
		{
			var obj = this[key];
			if (obj is T typed) return typed;
			throw new InvalidCastException($"Value for '{key}' is not of type {typeof(T).Name}");
		}

		public void AddTrigger(string key)
		{ Triggers[key] = false; }
		
		public void AddCondition(string key)
		{ Conditions[key] = false; }

		public void AddEvaluation(string key) 
		{ Evaluations[key] = 0f; }
	
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
	}
}
