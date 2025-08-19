using System;
using System.Collections.Generic;
using UnityEngine;

namespace GirlsDevGames.MassiveAI {
public class GHOST_Logic<TState> where TState : Enum
{
	// Global-static
	public static readonly TState AnyState = (TState)(object)-1;
	
	// Registry 
	private Blackboard blackboard;
	
	// State-Machine
	private StateMachine<GHOST_Logic<TState>> _state_machine;
	
	// Trigger-Map
	private Dictionary<string, TState> _triggersMap;
	
	// Transitions and sub-transitions
	private Dictionary<TState, TransitionsData> _transitionsMap;
	private Dictionary<TState, GHOST_Logic<TState>> _subTransitionsMap;
	
	// State, as a result of a trigger being fired.
	private TState _primary_state;  

	// Active state
	private TState _active_state;
	
	// Set this to true after finalizing, see Build method.
	private bool is_built = false;


	// Constructor
	public GHOST_Logic(
		TState initial_state,
		System.Action<int, int> methodChangeCallback = null,
		
		StateMachine<GHOST_Logic<TState>> state_machine = null,
		Blackboard blackboard = null)
	{
		if (state_machine == null) _state_machine = new();
		else _state_machine = state_machine;
		
		if (methodChangeCallback != null)
			_state_machine.AddOnMethodSwitchCallback( methodChangeCallback );
		
		if (blackboard == null) this.blackboard = new();
		else this.blackboard = blackboard;
		
		_primary_state = initial_state;
		_active_state = initial_state;
		
		_transitionsMap = new();
		_subTransitionsMap = new();
		_triggersMap = new();
	}
	
	public DefinitionsBuilder Begin_State_Defs()
	{ 
		return new DefinitionsBuilder(this);
	}
	
	public TransitionBuilder Begin_Transition_Defs()
	{
		var context = new TransitionContext
		{ GHOST_Logic = this };
		
		return new TransitionBuilder(context);
	}

	public void AddTransition(Transition<TState> transition)
	{
		if (!HasTransitions(transition.FromState))
			_transitionsMap[transition.FromState] = new(new());

		// Prevent duplicates
		var transitionsData = _transitionsMap[transition.FromState];
		foreach (var existing in transitionsData.Transitions)
		{
			if (EqualityComparer<TState>.Default.Equals(existing.ToState, transition.ToState))
			{
				Debug.Log("Dupe detected");
				throw new InvalidOperationException(
					$"A transition from '{transition.FromState}' to '{transition.ToState}' already exists.");
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
		{ throw new ArgumentException($"A sub transitions map from '{parent}' already exists."); }
	
		_subTransitionsMap[parent] = subBlock;
	}

	public void RegisterTrigger(string trigger, TState state)
	{
		if (_triggersMap.ContainsKey(trigger))
		{ throw new ArgumentException($"Trigger '{trigger}' is already exists."); }

		_triggersMap[trigger] = state;
		blackboard.AddTrigger(trigger);
	}

	public TState FireTrigger(string trigger)
	{
		if (_triggersMap.TryGetValue(trigger, out var state))
		{
			_active_state = state;
			return Evaluate(_active_state);
		}
		
		Debug.LogWarning($"Trigger '{trigger}' not registered.");
		return _active_state;
	}
	
	public GHOST_Logic<TState> Build()
	{
		if (_transitionsMap == null || _transitionsMap.Count == 0)
			throw new InvalidOperationException("No transitions defined.");

		foreach (KeyValuePair<TState, TransitionsData> kvp in _transitionsMap)
		{
			foreach(var transition in kvp.Value.Transitions)
				if (transition.Evaluations == null || transition.Conditions == null)
					throw new InvalidOperationException($"Transition from {transition.FromState} to {transition.ToState} is missing conditions and evaluations.");
		}
		
		_state_machine.SetOwner(this);
		_state_machine.SwitchState((int)(object)_active_state); // Switch to a default state
		
		is_built = true; 
		
		return this;
	}
	
	private bool _states_evaluated = false;
	private List<TState> _evaluated_states = new();
	
	public void Update()
	{				
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
					
					_state_machine.SwitchState((int)(object)state);
				}
			} else {
				_evaluated_states.Clear();
				_states_evaluated = false;
			}
			
			_state_machine.Update();
			return;
		}

		// Evaluate Triggers
		if (_triggersMap != null && _triggersMap.Count > 0)
		{
			foreach (var (trigger, trigger_state) in _triggersMap)
			{
				if (blackboard.TryGet(trigger, out bool triggerFired)
					&& triggerFired)
				{
					if (!EqualityComparer<TState>.Default.Equals(trigger_state, _primary_state))
					{
						Debug.Log($"Trigger '{trigger}' fired. Switching to State: {trigger_state}");
						
						// Clear previous.
						_evaluated_states.Clear();
						
						_active_state = trigger_state;
						_primary_state = trigger_state;
						_evaluated_states.Add(trigger_state);
					}
				}
			}
		}

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
		if (_transitionsMap == null || _transitionsMap.Count == 0)
			throw new InvalidOperationException("No transitions defined!");

		float bestScore;
		bool all_conditions_failed;
		TransitionsData transitionsData = null;
		Transition<TState> best_transition = null;

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

		// Debug.Log("No transition.");
		return from_state;
	}

	private Transition<TState> GetBestTransition(
		List<Transition<TState>> transitions,
		out float max_score) {
		max_score = 0f;
		
		Transition<TState> best_transition = null;
		
		// Evaluate conditions
		bool all_conditions_true = true;
		int highest_num_conditions = -1;
		
		foreach (var transition in transitions)
		{
			// Evaluate all conditions in the array, move to
			// evaluations only is all conditions of a
			// transition are true
			if (transition.Conditions != null && transition.Conditions.Length > 0)
			{
				all_conditions_true = true;
				
				// Evaluate if all conditions are true
				foreach (var cond in transition.Conditions)
				{
					if (!blackboard.TryGet(cond, out bool is_true) || !is_true)
					{
						all_conditions_true = false;
						break;
					}
				}
				
				if (!all_conditions_true)
					continue;
				
				// For cases where no Evaluations are defined,
				// we select the first transition as best
				// transition.
				if (best_transition == null)
					best_transition = transition;
			}

			// Evaluate scores
			float curr_score = 0f;
			int total_evals = 0;

			foreach (var evalKey in transition.Evaluations)
			{
				if (blackboard.TryGet(evalKey, out float score))
				{
					curr_score += score;
					total_evals++;
				}
				else
				{
					Debug.LogFormat("Evaluation {0} does not exist in blackboard!", evalKey);
				}
			}

			if (total_evals == 0)
				continue;

			float avg_score = curr_score / total_evals;

			if (avg_score > max_score)
			{
				max_score = avg_score;
				best_transition = transition;
			}
		}
		
		return best_transition;
	}

	public StateMachine<GHOST_Logic<TState>> GetSM()
	{ return _state_machine; }
	
	public Blackboard GetRegistry()
	{ return blackboard; }

	public TState GetActiveState() { return _active_state; }
		
	public TransitionsData GetTransitions(TState fromState) 
	{ return _transitionsMap[fromState]; }
		
	public bool GetTransitionsData(TState key, out TransitionsData data)
	{ return _transitionsMap.TryGetValue(key, out data); }
		
	public Dictionary<string, TState> GetTriggersMap()
	{ return _triggersMap; }
	
	public bool HasTransitions(TState key)
	{ return _transitionsMap.ContainsKey(key); }
	

	// ------------------------------------------------------------------------------------------- //
	// ---------------------------- Transitions Data --------------------------------------------- //
	// ------------------------------------------------------------------------------------------- //
	
	public class TransitionsData
	{
		public List<Transition<TState>> Transitions = null;

		public TransitionsData(List<Transition<TState>> transitions)
		{ Transitions = transitions; }

		public void Add(Transition<TState> transition)
		{ Transitions.Add(transition); }
	}
	
	// ------------------------------------------------------------------------------------------- //
	// ---------------------------- State Definitions Builder ------------------------------------ //
	// ------------------------------------------------------------------------------------------- //

	public class DefinitionsBuilder
	{
		protected GHOST_Logic<TState> tblock;

		public DefinitionsBuilder(GHOST_Logic<TState> tblock)
		{ this.tblock = tblock; }

		public DefinitionsBuilderRouter Add_Def(
			TState state,
			string trigger,
			Action Update,
			Func<bool> Enter=null,
			Func<bool> Exit=null)
		{
			tblock.GetSM().AddState(
				"",
				(int)(object)state,
				Update,
				Enter,
				Exit);

			tblock.RegisterTrigger(trigger, state);
			return new DefinitionsBuilderRouter(tblock);
		}
	}
	
	public class DefinitionsBuilderRouter : DefinitionsBuilder
	{
		public DefinitionsBuilderRouter(GHOST_Logic<TState> tblock) 
		: base(tblock) {} 
		
		public GHOST_Logic<TState> end_state_defs() { return tblock; }
		public void x() {}
	}

	// ------------------------------------------------------------------------------------------- //
	// ---------------------------- Transition Definitions Builder ------------------------------- //
	// ------------------------------------------------------------------------------------------- //        
	
	public class TransitionContext
	{
		// variables
		private TState _from_state = GHOST_Logic<TState>.AnyState;
		private TState _to_state = GHOST_Logic<TState>.AnyState;

		public string[] _conditions  = Array.Empty<string>();
		public string[] _evaluations = Array.Empty<string>();
		
		// methods
		public TState GetFromState() { return _from_state; }
		public TState GetToState() { return _to_state; }
		
		public void SetFromState(TState from_state)
		{ _from_state = from_state; }
		
		public void SetToState(TState to_state)
		{
			if (!EqualityComparer<TState>.Default.Equals(_to_state, to_state))
			{
				if (_conditions.Length > 0 || _evaluations.Length > 0)
				{
					var transition = new Transition<TState>(
						GetFromState(),
						GetToState(),
						_conditions,
						_evaluations
					);

					GHOST_Logic.AddTransition(transition);

					// reset
					_conditions = Array.Empty<string>();
					_evaluations = Array.Empty<string>();
					_to_state = GHOST_Logic<TState>.AnyState;
				}
			}
			
			if (!EqualityComparer<TState>.Default.Equals(to_state, GHOST_Logic<TState>.AnyState))
			{ _to_state = to_state; }
		}

		public GHOST_Logic<TState>   GHOST_Logic { get; set; } = null;
		public TransitionContext ParentTransitionContext { get; set; } = null;
		public TransitionBuilder TransitionBuilder { get; set; } = null;
		public ToBuilder         ToBuilder { get; set; } = null;
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
		
		public virtual ConditionsOrEvaluations To(TState state)
		{ 
			_context.SetToState(state);
			return new ConditionsOrEvaluations(_context);
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
	
	public class ConditionsOrEvaluations : TransitionBuilderBase
	{
		public ConditionsOrEvaluations(TransitionContext context) : base(context) {}
		
		public EvaluationsBuilder Conditions(params string[] conditions)
		{
			this._context._conditions = conditions;
			return new EvaluationsBuilder(this._context);
		}
		
		public ConditionsBuilder Evaluations(params string[] evaluations)
		{
			this._context._evaluations = evaluations;
			return new ConditionsBuilder(this._context);
		}
	}
	
	public class ConditionsBuilder : TransitionRouter
	{
		public ConditionsBuilder(TransitionContext context) : base(context) {}

		public TransitionRouter Conditions(params string[] conditions)
		{ 
			this._context._conditions = conditions;
			return new TransitionRouter(_context);
		}
	}
	
	public class EvaluationsBuilder : TransitionRouter
	{       
		public EvaluationsBuilder(TransitionContext context) : base(context) {}

		public TransitionRouter Evaluations(params string[] evaluations)
		{ 
			this._context._evaluations = evaluations;
			return new TransitionRouter(_context);
		}
	}

	public class TransitionRouter : ToBuilder
	{
		public TransitionRouter(TransitionContext context) 
		: base(context) {}
		
		public override ConditionsOrEvaluations To(TState state)
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
				state_machine: _context.GHOST_Logic.GetSM(),
				blackboard: _context.GHOST_Logic.GetRegistry()
			);
			sub_context.ParentTransitionContext = _context;
			sub_context.TransitionBuilder = _context.TransitionBuilder;
			sub_context.ToBuilder = new ToBuilder(sub_context);

			// Register the sub-transition block
			_context.GHOST_Logic.AddSubLogicBlock(
				active_to_state,
				sub_context.GHOST_Logic);

			// restrict chaining again
			return new ToBuilderBase(sub_context);
		}

		public ToBuilder end()
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
	
	public class Transition<T>
	{
		public T FromState { get; }
		public T ToState { get; }
		public string[] Conditions { get; }
		public string[] Evaluations { get; }

		public Transition(
			T fromState,
			T toState,
			string[] conditions,
			string[] evaluations)
		{
			FromState = fromState;
			ToState = toState;
			Conditions = conditions;
			Evaluations = evaluations;
		}
	}
}}
