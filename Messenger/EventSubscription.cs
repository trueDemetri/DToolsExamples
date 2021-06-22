using System;

namespace DTools.EventSystem
{
	public partial class Messenger<TEventBase>
	{
		private class EventSubscription<TEventType> : IDisposable where TEventType : TEventBase
		{
			public readonly Action<TEventType> Action;
			private readonly Messenger<TEventBase> _messenger;
			private bool _disposed;

			public EventSubscription(Messenger<TEventBase> messenger, Action<TEventType> action)
			{
				Action = action;
				_messenger = messenger;
			}
		
			public void Dispose()
			{
				if (_disposed) return;
				
				_messenger.Unsubscribe(this);
				_disposed = true;
			}
		}
	}
}