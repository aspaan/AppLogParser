using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogParser
{
    public abstract class Parser
    {
        public abstract Task<LogResponse> GetHistogramAsync(LogParserParameters parameters);
        public abstract Task<LogResponse> GetLogsAsync(LogParserParameters parameters);
    }
}
