using System.Collections.Generic;

namespace EventStore.Repository
{
    public interface IAggregateMarker
    {
        long PersistedVersion { get; }
        IEnumerable<EventWrapper> GetChanges();
        void SetPersistedVersion(long persistedVersion);
        void Raise(IEvent raisedEvent);
    }
}
