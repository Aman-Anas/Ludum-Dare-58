namespace Utilities.Logic;

using System.Collections.Generic;
using Godot;

// Credit to GodotUtilities! https://github.com/firebelley/GodotUtilities/
// Modified a little bit to fit my naming conventions
public partial class DelegateStateMachine : RefCounted
{
    public delegate void State();

    // The current state of this state machine
    public State CurrentState { get; private set; }

    private readonly Dictionary<State, StateFlow> states = new();

    // Add a state to this machine
    public void AddState(State normal, State enterState = null, State leaveState = null)
    {
        states[normal] = new StateFlow(normal, enterState, leaveState);
    }

    // Change our current state to something else
    public void ChangeState(State toStateDelegate)
    {
        states.TryGetValue(toStateDelegate, out var stateDelegates);
        Callable.From(() => SetState(stateDelegates)).CallDeferred();
    }

    // Only use this for setting state initially, doesn't defer
    public void SetInitialState(State stateDelegate)
    {
        states.TryGetValue(stateDelegate, out var stateFlows);
        SetState(stateFlows);
    }

    public void Update()
    {
        CurrentState?.Invoke();
    }

    private void SetState(StateFlow stateFlows)
    {
        if (CurrentState != null)
        {
            states.TryGetValue(CurrentState, out var currentStateDelegates);
            // If we have a leave state, then run it
            currentStateDelegates?.LeaveState?.Invoke();
        }
        CurrentState = stateFlows.Normal;
        stateFlows?.EnterState?.Invoke();
    }

    private class StateFlow
    {
        public State Normal { get; }
        public State EnterState { get; }
        public State LeaveState { get; }

        public StateFlow(State normal, State enterState = null, State leaveState = null)
        {
            Normal = normal;
            EnterState = enterState;
            LeaveState = leaveState;
        }
    }
}
