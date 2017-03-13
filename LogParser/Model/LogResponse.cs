using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Web;

namespace LogParser
{
    [DataContract]
    public class LogResponse
    {
        public LogResponse()
        {
            LogMetrics = new Dictionary<string, LogMetrics>();
            Logs = new List<string>();
        }

        [DataMember]
        public Dictionary<string,LogMetrics> LogMetrics { get; set; }

        [DataMember]
        public List<string> Logs { get; set; }

        [DataMember]
        public string LogFile { get; set; }

        [DataMember]
        public string SettingsFile { get; set; }

        [DataMember]
        public bool SettingsFileFound { get; set; }

        [DataMember]
        public bool LoggingEnabled { get; set; }

        [DataMember]
        public bool LogFileFound { get; set; }

        [DataMember]
        public long ParseTime { get; set; }

        [DataMember]
        public long DataScanned { get; set; }

        [DataMember]
        public LogParserParameters Parameters { get; set; }
    }

    [DataContract]
    public class LogMetric
    {
        [DataMember]
        public DateTime Timestamp { get; set; }

        [DataMember]
        public int Count { get; set; }

        public LogMetric(DateTime measurementTime, int total)
        {
            Timestamp = measurementTime;
            Count = total;
        }
    }

    [CollectionDataContract]
    public class LogMetrics : List<LogMetric>
    {
        /// <summary>
        /// Create a new instance of DiagnosticMetricSets
        /// </summary>
        public LogMetrics()
        {
        }

        /// <summary>
        /// Create a new instance of DiagnosticMetricSets
        /// </summary>
        public LogMetrics(List<LogMetric> metricSets)
            : base(metricSets)
        {
        }
    }
}
