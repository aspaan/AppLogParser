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

            var logMetricsList = new Dictionary<string, LogMetrics>();

            ReverseLineReader reader = new ReverseLineReader(response.LogFile);

            long i = 0;
            foreach (var line in reader)
            {

                if (!line.StartsWith("["))
                    continue;

                var metric = GetMetricFromLine(line, parameters, logMetricsList);

                i += line.Length;
                if (i > 100 * 1024 * 1024 || (metric != new DateTime() && metric < parameters.StartTime))
                {
                    break;
                }
            }
            response.DataScanned = i;


            foreach (var category in logMetricsList.Keys)
            {
                var logCatgeorMetrics = new LogMetrics();

                logCatgeorMetrics.AddRange(from dt in logMetricsList[category]
                                           group dt by dt.Timestamp.Ticks / parameters.TimeGrain.Ticks
            into g
                                           select
                                               new LogMetric(new DateTime(g.Key * parameters.TimeGrain.Ticks), g.ToList().Count));

                logCatgeorMetrics.Sort((x, y) => x.Timestamp.CompareTo(y.Timestamp));

                response.LogMetrics.Add(category, logCatgeorMetrics);

            }


            sw.Stop();
            response.ParseTime = sw.ElapsedMilliseconds;
            return response;
        }

        public async override Task<LogResponse> GetLogsAsync(LogParserParameters parameters)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();

            var response = await FindAndSetLoggingFileAndCreateResponseObject();
            response.Parameters = parameters;
            ReverseLineReader reader = new ReverseLineReader(response.LogFile);

            long i = 0;
            foreach (var line in reader)
            {

                if (!line.StartsWith("["))
                    continue;

                var date = GetDateFromLog(line);

                if (date > parameters.EndTime)
                    continue;

                if (date < parameters.StartTime)
                    break;

                response.Logs.Add(line);

                i += line.Length;
                if (i > 100 * 1024 * 1024 || response.Logs.Sum(x => x.Length) > 5 * 1024 * 1024)
                {
                    break;
                }
            }
            response.DataScanned = i;

            response.Logs.Reverse();

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
                response.LogFile = await GetLogFile(response.SettingsFile);

            //overwrite to test locally
            //response.LogFile = @"D:\Home\site\wwwroot\php_errors2.log";

            response.LogFileFound = File.Exists(response.LogFile);

            return response;
        }
        
        private DateTime GetMetricFromLine(string line, LogParserParameters parameters, Dictionary<string, LogMetrics> logMetricsList)
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
            var dateBracket = line.IndexOf("]");
            var categoryEndIndex = (line.IndexOf(":") > -1) ? line.IndexOf(":") : 40;

            return line.Substring(dateBracket + 1, categoryEndIndex - 1).Replace(":", "").Trim();
        }

        private DateTime GetDateFromLog(string line)
        {
            var dateBracket = line.IndexOf("]");
            if (!line.StartsWith("[") || dateBracket == -1)
                return new DateTime();

            string dateString = string.Join(" ", line.Substring(1, dateBracket - 1).Split().Take(2));
            string timeZone = line.Substring(1, dateBracket - 1).Split()[2];

            DateTime apiDate = DateTime.Parse(dateString + ((timeZone == "UTC") ? "Z" : ""));

            TimeZoneInfo tzi = Util.OlsonTimeZoneToTimeZoneInfo(timeZone);

            DateTime date = Util.GetDateTimeInUtcFormat(TimeZoneInfo.ConvertTime(apiDate, tzi));
            return date;
        }

        public async Task<string> GetLogFile(string filePath)
        {
            try
            {
                var file = new StreamReader(filePath);

                string line;
                while ((line = await file.ReadLineAsync()) != null)
                {
                    if (line.StartsWith("error_log="))
                    {
                        return line.Replace("error_log=", "").Replace("\"", "");
                    }
                }
                file.Close();
            }
            catch (Exception e)
            {
            }
            return "";
        }
    }

}



