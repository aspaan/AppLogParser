using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace LogParser
{
    public class EventLogParser : Parser
    {
        public override DateTime GetDateFromLog(string line)
        {
            throw new NotImplementedException();
        }

        public override Task<LogResponse> GetHistogramAsync(LogParserParameters parameters)
        {
            throw new NotImplementedException();
        }

        public override Task<string> GetLogFile(string filePath)
        {
            throw new NotImplementedException();
        }

        public override DateTime GetMetricFromLine(string line, LogParserParameters parameters, Dictionary<string, LogMetrics> logMetricsList)
        {
            throw new NotImplementedException();
        }

        private async Task<Dictionary<string, string>> GetAspNetCore()
        {
            Dictionary<string, string> aspNetCoreSettings = new Dictionary<string, string>();
            string webConfig = Environment.ExpandEnvironmentVariables(@"%HOME%\site\wwwroot\web.config");

            //for local testing
            //string webConfig= @"d:\temp\netcore\web.config";

            if (!File.Exists(webConfig))
            {
                aspNetCoreSettings.Add("Error", string.Format("web.config file not found in site home directory: {0}", webConfig));
                return aspNetCoreSettings;
            }
            else
            {
                aspNetCoreSettings = await Task.FromResult(GetAspNetCoreSettings(webConfig));
            }

            return aspNetCoreSettings;
        }

        private Dictionary<string, string> GetAspNetCoreSettings(string path)
        {
            Dictionary<string, string> res = new Dictionary<string, string>();

            res.Add("Webconfig", path);

            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(path);
                XmlNode xNodes = doc.SelectSingleNode("/configuration/system.webServer");

                foreach (XmlNode node in xNodes)
                {
                    if (string.Equals(node.Name, "handlers", StringComparison.InvariantCultureIgnoreCase))
                    {
                        foreach (XmlNode childNode in node.ChildNodes)
                        {
                            if (childNode.Attributes["name"] != null && string.Equals(childNode.Attributes["name"].Value, "aspNetCore", StringComparison.InvariantCultureIgnoreCase))
                            {
                                foreach (XmlAttribute attribute in childNode.Attributes)
                                {
                                    res.Add(attribute.Name, attribute.Value);
                                }
                                break;
                            }
                        }
                    }

                    if (string.Equals(node.Name, "aspNetCore", StringComparison.InvariantCultureIgnoreCase))
                    {
                        foreach (XmlAttribute attribute in node.Attributes)
                        {
                            res.Add(attribute.Name, attribute.Value);
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                res.Add("Exception", ex.Message);
                return res;
            }
            
            return res;
        }

        private async Task<StdoutLogsResponse> GetStdoutLogs(string path, DateTime startTime, DateTime endTime)
        {
            //string homeDirectory = Environment.ExpandEnvironmentVariables(@"%HOME%\site\wwwroot\");
            StdoutLogsResponse res = new StdoutLogsResponse();

            if (!path.EndsWith(@"\"))
            {
                int idx = path.LastIndexOf('\\');
                path = path.Substring(0, idx);
            }

            //string stdLogFilesPath = path.StartsWith(".") ? Path.Combine(homeDirectory, path.Substring(2)) : path;

            string stdLogFilesPath = @"d:\temp\netcore\LogFiles";

            if (!String.IsNullOrWhiteSpace(stdLogFilesPath))
            {
                return await Task.FromResult<StdoutLogsResponse>(GetStdOutFileList(stdLogFilesPath, startTime, endTime));
            }

            return res;
        }

        private StdoutLogsResponse GetStdOutFileList(string directoryPath, DateTime startTime, DateTime endTime)
        {
            StdoutLogsResponse res = new StdoutLogsResponse();
            res.StdOutLogs = new List<StdoutLog>();

            try
            {
                DirectoryInfo stdLogFolder = new DirectoryInfo(directoryPath);

                foreach (var file in stdLogFolder.GetFiles())
                {
                    if (file.LastAccessTimeUtc >= startTime && file.LastAccessTimeUtc <= endTime)
                    {
                        StdoutLog stdoutLog = new StdoutLog()
                        {
                            Name = file.Name,
                            Path = file.FullName,
                            LastAccessTimeUtc = file.LastAccessTimeUtc,
                            CreationTimeUtc = file.CreationTimeUtc
                        };
                        res.StdOutLogs.Add(stdoutLog);
                    }
                }

                res.ExceptionMessage = String.Format("Processed files in {0}", directoryPath);

            }
            catch (Exception ex)
            {
                res.ExceptionMessage = ex.Message;
            }

            return res;
        }

        public override async Task<EventLogResponse> GetEventLogs(string stack, DateTime startTime, DateTime endTime)
        {
            string eventLogPath = Environment.ExpandEnvironmentVariables(@"%HOME%\LogFiles\eventlog.xml");
            
            //for local testing
            //string eventLogPath = @"d:\temp\netcore\eventlog2.xml";
            EventLogResponse eventLogResponse = new EventLogResponse();
            bool aspNetCore = false;
            StringBuilder responseMessage = new StringBuilder();

            if (!String.IsNullOrWhiteSpace(stack) && String.Equals(stack, "aspnetcore", StringComparison.InvariantCultureIgnoreCase))
            {
                eventLogResponse.AdditionalData = await GetAspNetCore();

                string path;

                if (eventLogResponse.AdditionalData.TryGetValue("stdoutLogFile", out path))
                {
                    eventLogResponse.StdoutLogsResponse = await GetStdoutLogs(path, startTime, endTime);
                }
                else
                {
                    responseMessage.Append("No stdoutLog setting found in web.config;");
                }
                aspNetCore = true;
            }
            
            eventLogResponse.EventLogs = new List<EventLog>();
            
            List<string> eventDataList = new List<string>();
            DateTime preciseTimeStamp = new DateTime();

            if (!File.Exists(eventLogPath))
            {
                responseMessage.Append(string.Format("Unable to find eventlog.xml at {0};", eventLogPath));
                return eventLogResponse;
            }

            XmlDocument doc = new XmlDocument();
            try
            {
                doc.Load(eventLogPath);
                XmlElement root = doc.DocumentElement;
                XmlNodeList eventNodes = root.ChildNodes;

                foreach (XmlNode node in eventNodes)
                {
                    eventDataList = new List<string>();
                    XmlNode providerNode = node.SelectSingleNode("System/Provider");
                    XmlNode eventData = node.SelectSingleNode("EventData");

                    if (providerNode != null && providerNode.Attributes["Name"] != null)
                    {
                        if (aspNetCore && !string.Equals(providerNode.Attributes["Name"].Value, "IIS AspNetCore Module", StringComparison.OrdinalIgnoreCase))
                        {
                            //skip events from other providers
                            continue;
                        }
                        XmlNode timeCreated = node.SelectSingleNode("System/TimeCreated");
                        if (timeCreated != null && timeCreated.Attributes["SystemTime"] != null)
                        {
                            DateTime.TryParse(timeCreated.Attributes["SystemTime"].Value, out preciseTimeStamp);
                            preciseTimeStamp = preciseTimeStamp.ToUniversalTime();
                            if ((startTime != null && preciseTimeStamp <= startTime) || (endTime != null && preciseTimeStamp >= endTime))
                            {
                                //skip events outside of timerange
                                continue;
                            }
                        }

                        foreach (XmlNode data in eventData.ChildNodes)
                        {
                            eventDataList.Add(data.InnerText);
                        }

                        eventLogResponse.EventLogs.Add(new EventLog
                        {
                            PreciseTimeStamp = preciseTimeStamp,
                            EventData = new List<string>(eventDataList)
                        });

                        eventDataList.Clear();
                    }
                }
            }
            catch(Exception ex)
            {
                eventLogResponse.ExceptionMessage = ex.Message;
                return eventLogResponse;
            }

            eventLogResponse.ResponseMessage = responseMessage.ToString();
            return eventLogResponse;
        }
    }
}
