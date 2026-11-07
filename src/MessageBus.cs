using System;
using System.Collections.Generic;

namespace ClearWin
{
    /// <summary>
    /// Canonical data-flow messages published on the agent MessageBus.
    /// These are the typed contracts that decouple producers from consumers:
    /// a subsystem publishes a message, any number of subscribers react, and the
    /// producer never holds a reference to the consumer.
    /// </summary>
    public sealed class StateObservedEvent
    {
        public StateObservedEvent(SystemState state) { State = state; }
        public SystemState State { get; private set; }
    }

    public sealed class DecisionExecutedEvent
    {
        public DecisionExecutedEvent(AgentDecision decision, bool success)
        {
            Decision = decision;
            Success = success;
        }
        public AgentDecision Decision { get; private set; }
        public bool Success { get; private set; }
    }

    public sealed class RewardRecordedEvent
    {
        public RewardRecordedEvent(string action, double reward, bool success)
        {
            Action = action;
            Reward = reward;
            Success = success;
        }
        public string Action { get; private set; }
        public double Reward { get; private set; }
        public bool Success { get; private set; }
    }

    /// <summary>
    /// Minimal thread-safe publish/subscribe event bus (C# 5, no external deps).
    ///
    ///   bus.Subscribe<StateObservedEvent>(handler);   // returns IDisposable
    ///   bus.Publish(new StateObservedEvent(state));   // fan-out to all handlers
    ///
    /// A failing handler never breaks Publish: the exception is forwarded to the
    /// optional ErrorHandler (or swallowed) so one bad subscriber cannot take
    /// down the tick loop.
    /// </summary>
    public sealed class MessageBus
    {
        private readonly object _sync = new object();
        private readonly Dictionary<Type, List<Delegate>> _handlers = new Dictionary<Type, List<Delegate>>();

        /// <summary>Optional observer for subscriber exceptions raised during Publish.</summary>
        public Action<Type, Exception> ErrorHandler { get; set; }

        public IDisposable Subscribe<T>(Action<T> handler)
        {
            if (handler == null) throw new ArgumentNullException("handler");
            Type t = typeof(T);
            lock (_sync)
            {
                List<Delegate> list;
                if (!_handlers.TryGetValue(t, out list))
                {
                    list = new List<Delegate>();
                    _handlers[t] = list;
                }
                list.Add(handler);
                return new Subscription(this, t, handler);
            }
        }

        public void Publish<T>(T message)
        {
            Delegate[] snapshot;
            lock (_sync)
            {
                List<Delegate> list;
                if (!_handlers.TryGetValue(typeof(T), out list) || list.Count == 0) return;
                snapshot = list.ToArray();
            }
            for (int i = 0; i < snapshot.Length; i++)
            {
                var handler = (Action<T>)snapshot[i];
                try { handler(message); }
                catch (Exception ex)
                {
                    Action<Type, Exception> eh = ErrorHandler;
                    if (eh != null) { try { eh(typeof(T), ex); } catch (Exception ex2) { CWLog.Error("MessageBus", ex2); } }
                }
            }
        }

        public int SubscriberCount<T>()
        {
            lock (_sync)
            {
                List<Delegate> list;
                if (!_handlers.TryGetValue(typeof(T), out list)) return 0;
                return list.Count;
            }
        }

        public int TotalSubscribers()
        {
            lock (_sync)
            {
                int n = 0;
                foreach (var kv in _handlers) n += kv.Value.Count;
                return n;
            }
        }

        internal void Unsubscribe(Type type, Delegate handler)
        {
            lock (_sync)
            {
                List<Delegate> list;
                if (!_handlers.TryGetValue(type, out list)) return;
                list.Remove(handler);
                if (list.Count == 0) _handlers.Remove(type);
            }
        }

        private sealed class Subscription : IDisposable
        {
            private MessageBus _bus;
            private readonly Type _type;
            private readonly Delegate _handler;
            private bool _disposed;

            public Subscription(MessageBus bus, Type type, Delegate handler)
            {
                _bus = bus;
                _type = type;
                _handler = handler;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                if (_bus != null)
                {
                    _bus.Unsubscribe(_type, _handler);
                    _bus = null;
                }
            }
        }
    }
}
