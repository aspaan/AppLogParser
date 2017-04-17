using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogParser
{
    public class EventLog
    {
        public DateTime PreciseTimeStamp { get; set; }

        public List<string> EventData { get; set; }
    }

}
