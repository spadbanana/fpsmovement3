using Godot;
using System;

public class State
{
	public Action<double> OnStateUpdateAction;
	public Action OnStateEnterAction;
	public Action OnStateExitAction;
	public String Statename;

	public State(Action<double> update, Action enter, Action exit, string statename)
	{
		OnStateUpdateAction = update;
		OnStateEnterAction = enter;
		OnStateExitAction = exit;
		Statename = statename;
	}
}
