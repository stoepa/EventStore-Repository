using System.Threading.Tasks;

namespace EventStore.Repository
{
    public interface IEventStoreSnapshotService<T> where T : IAggregateMarker
    {
        Task SnapshotAggregateAsync(T aggregateRoot, string streamName, long version);
        Task<EventRepositorySnapshot<T>> GetAggregateSnapshotByStreamNameAsync(string streamName);
        Task<long> GetAggregateSnapshotVersionByStreamAsync(string streamName);
    }
}
