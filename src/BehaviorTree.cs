using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GirlsDevGames.MassiveAI {
public class BehaviorTree {

// Base interface for all tickable nodes
public interface ITickable
{
	bool Tick();  // Could also return enum Status for richer BTs
	void Reset(); // so nodes can be re-used after success/fail
}

// Compositive Nodes
// Sequence: run children in order
public class Sequence : ITickable
{
    private readonly ITickable[] children;
    private int currentIndex = 0;

    public Sequence(params ITickable[] children) => this.children = children;

    public bool Tick()
    {
        while (currentIndex < children.Length)
        {
            if (!children[currentIndex].Tick())
                return false; // still running
            children[currentIndex].Reset();
            currentIndex++;
        }
        return true; // all succeeded
    }

    public void Reset() => currentIndex = 0;
}

// Selector: try children until one succeeds
public class Selector : ITickable
{
    private readonly ITickable[] children;
    private int currentIndex = 0;

    public Selector(params ITickable[] children) => this.children = children;

    public bool Tick()
    {
        while (currentIndex < children.Length)
        {
            if (children[currentIndex].Tick())
                return true; // success
            currentIndex++;
        }
        return false; // none succeeded
    }

    public void Reset() => currentIndex = 0;
}

// Parallel: succeed when ALL children succeed
public class Parallel : ITickable
{
    private readonly ITickable[] children;
    private readonly bool succeedOnFirst;

    public Parallel(bool succeedOnFirst, params ITickable[] children)
    {
        this.children = children;
        this.succeedOnFirst = succeedOnFirst;
    }

    public bool Tick()
    {
        bool allDone = true;
        foreach (var child in children)
        {
            if (!child.Tick())
            {
                allDone = false;
                if (!succeedOnFirst) return false;
            }
        }
        return allDone || succeedOnFirst;
    }

    public void Reset() { foreach (var c in children) c.Reset(); }
}

// Leaf Nodes
// Wraps a void action (always succeeds)
public class ActionNode : ITickable
{
    private readonly Action action;
    public ActionNode(Action action) => this.action = action;
    public bool Tick() { action(); return true; }
    public void Reset() { }
}

// Wraps a bool condition (success = true, fail = false)
public class ConditionNode : ITickable
{
    private readonly Func<bool> condition;
    public ConditionNode(Func<bool> condition) => this.condition = condition;
    public bool Tick() => condition();
    public void Reset() { }
}

// Wait node: succeed after X seconds
public class WaitNode : ITickable
{
    private readonly float duration;
    private float startTime;
    private bool started;

    public WaitNode(float seconds) => duration = seconds;

    public bool Tick()
    {
        if (!started) { startTime = Time.time; started = true; }
        return Time.time - startTime >= duration;
    }

    public void Reset() => started = false;
}

// Utility Selector: pick action with highest score
public class UtilitySelector : ITickable
{
    private readonly List<(Func<float> score, Action action)> actions;

    public UtilitySelector(params (Func<float>, Action)[] actions)
    {
        this.actions = new List<(Func<float>, Action)>(actions);
    }

    public bool Tick()
    {
        if (actions.Count == 0) return true;
        float best = float.MinValue;
        Action bestAction = null;
        foreach (var (score, action) in actions)
        {
            var s = score();
            if (s > best) { best = s; bestAction = action; }
        }
        bestAction?.Invoke();
        return true;
    }

    public void Reset() { }
}

// Decorators
public class Inverter : ITickable
{
    private readonly ITickable child;
    public Inverter(ITickable child) => this.child = child;

    public bool Tick() => !child.Tick();
    public void Reset() => child.Reset();
}

public class Repeater : ITickable
{
    private readonly ITickable child;
    public Repeater(ITickable child) => this.child = child;

    public bool Tick() { child.Tick(); return true; }
    public void Reset() => child.Reset();
}

public class RepeatUntil : ITickable
{
    private readonly ITickable child;
    private readonly bool untilSuccess;

    public RepeatUntil(ITickable child, bool untilSuccess = true)
    {
        this.child = child;
        this.untilSuccess = untilSuccess;
    }

    public bool Tick()
    {
        var result = child.Tick();
        return untilSuccess ? result : !result;
    }

    public void Reset() => child.Reset();
}

public class Cooldown : ITickable
{
    private readonly ITickable child;
    private readonly float cooldown;
    private float lastTime = -999f;

    public Cooldown(ITickable child, float cooldown)
    {
        this.child = child;
        this.cooldown = cooldown;
    }

    public bool Tick()
    {
        if (Time.time - lastTime < cooldown) return true; // still cooling
        if (child.Tick())
        {
            lastTime = Time.time;
            return true;
        }
        return false;
    }

    public void Reset() => child.Reset();
}

public class Limiter : ITickable
{
    private readonly ITickable child;
    private readonly int limit;
    private int counter = 0;

    public Limiter(ITickable child, int limit)
    {
        this.child = child;
        this.limit = limit;
    }

    public bool Tick()
    {
        if (counter >= limit) return true;
        if (child.Tick()) counter++;
        return counter >= limit;
    }

    public void Reset() { counter = 0; child.Reset(); }
}

// DSL Factory
public static class BT
{
    // Composites
    public static ITickable Seq(params ITickable[] nodes) => new Sequence(nodes);
    public static ITickable Sel(params ITickable[] nodes) => new Selector(nodes);
    public static ITickable Par(bool succeedOnFirst, params ITickable[] nodes) => new Parallel(succeedOnFirst, nodes);

    // Leaves
    public static ITickable Act(Action a) => new ActionNode(a);
    public static ITickable Cond(Func<bool> fn) => new ConditionNode(fn);
    public static ITickable Wait(float seconds) => new WaitNode(seconds);
    
    // Utility
    public static ITickable Util(params (Func<float>, Action)[] evals) => new UtilitySelector(evals);
    
    // Decorators
    public static ITickable Invert(ITickable node) => new Inverter(node);
	public static ITickable Repeat(ITickable node) => new Repeater(node);
	public static ITickable UntilSuccess(ITickable node) => new RepeatUntil(node, true);
	public static ITickable UntilFail(ITickable node) => new RepeatUntil(node, false);
	public static ITickable Cool(ITickable node, float sec) => new Cooldown(node, sec);
	public static ITickable Limit(ITickable node, int times) => new Limiter(node, times);

    // Example, Domain-specific sugar
    // public static ITickable PlayAnim(string animName) => Act(() => Debug.Log($"PlayAnim: {animName}"));
}
}}
