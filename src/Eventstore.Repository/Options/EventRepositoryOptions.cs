using Newtonsoft.Json;
using System.Text;

namespace EventStore.Repository
{
    public class EventRepositoryOptions
    {
        public JsonSerializerSettings JsonSerializerSettings { get; set; }
        public Encoding Encoding { get; set; }
    }
}
