using System;
using System.Threading.Tasks;

namespace Omnijure.Core.Shared.Infrastructure.EventBus;

public interface IEventBus
{
    void Subscribe<TEvent>(Action<TEvent> handler);
    void SubscribeAsync<TEvent>(Func<TEvent, Task> asyncHandler);
    void Publish<TEvent>(TEvent @event);
}
