using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogParser
{
    public abstract class Parser
    {
        public bool SingleLineLog { get; set;  }

        public async Task<LogResponse> GetHistogramAsync(LogParserParameters parameters)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();

            var response = await FindAndSetLoggingFileAndCreateResponseObject();
            response.Parameters = parameters;

            if (!response.LogFileFound)
            {
                sw.Stop();
                response.ParseTime = sw.ElapsedMilliseconds;
                return response;
            }

            FileInfo fileInfo = new FileInfo(response.LogFile);
            long filesize = fileInfo.Length;

            long offSet = 0;
            using (Stream stream = File.Open(response.LogFile, FileMode.Open))
            {
                offSet = BinarySearchLogFile(stream, parameters.EndTime, 0, filesize, parameters.EndTime.Subtract(parameters.StartTime));
            }

            if (offSet == -1)
            {
                sw.Stop();
                response.ParseTime = sw.ElapsedMilliseconds;
                return response;
            }

            var logMetricsList = new Dictionary<string, LogMetrics>();
            offSet = ReadFileInReverseOrder(response.LogFile, offSet, parameters, response);

            ReadFileInOrder(offSet, parameters, logMetricsList, response);

            foreach (var category in logMetricsList.Keys)
            {
                var logCatgeorMetrics = new LogMetrics();

                logCatgeorMetrics.AddRange(from dt in logMetricsList[category]
                                           group dt by dt.Timestamp.Ticks / parameters.TimeGrain.Ticks
                                           into g
                                           select
                                               new LogMetric(Util.GetDateTimeInUtcFormat(new DateTime(g.Key * parameters.TimeGrain.Ticks)), g.ToList().Count));

                logCatgeorMetrics.Sort((x, y) => x.Timestamp.CompareTo(y.Timestamp));

                response.LogMetrics.Add(category, logCatgeorMetrics);

            }


            sw.Stop();

            response.ExceptionCount = response.ExceptionCount.OrderByDescending(x => x.Value).ToDictionary(x => x.Key, x => x.Value);

            response.ParseTime = sw.ElapsedMilliseconds;
            return response;
        }

        public void ReadFileInOrder(long offSet, LogParserParameters parameters, Dictionary<string, LogMetrics> logMetricsList, LogResponse response)
        {
            string fileName = response.LogFile;
            double timeSpanInMinutes = parameters.EndTime.Subtract(parameters.StartTime).TotalMinutes;
            bool includeLogs = (timeSpanInMinutes <= 10 && timeSpanInMinutes > 0);
            long i = 0;
            using (Stream stream = File.Open(fileName, FileMode.Open))
            {
                stream.Seek(offSet, SeekOrigin.Begin);
                using (StreamReader reader = new StreamReader(stream))
                {
                    string exceptionStr = "";
                    bool first = true;
                    var prevDate = new DateTime();
                    while (!reader.EndOfStream)
                    {
                        string line = reader.ReadLine();
                        i += line.Length;

                        var logDate = GetDateFromLog(line);
                        
                        if (logDate != new DateTime())
                        {
                            if (!string.IsNullOrEmpty(exceptionStr))
                            {
                                if (logDate > parameters.StartTime && logDate <= parameters.EndTime)
                                {
                                    var dateLessExceptionStr = RemoveDateFromLog(exceptionStr).Trim();

                                    if (prevDate >= parameters.StartTime && prevDate <= parameters.EndTime)
                                    {
                                        GetMetricFromLine(prevDate, dateLessExceptionStr, parameters, logMetricsList);

                                        if (!response.ExceptionCount.ContainsKey(dateLessExceptionStr))
                                            response.ExceptionCount.Add(dateLessExceptionStr, 0);

                                        response.ExceptionCount[dateLessExceptionStr] += 1;

                                        if (includeLogs)
                                        {
                                            response.LinkedLogs.AddLast(exceptionStr);
                                        }
                                    }
                                }

                                exceptionStr = line;
                            }
                            if (first)
                            {
                                exceptionStr = line;
                                first = false;
                            }
                            prevDate = logDate;
                        }
                        else
                        {
                            if(!SingleLineLog && !first)
                                exceptionStr = exceptionStr + Environment.NewLine + line; ;
                        }

                        

                        if ((logDate != new DateTime() && logDate > parameters.EndTime))
                        {
                            break;
                        }

                    }
                }
            }

            response.DataScanned += i;
        }

        public long ReadFileInReverseOrder(string fileName, long offSet, LogParserParameters parameters, LogResponse response)
        {
            ReverseLineReader reader = new ReverseLineReader(fileName, offSet);
            long i = 0;
            foreach (var line in reader)
            {
                DateTime date = GetDateFromLog(line);
                i += line.Length;
                if ((date != new DateTime() && date <= parameters.StartTime))
                {
                    return reader.OffSet;
                }
            }

            response.DataScanned += i;
            return offSet;
        }

        public long BinarySearchLogFile(Stream stream, DateTime dateToQuery, long startingOffset, long endingOffSet, TimeSpan timespan)
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
                    if (dateFromLog != new DateTime())
                    {
                        TimeSpan span = dateFromLog.Subtract(dateToQuery);
                        double totalMinutes = Math.Abs(span.TotalMinutes);
                        if ((totalMinutes <= timespan.TotalMinutes))
                        {
                            return (startingOffset + middle + str.Length);
                        }
                        if (span.TotalMinutes > timespan.TotalMinutes)
                        {
                            long offset = startingOffset + str.Length;
                            return this.BinarySearchLogFile(stream, dateToQuery, offset, startingOffset + middle, timespan);
                        }
                        if ((span.TotalMinutes > 0.0) && (span.TotalMinutes < timespan.TotalMinutes))
                        {
                            return (startingOffset + str.Length);
                        }
                        if (span.TotalMinutes < 0.0)
                        {
                            long offset = (startingOffset + middle);
                            return this.BinarySearchLogFile(stream, dateToQuery, offset, endingOffSet, timespan);
                        }
                    }
                }
            }
            return 0L;
        }

        public abstract Task<LogResponse> FindAndSetLoggingFileAndCreateResponseObject();

        public abstract void GetMetricFromLine(DateTime date, string line, LogParserParameters parameters,
            Dictionary<string, LogMetrics> logMetricsList);

        public abstract DateTime GetDateFromLog(string line);

        public abstract Task<string> GetLogFile(string filePath);

        public abstract Task<EventLogResponse> GetEventLogs(string stack, DateTime startTime, DateTime endTime);

        public abstract string RemoveDateFromLog(string line);

    }
}
