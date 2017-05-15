using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogParser
{
    public abstract class Parser
    {
        public abstract Task<LogResponse> GetHistogramAsync(LogParserParameters parameters);

        public long BinarySearchLogFile(Stream stream, DateTime date, long startingOffset, long endingOffSet, TimeSpan timespan)
        {
            long middle = (endingOffSet - startingOffset) / 2L;
            if (middle < 0L)
            {
                return -1L;
            }
            stream.Seek(startingOffset + middle, SeekOrigin.Begin);
            using (StreamReader reader = new StreamReader(stream))
            {
                string readLine;
                for (string str = ""; !reader.EndOfStream; str = str + readLine)
                {
                    readLine = reader.ReadLine();
                    DateTime dateFromLog = this.GetDateFromLog(readLine);
                    DateTime time2 = new DateTime();
                    if (dateFromLog != time2)
                    {
                        TimeSpan span = dateFromLog.Subtract(date);
                        if (span.TotalMinutes > timespan.TotalMinutes)
                        {
                            long offset = startingOffset + str.Length;
                            return this.BinarySearchLogFile(stream, date, offset, startingOffset + middle, timespan);
                        }
                        if ((span.TotalMinutes > 0.0) && (span.TotalMinutes < timespan.TotalMinutes))
                        {
                            return (startingOffset + str.Length);
                        }
                        if (span.TotalMinutes < 0.0)
                        {
                            long offset = (startingOffset + middle) + str.Length;
                            return this.BinarySearchLogFile(stream, date, offset, endingOffSet, timespan);
                        }
                    }
                }
            }
            return 0L;
        }

        public void ReadFileInOrder(string fileName, long offSet, LogParserParameters parameters, Dictionary<string, LogMetrics> logMetricsList, LogResponse response)
        {
            double timeSpanInMinutes = parameters.EndTime.Subtract(parameters.StartTime).TotalMinutes;
            bool includeLogs = (timeSpanInMinutes <= 10 && timeSpanInMinutes > 0);
            long i = 0;
            using (Stream stream = File.Open(fileName, FileMode.Open))
            {
                stream.Seek(offSet, SeekOrigin.Begin);
                using (StreamReader reader = new StreamReader(stream))
                {
                    while (!reader.EndOfStream)
                    {
                        string line = reader.ReadLine();


                        i += line.Length;
                        var logDate = GetMetricFromLine(line, parameters, logMetricsList);

                        if (includeLogs && (logDate != new DateTime() && logDate >= parameters.StartTime && logDate <= parameters.EndTime))
                        {
                            response.LinkedLogs.AddLast(line);
                        }

                        if ((logDate != new DateTime() && logDate >= parameters.EndTime))
                        {
                            break;
                        }

                    }
                }
            }

            response.DataScanned += i;
        }

        public void ReadFileInReverseOrder(string fileName, long offSet, LogParserParameters parameters, Dictionary<string, LogMetrics> logMetricsList, LogResponse response)
        {
            double timeSpanInMinutes = parameters.EndTime.Subtract(parameters.StartTime).TotalMinutes;
            bool includeLogs = (timeSpanInMinutes <= 10 && timeSpanInMinutes > 0);

            ReverseLineReader reader = new ReverseLineReader(fileName, offSet);
            long i = 0;
            foreach (var line in reader)
            {

                var logDate = GetMetricFromLine(line, parameters, logMetricsList);

                if (includeLogs && (logDate != new DateTime() && logDate >= parameters.StartTime && logDate <= parameters.EndTime))
                {
                    response.LinkedLogs.AddFirst(line);
                }

                i += line.Length;
                if ((logDate != new DateTime() && logDate <= parameters.StartTime))
                {
                    break;
                }
            }

            response.DataScanned += i;
        }

        public abstract DateTime GetMetricFromLine(string line, LogParserParameters parameters,
            Dictionary<string, LogMetrics> logMetricsList);

        public abstract DateTime GetDateFromLog(string line);

        public abstract Task<string> GetLogFile(string filePath);

        public abstract Task<EventLogResponse> GetEventLogs(string stack, DateTime startTime, DateTime endTime);

    }
}
