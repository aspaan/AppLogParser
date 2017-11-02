using System;
using System.IO;
using System.Threading.Tasks;

namespace LogParser
{
    public class PhpLogParserLinux : PhpLogParser
    {
        public override async Task<LogResponse> FindAndSetLoggingFileAndCreateResponseObject()
        {
            var response = new LogResponse();
            response.LoggingEnabled = true;
            response.SettingsFile = Environment.ExpandEnvironmentVariables(@"/home/site/wwwroot/.htaccess");
            //response.SettingsFile = Environment.ExpandEnvironmentVariables(@"D:\Home\site\wwwroot\.user.ini");
            response.SettingsFileFound = File.Exists(response.SettingsFile);

            if (response.SettingsFileFound)
                response.LogFile = await GetLogFile(response.SettingsFile);
            else
                response.LogFile = "";

            if (!File.Exists(response.LogFile))
            {
                response.LogFile = @"/home/LogFiles/php-error.log";
            }
            response.LogFileFound = File.Exists(response.LogFile);
           
           Util.WriteLog("response.LogFile: " + response.LogFile);
           Util.WriteLog("response.LogFileFound: " + response.LogFileFound);
            //overwrite to test locally
            //response.LogFile = @"D:\Home\site\wwwroot\php_errors.log";
            //response.LogFileFound = File.Exists(response.LogFile);
            return response;
        }

        public override async Task<string> GetLogFile(string filePath)
        {
            try
            {
                var file = new StreamReader(filePath);

                string line;
                while ((line = await file.ReadLineAsync()) != null)
                {
                    if (line.ToLower().Contains("error_log"))
                    {
                        line = line.Replace("error_log", "");
                        line = line.Replace("php_value", "");
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
    }
}
