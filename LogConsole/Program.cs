using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using LogParser;

namespace LogConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            var result = Get("php",WorkerType.Windows, "2017-10-18T20:43:56.000Z", "2017-10-18T20:58:56.000Z");
            Console.WriteLine(result.Logs);
        }

        private static LogResponse Get(string stack, WorkerType workerType, string startTime = null, string endTime = null, string timeGrain = null)
        {
            DateTime startTimeUtc, endTimeUtc;
            TimeSpan timeGrainTimeSpan;
            string errorMessage;

            if (!PrepareStartEndTimeUtc(startTime, endTime, timeGrain, out startTimeUtc, out endTimeUtc, out timeGrainTimeSpan, out errorMessage))
            {
                throw new WebException(HttpStatusCode.BadRequest.ToString() + ": " + errorMessage);
            }

            LogParserParameters p = new LogParserParameters();
            p.StartTime = startTimeUtc;
            p.EndTime = endTimeUtc;
            p.TimeGrain = timeGrainTimeSpan;
            p.WorkerType = workerType;

            Parser parser = ParserFactory.GetParser(stack);

            if (parser == null)
            {
                throw new WebException("Stack " + stack + " has no log parser implimintation");
            }

            return parser.GetHistogramAsync(p).Result;
        }

        private static bool PrepareStartEndTimeUtc(string startTime, string endTime, string timeGrain, out DateTime startTimeUtc, out DateTime endTimeUtc, out TimeSpan timeGrainTimeSpan, out string errorMessage)
        {

            DateTime currentUtcTime = GetDateTimeInUtcFormat(DateTime.UtcNow);
            bool result = true;
            errorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(timeGrain))
            {
                timeGrainTimeSpan = TimeSpan.FromMinutes(5);
            }
            else
            {
                int temp;
                bool timeGrainResult = Int32.TryParse(timeGrain, out temp);
                if (timeGrainResult)
                {
                    timeGrainTimeSpan = TimeSpan.FromMinutes(temp);
                }
                else
                {
                    timeGrainTimeSpan = TimeSpan.FromMinutes(5);
                    errorMessage = "Invalid time grain: " + timeGrain;
                }
            }

            if (string.IsNullOrWhiteSpace(startTime) && string.IsNullOrWhiteSpace(endTime))
            {
                endTimeUtc = currentUtcTime;
                startTimeUtc = endTimeUtc.AddDays(-1);
            }
            else if (string.IsNullOrWhiteSpace(startTime))
            {
                result = ParseDateTimeParameter(endTime, currentUtcTime, out endTimeUtc);
                startTimeUtc = endTimeUtc.AddDays(-1);
            }
            else if (string.IsNullOrWhiteSpace(endTime))
            {
                result = ParseDateTimeParameter(startTime, currentUtcTime.AddDays(-1), out startTimeUtc);
                endTimeUtc = startTimeUtc.AddDays(1);
                if (endTimeUtc > currentUtcTime)
                {
                    endTimeUtc = currentUtcTime;
                }
            }
            else
            {
                result = ParseDateTimeParameter(endTime, currentUtcTime, out endTimeUtc);
                result &= ParseDateTimeParameter(startTime, currentUtcTime.AddDays(-1), out startTimeUtc);
            }

            if (result == false)
            {
                errorMessage = "Cannot parse invalid date time. Valid Time format is yyyy-mm-ddThh:mm";
                return false;
            }

            if (startTimeUtc > endTimeUtc)
            {
                errorMessage = "Invalid Start Time and End Time. End Time cannot be earlier than Start Time.";
                return false;
            }
            else if (startTimeUtc > currentUtcTime)
            {
                errorMessage = "Invalid Start Time. Start Time cannot be a future date.";
                return false;
            }

            if (endTimeUtc - startTimeUtc > TimeSpan.FromHours(24))
            {
                errorMessage = string.Format("Invalid Time Range. Time Range cannot be more than 24 hours.");
                return false;
            }

            return true;
        }

        private static bool ParseDateTimeParameter(string parameterValue, DateTime defaultValue, out DateTime dateObj)
        {
            dateObj = defaultValue;
            if (!string.IsNullOrEmpty(parameterValue))
            {
                DateTime temp;
                bool result = DateTime.TryParse(parameterValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out temp);
                if (result)
                {
                    dateObj = GetDateTimeInUtcFormat(temp);
                    return true;
                }

                return false;
            }

            return true;
        }

        private static DateTime GetDateTimeInUtcFormat(DateTime dateTime)
        {
            if (dateTime.Kind == DateTimeKind.Unspecified)
            {
                return new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, dateTime.Hour, dateTime.Minute, dateTime.Second, dateTime.Millisecond, DateTimeKind.Utc);
            }

            return dateTime.ToUniversalTime();
        }
    }
}
