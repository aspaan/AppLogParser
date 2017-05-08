using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace LogParser
{
    public class PhpLogParser: Parser
    {

        public async override Task<LogResponse> GetHistogramAsync(LogParserParameters parameters)
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
            ReadFileInReverseOrder(response.LogFile, offSet, parameters, logMetricsList, response);
            ReadFileInOrder(response.LogFile, offSet, parameters, logMetricsList, response);

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
            response.ParseTime = sw.ElapsedMilliseconds;
            return response;
        }

        private async Task<LogResponse> FindAndSetLoggingFileAndCreateResponseObject()
        {
            var response = new LogResponse();

            response.SettingsFile = Environment.ExpandEnvironmentVariables(@"%HOME%\site\wwwroot\.user.ini");

            response.SettingsFileFound = File.Exists(response.SettingsFile);

            if (response.SettingsFileFound)
            {
                response.LogFile = await GetLogFile(response.SettingsFile);
            }

            if (!File.Exists(response.LogFile))
            {
                response.LogFile = @"D:\Home\site\wwwroot\php_errors.log";
            }
            //overwrite to test locally
            //response.LogFile = @"D:\Home\site\wwwroot\php_errors.log";

            response.LogFileFound = File.Exists(response.LogFile);

            return response;
        }

        public override DateTime GetMetricFromLine(string line, LogParserParameters parameters, Dictionary<string, LogMetrics> logMetricsList)
        {
            DateTime date = GetDateFromLog(line);

            if (date < parameters.StartTime || date > parameters.EndTime)
                return date;

            var category = GetCategoryFromLog(line);

            var metric = new LogMetric(date, 1);
            if (logMetricsList.ContainsKey(category))
            {
                logMetricsList[category].Add(metric);
            }
            else
            {

                var newMetricSet = new LogMetrics();
                newMetricSet.Add(metric);
                logMetricsList.Add(category, newMetricSet);
            }

            return metric.Timestamp;
        }

        private string GetCategoryFromLog(string line)
        {
            if (line.ToLower().Contains("has exceeded the "))
            {
                return "Connections Maxed Out";
            }
            if (line.ToLower().Contains("server has gone away"))
            {
                return "MYSQL Server Gone";
            }
            if (line.ToLower().Contains("maximum execution time of"))
            {
                return "Time Out";
            }
            if (line.ToLower().Contains("mysql_connect(): an attempt was made to access a socket in a way forbidden by its access permissions"))
            {
                return "Access Permission Denied";
            }
            if (line.ToLower().Contains("out of memory") 
                || (line.ToLower().Contains("allowed memory size of") && line.ToLower().Contains("bytes exhausted")))
            {
                return "Out of Memory";
            }
            if (line.ToLower().Contains("ssl certificate problem, verify that the ca cert is ok"))
            {
                return "SSSL Certificate Problem";
            }
            int index = line.IndexOf("]");
            int num2 = (line.IndexOf(":") > -1) ? line.IndexOf(":") : 40;
            return line.Substring(index + 1, num2 - 1).Replace(":", "").Trim();
        }

        public override DateTime GetDateFromLog(string line)
        {
            var dateBracket = line.IndexOf("]");
            if (!line.StartsWith("[") || dateBracket == -1)
                return new DateTime();

            string dateString = string.Join(" ", line.Substring(1, dateBracket - 1).Split().Take(2));
            string timeZone = line.Substring(1, dateBracket - 1).Split()[2];

            DateTime apiDate = DateTime.Parse(dateString + ((timeZone == "UTC") ? "Z" : ""));

            TimeZoneInfo tzi = Util.OlsonTimeZoneToTimeZoneInfo(timeZone);

            if(timeZone=="UTC")
                return TimeZoneInfo.ConvertTime(apiDate, tzi);

            return TimeZoneInfo.ConvertTimeToUtc(apiDate, tzi);
        }

        public override async Task<string> GetLogFile(string filePath)
        {
            try
            {
                var file = new StreamReader(filePath);

                string line;
                while ((line = await file.ReadLineAsync()) != null)
                {
                    if (line.ToLower().StartsWith("error_log"))
                    {
                        line = line.Replace("error_log=", "");
                        line = line.Replace("error_log = ", "");
                        line = line.Replace("error_log =", "");
                        line = line.Replace("error_log= ", "");
                        line = line.Replace("\"", "").Trim();
                        break;
                    }
                }
                file.Close();
                return line;
            }
            catch (Exception e)
            {
            }
            return "";
        }

        public override Task<EventLogResponse> GetEventLogs(string stack, DateTime startTime, DateTime endTime)
        {
            throw new NotImplementedException();
        }
    }

}
