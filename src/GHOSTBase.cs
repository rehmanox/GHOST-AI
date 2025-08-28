using System;
using GirlsDevGames.MassiveAI;

public class GHOSTBase<TState> where TState : Enum
{
	public GHOST_Logic<TState> ghost_logic;
	
	public GHOSTBase(TState initial_state)
	{
        ghost_logic = new(
			initial_state,
			methodChangeCallback: OnMethodChange
		);
	}

	protected void Add_Def(
		TState state,
		Action Update,
		Func<bool> Enter=null,
		Func<bool> Exit=null)
	{
		Add_Def(state, null, Update, Enter, Exit);
	}
	
	protected void Add_Def(
		TState state,
		System.Func<bool> trigger,
		Action Update,
		Func<bool> Enter=null,
		Func<bool> Exit=null)
	{
		ghost_logic.GetSM().AddState(
			state.ToString(),
			(int)(object)state,
			Update,
			Enter,
			Exit);
		
		if (trigger != null)
			ghost_logic.RegisterTrigger(state, trigger);
	}
	
	protected void UtilityFor(TState state, Func<float> utility_func)
	{
		ghost_logic.RegisterUtilityFucn(state, utility_func);
	}
	
	protected virtual void OnMethodChange(int state_id, int method_id)
	{
	}
}
