using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Web;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace LogAnalyzer.Controllers
{
    public class LogEnabler
    {
        public async Task<List<string>> EnableLogging(string stack, bool enable)
        {

            switch (stack.ToLower())
            {
                case "php":
                default:
                    return await EnablePHPLoging(enable);
                    
            }
        }

        public async Task<bool> IsEnabled(string stack)
        {

            switch (stack.ToLower())
            {
                case "php":
                default:
                    return await IsPhpEnabled();

            }
        }

        private async Task<bool> IsPhpEnabled()
        {

            //for .user.ini
            var settingsFileDir = Environment.ExpandEnvironmentVariables(@"%HOME%\site\wwwroot\");

            var settingsFileName = ".user.ini";
            var settingsFile = Path.Combine(settingsFileDir, settingsFileName);
            var setting = "log_errors";

            if (File.Exists(settingsFile))
            {
                var lineNumber = ContainsSettings(settingsFile, setting);
                if (lineNumber > -1)
                {
                    string[] arrLine = File.ReadAllLines(settingsFile);
                    var userIniSettingEnabled =  (arrLine[lineNumber].ToLower().Replace(" ", "") == "log_errors=on");

                    //for word press sites
                    settingsFileName = "wp-config.php";
                    settingsFile = Path.Combine(settingsFileDir, settingsFileName);
                    setting = "define('WP_DEBUG'";

                    if (!File.Exists(settingsFile))
                    {
                        return userIniSettingEnabled;
                    }

                    lineNumber = ContainsSettings(settingsFile, setting);
                    if (lineNumber > -1)
                    {
                        arrLine = File.ReadAllLines(settingsFile);
                        var wordPressEnabled = (arrLine[lineNumber].ToLower().Replace(" ", "") == "define('wp_debug',true);");
                        return userIniSettingEnabled && wordPressEnabled;
                    }
                }

                return false;
            }
            return false;
        }

        private async Task<List<string>> EnablePHPLoging(bool enable)
        {
            List<string> log = new List<string>(); ;

            //for .user.ini
            var settingsFileDir = Environment.ExpandEnvironmentVariables(@"%HOME%\site\wwwroot\");

            var settingsFileName = ".user.ini";
            var settingsFile = Path.Combine(settingsFileDir, settingsFileName);
            var settingString = enable?"log_errors=On": "log_errors=Off";
            var setting = "log_errors";

            if (!File.Exists(settingsFile) && enable)
            {
                CreateSettingsFile(settingsFile, settingString);
                log.Add(string.Format("Created the setting file {0} with the following setting: {1}", settingsFileName, settingString));
            }
            else
            {
                AddOrUpdateSetting(settingsFileDir, settingsFileName, settingString, setting, log);
            }

            //for word press sites
            settingsFileName = "wp-config.php";
            settingsFile = Path.Combine(settingsFileDir, settingsFileName);
            settingString = "define('WP_DEBUG', "+ enable.ToString().ToLower() + ");";
            setting = "define('WP_DEBUG'";

            if (File.Exists(settingsFile))
            {
                AddOrUpdateSetting(settingsFileDir, settingsFileName, settingString, setting, log);
            }

            return log;
        }

        private void AddOrUpdateSetting(string settingsFileDir, string settingsFileName, string settingString, string setting, List<string> log)
        {
            var settingsFile = Path.Combine(settingsFileDir, settingsFileName);

            // make a backup
            var backupFile = MakeBackUp(settingsFileDir, settingsFileName);
            try
            {
                var lineToEdit = ContainsSettings(settingsFile, setting);
                if (lineToEdit == -1)
                {
                    AddSettingToFile(settingsFile, settingString);
                    log.Add(string.Format("Added setting: {1}, to file {0}", settingsFileName, settingString));
                }
                else
                {
                    UpdateSetting(settingsFile, lineToEdit, settingString);
                    log.Add(string.Format("Updated setting: {1}, in file {0}", settingsFileName, settingString));
                }
            }
            catch (Exception ex)
            {
                //restore from backup if there is any issue
                File.Copy(settingsFile, Path.Combine(settingsFileDir, backupFile), true);
                File.Delete(Path.Combine(settingsFileDir, backupFile));
                throw ex;
            }

            //Delete backup
            File.Delete(Path.Combine(settingsFileDir, backupFile));

        }
        private void UpdateSetting(string settingsFile, int lineToEdit, string setting)
        {
            string[] arrLine = File.ReadAllLines(settingsFile);
            arrLine[lineToEdit] = setting;
            File.WriteAllLines(settingsFile, arrLine);
        }

        private void AddSettingToFile(string settingsFile, string setting)
        {
            using (StreamWriter sw = File.AppendText(settingsFile))
            {
                sw.WriteLine(setting);
            }
        }

        private void CreateSettingsFile(string settingsFile, string setting)
        {
            
            using (FileStream fs = File.Create(settingsFile))
            {
                Byte[] info = new UTF8Encoding(true).GetBytes(setting);
                // Add some information to the file.
                fs.Write(info, 0, info.Length);
            }
        }

        private string MakeBackUp(string settingsFileDir, string settingsFileName)
        {
            string name = Guid.NewGuid().ToString();
            var stringBackUpfileName = settingsFileName + "_" + name;
            File.Copy(Path.Combine(settingsFileDir, settingsFileName), Path.Combine(settingsFileDir, stringBackUpfileName), true);
            return stringBackUpfileName;
        }

        private int ContainsSettings(string filePath, string setting)
        {
            string line;
            var file = new StreamReader(filePath);
            int lineNumber = -1;
            int count = -1;
            while ((line = file.ReadLine()) != null)
            {
                ++count;
                if (line.ToLower().Trim().StartsWith(setting.ToLower()))
                {
                    lineNumber = count;
                    break;
                }
            }

            file.Close();
            return lineNumber;
        }
        
    }
}