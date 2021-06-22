using System;

namespace DTools.StateMachines
{
	public interface IStateEntrails<TState> where TState: State<TState>
	{
		void Enter(TState prevState, object arg);
		void Exit(TState nextState);
		void SetStateMachine(StateMachine<TState> stateMachine);
	}

	public abstract class State<TState>: IDisposable, IStateEntrails<TState> where TState : State<TState>
	{
		protected StateMachine<TState> StateMachine { get; private set; }
		protected bool Active { get; private set; }

		void IStateEntrails<TState>.SetStateMachine(StateMachine<TState> stateMachine)
		{
			StateMachine = stateMachine;
		}
		
		void IStateEntrails<TState>.Enter(TState prevState, object arg)
		{
			Active = true;
			OnEnter(prevState, arg);
		}

		void IStateEntrails<TState>.Exit(TState nextState)
		{
			Active = false;
			OnExit(nextState);
		}
		
		protected virtual void OnEnter(TState prevState, object arg)
		{}

		protected virtual void OnExit(TState nextState)
		{}
		
		public virtual void Dispose()
		{}
	}
}