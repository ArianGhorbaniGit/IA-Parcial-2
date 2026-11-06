using System;
using System.Collections.Generic;
using UnityEngine;

public class FiniteStateMachine<T>
{
    private readonly Dictionary<T, State<T>> _allStates = new();
    private readonly List<GlobalTransition> _globalTransitions = new();
    private State<T> _currentState;

    public T CurrentState { get; private set; }
    public T PreviousState { get; private set; }
    public bool InValidState => _currentState != null;
    public string CurrentStateName => InValidState ? _currentState.Name : "Sin estado";
    public string PreviousStateName { get; private set; } = "-";
    public string LastReason { get; private set; } = "";
    public float TimeInState { get; private set; }

    //Avisa cada cambio de estado (nuevo estado, motivo). Lo uso para el feedback
    public event Action<T, string> OnStateChanged = delegate { };

    private struct GlobalTransition
    {
        public T To;
        public Func<bool> Condition;
        public string Reason;
        public T[] Except;
    }

    public FiniteStateMachine(params (T, State<T>)[] states)
    {
        AddStates(states);
    }

    public void AddState(T elem, State<T> state)
    {
        _allStates[elem] = state;
    }

    public void AddStates(params (T, State<T>)[] states)
    {
        foreach ((T, State<T>) item in states)
        {
            _allStates[item.Item1] = item.Item2;
        }
    }

    //Transicion que se puede dar desde cualquier estado (por ejemplo Dead o Flee).
    //Se chequean antes que el estado actual y en el orden en que se agregan (orden = prioridad).
    public void AddGlobalTransition(T to, Func<bool> condition, string reason, params T[] except)
    {
        _globalTransitions.Add(new GlobalTransition { To = to, Condition = condition, Reason = reason, Except = except });
    }

    public void ChangeState(T state, string reason = "")
    {
        if (!_allStates.ContainsKey(state))
        {
            Debug.LogError(state + " - Target State was not present on the state machine!");
            return;
        }

        PreviousStateName = InValidState ? _currentState.Name : "-";
        _currentState?.Exit();

        PreviousState = CurrentState;
        CurrentState = state;
        _currentState = _allStates[state];
        TimeInState = 0f;
        LastReason = reason;

        OnStateChanged(state, reason);
        _currentState.Enter();
    }

    public void Update()
    {
        if (!CurrentStateAvailable()) return;

        foreach (GlobalTransition transition in _globalTransitions)
        {
            if (IsCurrent(transition.To) || IsExcluded(transition.Except)) continue;
            if (transition.Condition())
            {
                ChangeState(transition.To, transition.Reason);
                break;
            }
        }

        TimeInState += Time.deltaTime;
        _currentState.Update();
    }

    public void LateUpdate()
    {
        if (CurrentStateAvailable()) _currentState.LateUpdate();
    }

    public void FixedUpdate()
    {
        if (CurrentStateAvailable()) _currentState.FixedUpdate();
    }

    public bool IsCurrent(T state) => InValidState && EqualityComparer<T>.Default.Equals(CurrentState, state);

    public State<T> GetState(T state)
    {
        if (!_allStates.ContainsKey(state))
        {
            Debug.LogError(state + ": state was not present in state machine");
            return null;
        }
        return _allStates[state];
    }

    private bool IsExcluded(T[] except)
    {
        if (except == null) return false;
        foreach (T state in except)
        {
            if (IsCurrent(state)) return true;
        }
        return false;
    }

    private bool CurrentStateAvailable()
    {
        if (_currentState == null)
        {
            Debug.LogError("There is no state selected, did you forget to execute change state on start?");
            return false;
        }
        return true;
    }
}

public abstract class State<T>
{
    public virtual string Name { get; protected set; }
    protected FiniteStateMachine<T> _fsm;

    public State(FiniteStateMachine<T> fsm, string name = "")
    {
        _fsm = fsm;
        Name = name;
    }

    public virtual void Enter() { }
    public virtual void Update() { }
    public virtual void LateUpdate() { }
    public virtual void FixedUpdate() { }
    public virtual void Exit() { }
}
