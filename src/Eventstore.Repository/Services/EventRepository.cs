using EventStore.Client;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EventStore.Repository
{
    public class EventRepository<T> : IEventRepository<T> where T : IAggregateMarker, new()
    {
        private readonly EventStoreClient eventStoreClient;
        private readonly EventRepositoryOptions eventRepositoryOptions;
        private readonly EventRepositorySnapshottingOptions eventRepositorySnapshottingOptions;
        private readonly IEventStoreSnapshotService<T> eventStoreSnapshotService;
        public EventRepository(EventStoreClient eventStoreClient, EventRepositoryOptions eventRepositoryOptions,
            IEventStoreSnapshotService<T> eventStoreSnapshotService, EventRepositorySnapshottingOptions eventRepositorySnapshottingOptions)
        {
            this.eventStoreClient = eventStoreClient;
            this.eventRepositoryOptions = eventRepositoryOptions;
            this.eventStoreSnapshotService = eventStoreSnapshotService;
            this.eventRepositorySnapshottingOptions = eventRepositorySnapshottingOptions;
        }

        public async Task<T> GetAsync(string streamName)
        {
            try
            {
                T aggregate = default(T);
                var currentVersion = -1L;
                if (eventRepositorySnapshottingOptions.IsEnabled)
                {
                    var snapshot = await eventStoreSnapshotService.GetAggregateSnapshotByStreamNameAsync(streamName);
                    aggregate = snapshot.Aggregate;
                    currentVersion = snapshot.SnapshotVersion;
                }

                if (aggregate is null)
                    aggregate = (T)Activator.CreateInstance(typeof(T), true);

                var events = eventStoreClient.ReadStreamAsync(Direction.Forwards,
                    streamName, 
                    aggregate == null ? StreamPosition.Start : Convert.ToUInt64(currentVersion));

                if (events.ReadState.Result == ReadState.StreamNotFound)
                    return default(T);
                
                await foreach(var esEv in events)
                {
                    var ev = JsonConvert.DeserializeObject<IEvent>(eventRepositoryOptions.Encoding.GetString(esEv.Event.Data.Span),
                        eventRepositoryOptions.JsonSerializerSettings);
                    aggregate.Raise(ev);
                    aggregate.SetPersistedVersion(esEv.Event.EventNumber.ToInt64());
                }

                return aggregate;
            }
            catch (Exception)
            {
                throw;
            }
        }

        public async Task<IWriteResult> SaveAsync(T aggregate, Func<T, string> streamNameFunc)
        {
            if (streamNameFunc == null)
                throw new ArgumentNullException(nameof(streamNameFunc));

            var streamName = streamNameFunc(aggregate);

            var writeResult = await AppendToEventStoreAsync(aggregate, streamName);

            if (eventRepositorySnapshottingOptions.IsEnabled)
            {
                var eventStoreVersion = writeResult.NextExpectedStreamRevision.ToInt64();
                var currentSnapshotVersion = await eventStoreSnapshotService.GetAggregateSnapshotVersionByStreamAsync(streamName);

                if ((eventStoreVersion - currentSnapshotVersion) >= Convert.ToInt64(eventRepositorySnapshottingOptions.Interval)
                            || (eventStoreVersion % Convert.ToInt64(eventRepositorySnapshottingOptions.Interval) == 0))
                {
                    await eventStoreSnapshotService.SnapshotAggregateAsync(aggregate, streamName, eventStoreVersion);
                }
            }

            return writeResult;
        }

        private async Task<IWriteResult> AppendToEventStoreAsync(T aggregate, string streamName)
        {
            var currentVersion = aggregate.PersistedVersion;
            var eventsToAppend = new List<List<EventData>> { new List<EventData>() };
            foreach (var domainEvent in aggregate.GetChanges())
            {
                var eventBytes = eventRepositoryOptions.Encoding.GetBytes(JsonConvert.SerializeObject(domainEvent.Event, eventRepositoryOptions.JsonSerializerSettings));
                var metadataBytes = eventRepositoryOptions.Encoding.GetBytes(JsonConvert.SerializeObject(domainEvent.Metadata, eventRepositoryOptions.JsonSerializerSettings));

                var eventData = new EventData(Uuid.NewUuid(), domainEvent.Event.Name, eventBytes, metadataBytes);
                if (eventsToAppend.Last().Sum(e => e.Data.Length + e.Metadata.Length) + (eventData.Data.Length + eventData.Metadata.Length) < 1048576)
                    eventsToAppend.Last().Add(eventData);
                else
                    eventsToAppend.Add(new List<EventData> { eventData });
            }

            IWriteResult writeResult = null;

            for (int i = 0; i < eventsToAppend.Count; i++)
            {
                if (currentVersion == -1L)
                {
                    writeResult = await eventStoreClient.AppendToStreamAsync(streamName, StreamState.NoStream, eventsToAppend[i]);
                    if (writeResult is WrongExpectedVersionResult)
                    {
                        var wr = (WrongExpectedVersionResult)writeResult;
                        throw new WrongExpectedVersionException(wr.StreamName, 
                            StreamRevision.FromInt64(wr.NextExpectedVersion),
                            StreamRevision.FromInt64(wr.ActualVersion));
                    }
                    currentVersion = writeResult.NextExpectedStreamRevision.ToInt64();
                }
                else
                {
                    writeResult = await eventStoreClient.AppendToStreamAsync(streamName, Convert.ToUInt64(currentVersion), eventsToAppend[i]);
                    currentVersion = writeResult.NextExpectedStreamRevision.ToInt64();
                    if (writeResult is WrongExpectedVersionResult)
                    {
                        var wr = (WrongExpectedVersionResult)writeResult;
                        throw new WrongExpectedVersionException(wr.StreamName,
                            StreamRevision.FromInt64(wr.NextExpectedVersion),
                            StreamRevision.FromInt64(wr.ActualVersion));
                    }
                }
            }

            return writeResult;
        }
    }
}
