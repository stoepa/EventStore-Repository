namespace EventStore.Repository
{
    public record EventWrapper(IEvent Event, IEventMetadata Metadata);
}
