using System;
using System.Collections.Generic;
using UnityEngine;

namespace GirlsDevGames.MassiveAI {
public class GHOST_Logic<TState> where TState : Enum
{
	// Global-static
	public static readonly TState AnyState = (TState)(object)-1;
	public static readonly TState InvalidState = (TState)(object)-100;
	
		
	// State-Machine
	private StateMachine<GHOST_Logic<TState>> _state_machine;
	
	// Trigger-Map
	private Dictionary<TState, System.Func<bool>> _triggers_map;
	
	// State to corresponding Utility functions map
	private Dictionary<TState, System.Func<float>> _utility_map;
	
	// Transitions and sub-transitions
	private Dictionary<TState, TransitionsData> _transitionsMap;
	private Dictionary<TState, GHOST_Logic<TState>> _subTransitionsMap;
	
	// State, as a result of a trigger being fired.
	private TState _primary_state;  

	// Active state
	private TState _active_state;
	
	// Set this to true after finalizing, see Build method.
	private bool is_built = false;
	
	// Debugging and error handling
	private bool _has_error = false;

	
	// Constructor
	public GHOST_Logic(
		TState initial_state,
		Dictionary<TState, System.Func<float>> utility_map = null,
		StateMachine<GHOST_Logic<TState>> state_machine = null,
		System.Action<int, int> methodChangeCallback = null)
	{
		if (state_machine == null) _state_machine = new();
		else _state_machine = state_machine;
		
		if (utility_map == null) _utility_map = new();
		else _utility_map = utility_map;
		
		if (methodChangeCallback != null)
			_state_machine.AddOnMethodSwitchCallback( methodChangeCallback );

		_primary_state = initial_state;
		_active_state = initial_state;
		
		_transitionsMap = new();
		_subTransitionsMap = new();
		
		// do same for trigger map as _utility_map
		_triggers_map = new();
	}
		
	public TransitionBuilder Begin_Transition_Defs()
	{
		var context = new TransitionContext
		{ GHOST_Logic = this };
		
		return new TransitionBuilder(context);
	}

	public void AddTransition(Transition transition)
	{
		if (!HasTransitions(transition.FromState))
			_transitionsMap[transition.FromState] = new(new());

		// Prevent duplicates
		var transitionsData = _transitionsMap[transition.FromState];
		foreach (var existing in transitionsData.Transitions)
		{
			if (EqualityComparer<TState>.Default.Equals(existing.ToState, transition.ToState))
			{
				Debug.LogError($"Transition from '{transition.FromState}' to '{transition.ToState}' already exists!");
				return;
			}
		}

		transitionsData.Add(transition);

		Debug.LogFormat("Added Transition To: {0} FromState: {1}", 
			transition.ToState, transition.FromState);
	}
	
	public void AddSubLogicBlock(TState parent, GHOST_Logic<TState> subBlock)
	{ 
		if (_subTransitionsMap.ContainsKey(parent))
		{
			Debug.LogError($"A sub transitions map from '{parent}' already exists!");
			return;
		}
	
		_subTransitionsMap[parent] = subBlock;
	}

	public bool RegisterTrigger(TState state, System.Func<bool> trigger)
	{
		if (_triggers_map.ContainsKey(state))
		{ 
			Debug.LogError($"Trigger '{trigger}' already exists!");
			return false;
		}

		_triggers_map[state] = trigger;
		return true;
	}
	
	public void RegisterUtilityFucn(TState state, Func<float> utility_func)
	{
		if (!_utility_map.ContainsKey(state))
			_utility_map[state] = utility_func;
		else
			Debug.LogErrorFormat("Utility Function for State: {0} already exists", state);
	}
	
	public void TriggerState(TState state)
	{
		// No action if primary or active state same as requeted state.
		if (_primary_state.Equals(state) || _active_state.Equals(state))
			return;

		// We fire a State by setting it's trigger to true.
		if (_triggers_map.ContainsKey( state ))
		{			
			FireState(state);
		}
		else {
			Debug.LogWarning($"Unable to fire state '{state.ToString()}', trigger for this state is not registered.");
		}
	}
	
	public void FireState(TState state)
	{
		// No action if primary or active state same as requeted state.
		if (_primary_state.Equals(state) || _active_state.Equals(state))
			return;

		// Clear previously evaluated states
		_evaluated_states.Clear();
		
		// Update data
		_active_state = state;
		_primary_state = state;
		_evaluated_states.Add(state);
		
		// Log
		Debug.Log($"State '{state.ToString()}' fired. Switching to State: {state}");
	}
	
	public void Build()
	{
		if (_transitionsMap == null || _transitionsMap.Count == 0) {
			Debug.LogError("No transitions defined!");
			return;
		}
		
		// If state does not exists.
		if (!_state_machine.HasState((int)(object)_active_state)) {
			Debug.LogError($"State at index {(int)(object)_active_state} does not exists!");
			return;
		}
		// ---------------------------------------------------------- //
		
		_state_machine.SetOwner(this);
		_state_machine.SwitchState((int)(object)_active_state);
		
		is_built = true; 
	}
	
	private bool _states_evaluated = false;
	private List<TState> _evaluated_states = new();
	
	public void Update()
	{			
		/*	
		if (_has_error) {
			Debug.LogError(_error_msg);
			return;
		}
		*/
		
		if (!is_built) {
			Build();
			return;
		}
		
		if(_states_evaluated) {
			if (_evaluated_states.Count > 0) {
				if (!_state_machine.IsInTransition()) {
					TState state = _evaluated_states[0];
					_evaluated_states.RemoveAt(0);
					_active_state = state;
					
					// If state does not exists.
					if (!_state_machine.HasState((int)(object)state)) {
						Debug.LogError($"State at index {(int)(object)state} does not exists!");
						return;
					}
					// ---------------------------------------------- //
					
					_state_machine.SwitchState((int)(object)state);
				}
			} else {
				_evaluated_states.Clear();
				_states_evaluated = false;
			}
			
			_state_machine.Update();
			return;
		}
		
		// ---------------------------------------------------------- //
		// Evaluate triggers
		foreach (var (state, trigger) in _triggers_map)
		{
			// State is only triggered once, even if trigger return true
			// continiusly.
			if (trigger()) TriggerState(state);
		} 
		// ---------------------------------------------------------- //
		
		TState next_state = _active_state;
		TState prev_state;

		do
		{
			prev_state = next_state;
			next_state = Evaluate(prev_state, _evaluated_states);
		} while (!EqualityComparer<TState>.Default.Equals(prev_state, next_state));

		_states_evaluated = true;
	}

	public TState Evaluate(
		TState from_state,
		List<TState> states = null)
	{
		if (_transitionsMap == null || _transitionsMap.Count == 0) {
			Debug.LogError("TransitionsMap is null or no transitions defined!");
			return InvalidState;
		}

		float bestScore;
		bool all_conditions_failed;
		TransitionsData transitionsData = null;
		Transition best_transition = null;

		// ------------------------------------------------------------ //
		// Recursive Evaluation in Sub-States
		if (_subTransitionsMap != null && _subTransitionsMap.TryGetValue(from_state, out var subBlock))
		{
			var evaluated_state = subBlock.Evaluate(from_state, states);

			// Keep evaluating as long as there is a deeper sub-block
			while (subBlock._subTransitionsMap != null &&
				   subBlock._subTransitionsMap.TryGetValue(evaluated_state, out var deeperSubBlock))
			{
				subBlock = deeperSubBlock;
				evaluated_state = subBlock.Evaluate(evaluated_state, states);
			}

			return evaluated_state;
		}

		// ------------------------------------------------------------ //
		// Local transitions
		if (GetTransitionsData(from_state, out transitionsData))
		{
			best_transition = GetBestTransition(transitionsData.Transitions, out bestScore);
			if (best_transition != null)
			{
				// Debug.Log($"Transitioning from {from_state} to {best_transition.ToState} with score {bestScore}");
				if (!EqualityComparer<TState>.Default.Equals(best_transition.ToState, _active_state))
				{
					states.Add(best_transition.ToState);
					Debug.LogFormat("State switched to {0}", best_transition.ToState);
				}
				return best_transition.ToState;
			}
		}
		
		// Uncomment to see if there was no transition for this update step.
		// Debug.Log("No transition.");
		return from_state;
	}

	private Transition GetBestTransition(
		List<Transition> transitions,
		out float max_score) {
		max_score = 0f;
		
		Transition best_transition = null;
		float bestScore = float.MinValue;
		
		foreach(var transition in transitions)
		{
			// iter all transition utility functions defined from
			// transition.from_state
			if (_utility_map.TryGetValue(transition.ToState, out var func))
			{
				float score = func();
				if (score > bestScore)
				{
					bestScore = score;
					best_transition = transition;
				}
			} else {
				Debug.LogFormat( "Not Found: {0}", transition.ToState.ToString() );
			}
		}

		return best_transition;
	}

	public StateMachine<GHOST_Logic<TState>> GetSM()
	{ return _state_machine; }
	
	public TState GetActiveState() { return _active_state; }
		
	public TransitionsData GetTransitions(TState fromState) 
	{ return _transitionsMap[fromState]; }
		
	public bool GetTransitionsData(TState key, out TransitionsData data)
	{ return _transitionsMap.TryGetValue(key, out data); }
		
	public Dictionary<TState, System.Func<bool>> GetTriggersMap()
	{ return _triggers_map; }
	
	public Dictionary<TState, System.Func<float>> GetUtilityMap()
	{ return _utility_map; }
	
	public bool HasTransitions(TState key)
	{ return _transitionsMap.ContainsKey(key); }
	
	// ------------------------------------------------------------------------------------------- //
	// ---------------------------- Transitions Data --------------------------------------------- //
	// ------------------------------------------------------------------------------------------- //
	
	public class TransitionsData
	{
		public List<Transition> Transitions = null;

		public TransitionsData(List<Transition> transitions)
		{ Transitions = transitions; }

		public void Add(Transition transition)
		{ Transitions.Add(transition); }
	}
	
	// ------------------------------------------------------------------------------------------- //
	// ---------------------------- Transition Definitions Builder ------------------------------- //
	// ------------------------------------------------------------------------------------------- //        
	
	public class TransitionContext
	{
		// variables
		private TState _from_state = GHOST_Logic<TState>.AnyState;
		private TState _to_state = GHOST_Logic<TState>.AnyState;
		
		// methods
		public TState GetFromState() { return _from_state; }
		public TState GetToState() { return _to_state; }
		
		public void SetFromState(TState from_state)
		{ _from_state = from_state; }
		
		public void SetToState(TState to_state)
		{
			if (!EqualityComparer<TState>.Default.Equals(_to_state, to_state))
			{
				if (!EqualityComparer<TState>.Default.Equals(_to_state, GHOST_Logic<TState>.AnyState))
				{
					// Create and add transition
					var transition = new Transition(
						GetFromState(),
						GetToState()
					);

					GHOST_Logic.AddTransition(transition);

					// Reset
					_to_state = GHOST_Logic<TState>.AnyState;
				}
			}
			
			if (!EqualityComparer<TState>.Default.Equals(to_state, GHOST_Logic<TState>.AnyState))
			{ _to_state = to_state; }
		}

		public GHOST_Logic<TState> GHOST_Logic { get; set; } = null;
		public TransitionContext   ParentTransitionContext { get; set; } = null;
		public TransitionBuilder   TransitionBuilder { get; set; } = null;
		public ToBuilder           ToBuilder { get; set; } = null;
	}
	
	public class TransitionBuilderBase
	{
		protected readonly TransitionContext _context;
		public TransitionBuilderBase(TransitionContext context) { _context = context; }
		public TransitionContext GetContext() => _context;
	}
	
	public class TransitionBuilder : TransitionBuilderBase
	{
		public TransitionBuilder(TransitionContext context) : base(context)
		{ _context.TransitionBuilder = this; }
		
		public ToBuilderBase From(TState from_state)
		{ _context.SetFromState(from_state); return new ToBuilderBase(_context); }

		public GHOST_Logic<TState> x(TransitionContext context = null)
		{
			if (context == null) context = _context;
			
			// Traverse to root
			while (context.ParentTransitionContext != null)
			{ context = context.ParentTransitionContext; }

			return context.GHOST_Logic;
		}
		
		public void x() {}
	}

	public class ToBuilderBase : TransitionBuilderBase
	{		
		public ToBuilderBase(TransitionContext context) 
		: base(context) {}
		
		public virtual TransitionRouter To(TState state)
		{ 
			_context.SetToState(state);
			return new TransitionRouter(_context);
		}
	}
	
	public class ToBuilder : ToBuilderBase
	{		
		public ToBuilder(TransitionContext context) 
		: base(context) { _context.ToBuilder = this; }
		
		public virtual TransitionBuilder end_from() 
		{ 
			return _context.TransitionBuilder;
		}
	}
	
	public class TransitionRouter : ToBuilder
	{
		public TransitionRouter(TransitionContext context) 
		: base(context) {}
		
		public override TransitionRouter To(TState state)
		{ return base.To(state); }
		
		public ToBuilderBase sub_transitions()
		{
			TState active_to_state = _context.GetToState();
			_context.SetToState(GHOST_Logic<TState>.AnyState);

			// Start new transition block for sub-transitions
			var sub_context = new TransitionContext();
			sub_context.SetFromState(active_to_state);
			sub_context.GHOST_Logic = new GHOST_Logic<TState>(
				active_to_state,
				utility_map  : _context.GHOST_Logic.GetUtilityMap(),
				state_machine: _context.GHOST_Logic.GetSM()
			);
			sub_context.ParentTransitionContext = _context;
			sub_context.TransitionBuilder = _context.TransitionBuilder;
			sub_context.ToBuilder = new ToBuilder(sub_context);

			// Register the sub-transition block
			_context.GHOST_Logic.AddSubLogicBlock(
				active_to_state,
				sub_context.GHOST_Logic);

			// Restrict chaining again
			return new ToBuilderBase(sub_context);
		}

		public ToBuilder x()
		{
			_context.SetToState(GHOST_Logic<TState>.AnyState);
			
			// Return parent
			if (_context.ParentTransitionContext != null)
			{ return _context.ParentTransitionContext.ToBuilder; }

			return _context.ToBuilder;
		}
		
		public override TransitionBuilder end_from()
		{
			_context.SetToState(GHOST_Logic<TState>.AnyState);
			return base.end_from();
		}
	}
	
	public class Transition
	{
		public TState FromState { get; }
		public TState ToState   { get; }

		public Transition(
			TState fromState,
			TState toState)
		{
			FromState = fromState;
			ToState = toState;
		}
	}
}}
