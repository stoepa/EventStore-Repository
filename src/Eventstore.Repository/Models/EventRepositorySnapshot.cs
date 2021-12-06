namespace EventStore.Repository
{
    public class EventRepositorySnapshot<T>
    {
        public long SnapshotVersion { get; set; }
        public T Aggregate { get; set; }
    }
}
