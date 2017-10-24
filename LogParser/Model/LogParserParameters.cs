using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace LogParser
{
    public class LogParserParameters
    {
        public DateTime StartTime { get; set; }

        public DateTime EndTime { get; set; }

        public TimeSpan TimeGrain { get; set; }

        public WorkerType WorkerType { get; set; }
    }

    public enum WorkerType
    {
        Windows=1,
        Linux=2
    }
}