using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace LogParser
{
    [DataContract]
    public class EventLogResponse
    {
        [DataMember]
        public string ResponseMessage { get; set; }

        [DataMember]
        public Dictionary<string, string> AdditionalData { get; set; }

        [DataMember]
        public List<EventLog> EventLogs { get; set; }

        [DataMember]
        public StdoutLogsResponse StdoutLogsResponse { get; set; }

        [DataMember]
        public string ExceptionMessage { get; set; }
    }

    [DataContract]
    public class StdoutLog
    {
        [DataMember]
        public string Name { get; set; }

        [DataMember]
        public DateTime LastAccessTimeUtc { get; set; }

        [DataMember]
        public DateTime CreationTimeUtc { get; set; }

        [DataMember]
        public string Path { get; set; }
    }

    [DataContract]
    public class StdoutLogsResponse
    {
        [DataMember]
        public string ExceptionMessage { get; set; }

        [DataMember]
        public List<StdoutLog> StdOutLogs { get; set; }
    }
}
