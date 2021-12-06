using System;
using System.Threading.Tasks;

namespace EventStore.Repository
{
    public interface IEventRepository<T> where T : IAggregateMarker
    {
        Task<EventStore.Client.IWriteResult> SaveAsync(T aggregate, Func<T, string> streamName);
        Task<T> GetAsync(string streamName);
    }
}
