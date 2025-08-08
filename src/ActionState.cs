using System;
using System.Collections.Generic;

namespace GirlsDevGames.MassiveAI
{
    public class ActionState<T> : StateBase<T>
    {
        // --- Types for user-defined actions ---
        public delegate bool  StateAction();  // For Enter and Exit
        public delegate float ScoredAction(); // For Update

        private readonly Queue<StateAction> enterActions = new();
        private readonly Queue<StateAction> exitActions = new();
        private readonly List<ScoredAction> updateActions = new();

        private Queue<StateAction> enterExecutionQueue;
        private Queue<StateAction> exitExecutionQueue;

        private bool enterPhaseDone = false;
        private bool exitPhaseDone = false;
        
        // Constructors
        public ActionState(string name = "") : 
			base(default, name) {}
			
        public ActionState(T owner, string name = "") :
			base(owner, name) {}

        public override void Reset()
        {
            enterExecutionQueue = null;
            exitExecutionQueue = null;
            enterPhaseDone = false;
            exitPhaseDone = false;
        }

        public void AddEnterAction(StateAction action) 
        => enterActions.Enqueue(action);
        
        public void AddExitAction(StateAction action) 
        => exitActions.Enqueue(action);
        
        public void AddUpdateAction(ScoredAction action)
        => updateActions.Add(action);

        public override bool Enter()
        {
            // First call: initialize queue
            if (enterExecutionQueue == null)
            {
                enterExecutionQueue = new Queue<StateAction>(enterActions);
                enterPhaseDone = false;
            }

            if (enterPhaseDone) return true;

            while (enterExecutionQueue.Count > 0)
            {
                var currentAction = enterExecutionQueue.Peek();
                if (currentAction()) enterExecutionQueue.Dequeue(); // Done, move to next
                else return false; // Still in progress
            }

            enterPhaseDone = true;
            return true;
        }

        public override bool Exit()
        {
            // First call: initialize queue
            if (exitExecutionQueue == null)
            {
                exitExecutionQueue = new Queue<StateAction>(exitActions);
                exitPhaseDone = false;
            }

            if (exitPhaseDone) return true;

            while (exitExecutionQueue.Count > 0)
            {
                var currentAction = exitExecutionQueue.Peek();
                if (currentAction()) exitExecutionQueue.Dequeue(); // Done, move to next
                else return false; // Still in progress
            }

            exitPhaseDone = true;
            return true;
        }

        public override void Update()
        {
            if (updateActions.Count == 0) return;

            float bestScore = float.MinValue;
            ScoredAction bestAction = null;

            foreach (var action in updateActions)
            {
                float score = action();
                if (score > bestScore)
                {
                    bestScore = score;
                    bestAction = action;
                }
            }

            bestAction?.Invoke(); // Execute the best scoring action
        }
    }
}
