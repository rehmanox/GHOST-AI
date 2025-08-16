using System;
using System.Collections.Generic;
using UnityEngine;

namespace GirlsDevGames.MassiveAI
{
    public class Transition_Builder<TState> where TState : Enum
    {
        private static readonly TState AnyState = (TState)(object)-1;
        
        public static TransitionBlock Begin(TState current)
        { return new TransitionBlock(current); }
        
		public class TransitionBlock
		{
			public class TransitionsData
			{
				public List<Transition<TState>> Transitions = null;

				public TransitionsData(List<Transition<TState>> transitions)
				{ Transitions = transitions; }

				public void Add(Transition<TState> transition)
				{ Transitions.Add(transition); }
			}

            // state-machine
            public Registry registry;
            private StateMachine<AI_Agent> _state_machine;
            
            // trigger map
            private Dictionary<string, TState> _triggersMap;
            
            // transitions
			private Dictionary<TState, TransitionsData> _transitionsMap;
			private Dictionary<TState, TransitionBlock> _subTransitionsMap;
			
			// active state
			private TState _primary_state;  // state, as a result of 
											// a trigger being fired.
            private TState _active_state;

            // Constructor
			public TransitionBlock(
				TState current,
				StateMachine<AI_Agent> sm = null,
				Registry registry = null)
			{
				if (sm == null) _state_machine = new();
				else _state_machine = sm;
                
				if (registry == null) this.registry = new();
				else this.registry = registry;
                
                _primary_state = current;
                _active_state = current;
                
                _transitionsMap = new();
                _subTransitionsMap = new();
                _triggersMap = new();
			}
            
            public DefinitionsBuilder State_Definitions()
            { 
				return new DefinitionsBuilder(this);
			}
            
            public TransitionBuilder Transitions()
            {
				var context = new TransitionContext
				{ TransitionBlock = this };
				
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
            
            public void AddSubTransitionBlock(TState parent, TransitionBlock subBlock)
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
                registry.AddTrigger(trigger);
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
			
			public TransitionBlock Build(AI_Agent agent)
			{
                if (_transitionsMap == null || _transitionsMap.Count == 0)
                    throw new InvalidOperationException("No transitions defined.");

				foreach (KeyValuePair<TState, TransitionsData> kvp in _transitionsMap)
				{
					foreach(var transition in kvp.Value.Transitions)
						if (transition.Evaluations == null || transition.Conditions == null)
							throw new InvalidOperationException($"Transition from {transition.FromState} to {transition.ToState} is missing conditions and evaluations.");
				}
                
                _state_machine.SetOwner(agent);
                _state_machine.SwitchState((int)(object)_active_state); // Switch to a default state
                return this;
			}
			
			private bool _states_evaluated = false;
            private List<TState> _evaluated_states = new();
            
            public void Update()
            {				
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
                        if (registry.Triggers.TryGetValue(trigger, out bool triggerFired)
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
                            if (!registry.Conditions.TryGetValue(cond, out bool is_true) || !is_true)
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
                        if (registry.Evaluations.TryGetValue(evalKey, out float score))
                        {
                            curr_score += score;
                            total_evals++;
                        }
                        else
                        {
                            Debug.LogFormat("Evaluation {0} does not exist in registry!", evalKey);
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

			public StateMachine<AI_Agent> GetSM()
            { return _state_machine; }
            
            public Registry GetRegistry()
            { return registry; }

            public TState GetActiveState() { return _active_state; }
                
			public TransitionsData GetTransitions(TState fromState) 
            { return _transitionsMap[fromState]; }
                
			public bool GetTransitionsData(TState key, out TransitionsData data)
            { return _transitionsMap.TryGetValue(key, out data); }
                
			public Dictionary<string, TState> GetTriggersMap()
            { return _triggersMap; }
            
			public bool HasTransitions(TState key)
            { return _transitionsMap.ContainsKey(key); }
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

        // ------------------------------------------------------------------------------------------- //
        // ---------------------------- State Definitions Builder ------------------------------------ //
        // ------------------------------------------------------------------------------------------- //
 
        public class DefinitionsBuilder
        {
			protected TransitionBlock tblock;

            public DefinitionsBuilder(TransitionBlock tblock)
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
            public DefinitionsBuilderRouter(TransitionBlock tblock) 
            : base(tblock) {} 
            
            public TransitionBlock end_state_defs() { return tblock; }
        }

        // ------------------------------------------------------------------------------------------- //
        // ---------------------------- Transition Definitions Builder ------------------------------- //
        // ------------------------------------------------------------------------------------------- //        
        
		public class TransitionContext
		{
			// variables
			private TState _from_state = Transition_Builder<TState>.AnyState;
			private TState _to_state = Transition_Builder<TState>.AnyState;

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

						TransitionBlock.AddTransition(transition);

						// reset
						_conditions = Array.Empty<string>();
						_evaluations = Array.Empty<string>();
						_to_state = Transition_Builder<TState>.AnyState;
					}
				}
				
				if (!EqualityComparer<TState>.Default.Equals(to_state, Transition_Builder<TState>.AnyState))
				{ _to_state = to_state; }
			}

            public TransitionBlock   TransitionBlock { get; set; } = null;
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
            
            public TransitionBlock end_transition_defs(TransitionContext context = null)
            {
                if (context == null) context = _context;
                
                // Traverse to root
                while (context.ParentTransitionContext != null)
                { context = context.ParentTransitionContext; }

                return context.TransitionBlock;
            }
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
				_context.SetToState(Transition_Builder<TState>.AnyState);

                // Start new transition block for sub-transitions
                var sub_context = new TransitionContext();
                sub_context.SetFromState(active_to_state);
                sub_context.TransitionBlock = new TransitionBlock(
					active_to_state,
					_context.TransitionBlock.GetSM(),
					_context.TransitionBlock.GetRegistry());
                sub_context.ParentTransitionContext = _context;
                sub_context.TransitionBuilder = _context.TransitionBuilder;
                sub_context.ToBuilder = new ToBuilder(sub_context);

                // Register the sub-transition block
                _context.TransitionBlock.AddSubTransitionBlock(
					active_to_state,
					sub_context.TransitionBlock);

                // restrict chaining again
                return new ToBuilderBase(sub_context);
            }

			public ToBuilder end()
            {
				_context.SetToState(Transition_Builder<TState>.AnyState);
				
                // Return parent
                if (_context.ParentTransitionContext != null)
                { return _context.ParentTransitionContext.ToBuilder; }

                return _context.ToBuilder;
            }
            
			public override TransitionBuilder end_from()
			{
				_context.SetToState(Transition_Builder<TState>.AnyState);
                return base.end_from();
			}
        }
    }
}
