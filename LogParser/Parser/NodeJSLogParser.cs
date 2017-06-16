using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogParser
{
    public class NodeJSLogParser : Parser
    {

        public override async Task<LogResponse> FindAndSetLoggingFileAndCreateResponseObject()
        {
            var response = new LogResponse();

            response.SettingsFile = Environment.ExpandEnvironmentVariables(@"%HOME%\site\wwwroot\iisnode.yml");

            response.SettingsFileFound = File.Exists(response.SettingsFile);

            if (response.SettingsFileFound)
            {
                response.LogFileFound = await LoggingEnabled(response.SettingsFile);
                response.LogFile = @"D:\home\LogFiles\Application\logging-errors.txt";
                response.LogFileFound = File.Exists(response.LogFile);
            }

            //overwrite to test locally
            //response.LogFile = @"D:\Home\site\wwwroot\logging-errors.txt";
            //response.LogFileFound = File.Exists(response.LogFile);

            return response;
        }

        public override void GetMetricFromLine(DateTime date, string line, LogParserParameters parameters, Dictionary<string, LogMetrics> logMetricsList)
        {
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
        }

        private string GetCategoryFromLog(string line)
        {
            var lines = line.Split(Environment.NewLine.ToCharArray());

            if (lines.Length>2)
                return lines[2];

            return "";
        }

        public override DateTime GetDateFromLog(string line)
        {
            var dateBracket = line.IndexOf("(");
            if (!line.Contains("GMT") || dateBracket == -1)
                return new DateTime();

            var dateString = string.Join(" ",line.Substring(1, dateBracket - 1).Split().ToList().GetRange(1, 4)) ;
 
            DateTime apiDate = DateTime.Parse(dateString + "Z");
            TimeZoneInfo tzi = Util.OlsonTimeZoneToTimeZoneInfo("UTC");
          
            return TimeZoneInfo.ConvertTime(apiDate, tzi);
        }

        public async Task<bool> LoggingEnabled(string filePath)
        {
            try
            {
                var file = new StreamReader(filePath);
                bool loggingEnabled = false;
                string line;
                while ((line = await file.ReadLineAsync()) != null)
                {
                    if (line.Trim().ToLower().StartsWith("loggingEnabled")
                        && line.Trim().ToLower().Contains("true"))
                    {
                        loggingEnabled = true;
                        break;
                    }
                }
                file.Close();
                return loggingEnabled;
            }
            catch (Exception e)
            {
            }
            return true;
        }

        public override string RemoveDateFromLog(string line)
        {
            var dateBracket = line.IndexOf("):");
            if (dateBracket > 0)
                return line.Substring(dateBracket+2);

            return line;
        }

        public override Task<EventLogResponse> GetEventLogs(string stack, DateTime startTime, DateTime endTime)
        {
            throw new NotImplementedException();
        }


        public override async Task<string> GetLogFile(string filePath)
        {
            throw new NotImplementedException();
        }
    }
}
