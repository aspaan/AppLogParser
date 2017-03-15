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

        public long BinarySearchLogFile(Stream stream, DateTime date, long startingOffset, long endingOffSet)
        {
            long middle = (endingOffSet - startingOffset) / 2;
            if (middle < 0)
            {
                return -1;
            }

            stream.Seek(startingOffset + middle, SeekOrigin.Begin);
            using (StreamReader reader = new StreamReader(stream))
            {
                string prevLine = "";
                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine();
                    var logDate = GetDateFromLog(line);
                    if (logDate != new DateTime())
                    {
                        var timeSpan = logDate.Subtract(date);
                        if (timeSpan.Days > 0)
                        {
                            var offSet = (startingOffset + prevLine.Length);
                            return BinarySearchLogFile(stream, date, offSet, (startingOffset + middle));
                        }
                        if (timeSpan.Days == 0)
                        {
                            return startingOffset + prevLine.Length;
                        }
                        if (timeSpan.Days < 0)
                        {
                            var offSet = (startingOffset + middle + prevLine.Length);
                            return BinarySearchLogFile(stream, date, offSet, endingOffSet);
                        }
                    }
                    prevLine += line;
                }
            }

            return 0;
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
                            response.Logs.Add(line);
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
                    response.Logs.Add(line);
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

    }
}
