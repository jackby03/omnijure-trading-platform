using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Omnijure.Core.Shared.Infrastructure.EventBus;

public class EventBus : IEventBus
{
    private readonly ConcurrentDictionary<Type, List<object>> _handlers = new();
    private readonly ConcurrentDictionary<Type, List<object>> _asyncHandlers = new();

    public void Subscribe<TEvent>(Action<TEvent> handler)
    {
        var eventType = typeof(TEvent);
        var list = _handlers.GetOrAdd(eventType, _ => new List<object>());
        lock (list)
        {
            list.Add(handler);
        }
    }

    public void SubscribeAsync<TEvent>(Func<TEvent, Task> asyncHandler)
    {
        var eventType = typeof(TEvent);
        var list = _asyncHandlers.GetOrAdd(eventType, _ => new List<object>());
        lock (list)
        {
            list.Add(asyncHandler);
        }
    }

    public void Publish<TEvent>(TEvent @event)
    {
        var eventType = typeof(TEvent);
        
        // Synchronous handlers
        if (_handlers.TryGetValue(eventType, out var syncHandlers))
        {
            List<object> handlersCopy;
            lock (syncHandlers)
            {
                handlersCopy = syncHandlers.ToList();
            }

            foreach (var handler in handlersCopy)
            {
                if (handler is Action<TEvent> typedHandler)
                {
                    typedHandler(@event);
                }
            }
        }

        // Asynchronous handlers (Fire and forget, but conceptually we start the task)
        if (_asyncHandlers.TryGetValue(eventType, out var asyncHandlers))
        {
            List<object> asyncHandlersCopy;
            lock (asyncHandlers)
            {
                asyncHandlersCopy = asyncHandlers.ToList();
            }

            foreach (var handler in asyncHandlersCopy)
            {
                if (handler is Func<TEvent, Task> typedAsyncHandler)
                {
                    _ = typedAsyncHandler(@event);
                }
            }
        }
    }
}
