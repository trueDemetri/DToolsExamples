using System;
using System.Collections.Generic;

namespace DTools.EventSystem
{
	public partial class Messenger<TEventBase>
	{
		private readonly Dictionary<Type, object> _subscriptions = new Dictionary<Type, object>();
		
		/// <summary>
		/// Subscribe to event by its type
		/// </summary>
		/// <param name="action">Event handler</param>
		/// <param name="highPriority">This flag allows receive event signal earlier than other normal-priority handlers. DON'T USE without special need!</param>
		/// <typeparam name="TEventType">Type of event class to subscribe</typeparam>
		/// <returns>IDisposable allowing to unsubscribe. Also returns null if duplicate subscription detected</returns>
		public IDisposable Subscribe<TEventType>(Action<TEventType> action, bool highPriority = false) where TEventType : TEventBase
		{
			var type = typeof(TEventType);
			LinkedList<Action<TEventType>> subscriptionList;
		
			if (!_subscriptions.TryGetValue(type, out var packedSubscriptions))
			{
				subscriptionList = new LinkedList<Action<TEventType>>();
				_subscriptions[type] = subscriptionList;
			}
			else
			{
				subscriptionList = (LinkedList<Action<TEventType>>) packedSubscriptions;
			}

			if (subscriptionList.Contains(action))
				return null;

			if (highPriority)
				subscriptionList.AddFirst(action);
			else
				subscriptionList.AddLast(action);
			
			return new EventSubscription<TEventType>(this, action);
		}

		public void RaiseEvent<TEventType>(TEventType eventData = default) where TEventType : TEventBase
		{
			var type = typeof(TEventType);
			if (!_subscriptions.TryGetValue(type, out var packedSubscriptions))
				return;

			var subscriptionList = (LinkedList<Action<TEventType>>) packedSubscriptions;

			var node = subscriptionList.First;
			var endNode = subscriptionList.Last;
			while (node != null)
			{
				var next = node.Next;
				node.Value(eventData);
				if (node == endNode) break;
				node = node.Next ?? next;
			}
		}

		public void Unsubscribe<TEventType>(Action<TEventType> handler) where TEventType : TEventBase
		{
			var type = typeof(TEventType);
			if (!_subscriptions.TryGetValue(type, out var packedSubscriptions))
				return;

			var subscriptionList = (LinkedList<Action<TEventType>>) packedSubscriptions;
			subscriptionList.Remove(handler);
		}
		
		private void Unsubscribe<TEventType>(EventSubscription<TEventType> eventSubscription) where TEventType : TEventBase
		{
			Unsubscribe(eventSubscription.Action);
		}
	}
}