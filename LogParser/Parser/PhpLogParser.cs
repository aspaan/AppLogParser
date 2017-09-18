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

        public override async Task<LogResponse> FindAndSetLoggingFileAndCreateResponseObject()
        {
            var response = new LogResponse();
            LogEnabler le = new LogEnabler();
            var logsEnabled = await le.IsEnabled("php");
            if (!logsEnabled)
            {
                response.LogFileFound = false;
                return response;
            }

            response.LoggingEnabled = true;
            response.SettingsFileFound = true;


            response.SettingsFile = Environment.ExpandEnvironmentVariables(@"%HOME%\site\wwwroot\.user.ini");

            response.LogFile = await GetLogFile(response.SettingsFile);
            if (!File.Exists(response.LogFile))
            {
                response.LogFile = @"D:\home\LogFiles\php_errors.log";
            }
            response.LogFileFound = File.Exists(response.LogFile);
            

            //overwrite to test locally
            //response.LogFile = @"D:\Home\site\wwwroot\php_errors.log";
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
            if (line.ToLower().Contains("has exceeded the"))
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
                return "Socket Permission Denied";
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
            if (line.ToLower().Contains("undefined method"))
            {
                return "Undefined Method";
            }
            if (line.ToLower().Contains("syntax error"))
            {
                return "Syntax Error ";
            }
            if (line.ToLower().Contains("wordpress database error deadlock found when trying to")
                || line.ToLower().Contains("error deadlock found when trying to get lock"))
            {
                return "WordPress Database Deadlock";
            }
            int index = line.IndexOf("]");
            int num2 = (line.IndexOf(":") > -1) ? line.IndexOf(":") : 40;
            if (line.Length >= index + num2)
            {
                var str = line.Substring(index + 1, num2).Replace(":", "").Trim();
                //if (str.EndsWith("ic"))
                //{
                //    Console.WriteLine(str);
                //}
                return str;
            }

            return "";
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

        public override string RemoveDateFromLog(string line)
        {
            var dateBracket = line.IndexOf("]");
            if (dateBracket > 0)
                return line.Substring(dateBracket+1);

            return line;
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
