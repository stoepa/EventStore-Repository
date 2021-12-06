namespace EventStore.Repository
{
    public class EventRepositorySnapshottingOptions
    {
        public bool IsEnabled { get; set; } = false;
        public int Interval { get; set; } = 200;
    }
}
