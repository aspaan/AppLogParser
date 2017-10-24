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
            response.SettingsFileFound = true;
            response.LogFileFound = File.Exists(@"/LogFiles/kudu/httpd/php-error.log");
            //overwrite to test locally
            //response.LogFile = @"D:\Home\site\wwwroot\php_errors.log";
            //response.LogFileFound = File.Exists(response.LogFile);
            return response;
        }
    }
}
