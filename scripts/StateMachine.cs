using Godot;
using System;
using System.Collections.Generic;

public class StateMachine
{
	public State currentState { get; private set; }
	public State previousState { get; private set; }

	public bool setStateThisDelta = false;
	private List<State> statelist = new();

	public State CreateState(Action<double> update, Action enter, Action exit, string statename)
	{
		State newState = new(update, enter, exit, statename);
		if (statelist.Count == 0)
		{
			currentState = newState;
			previousState = newState;
		}
		statelist.Add(newState);
		return newState;
	}

	public void SetState(State state)
	{
		previousState = currentState;
		currentState = state;
		previousState.OnStateExitAction?.Invoke();
		currentState.OnStateEnterAction?.Invoke();
		setStateThisDelta = true;
	}

	public bool SetStateIfNotCurrentState(State state) {
    	if (currentState == state) 
		{
			return false;
		}
    	SetState(state);
    	return true;
    }

	public void Tick(double delta)
	{
		if (setStateThisDelta == false)
		{
			currentState.OnStateUpdateAction?.Invoke(delta);
		}
		setStateThisDelta = false;
    }

	public string CurrentState()
	{
		return currentState.Statename;
	}

	public string PreviousState()
	{
		return previousState.Statename;
	}
}
