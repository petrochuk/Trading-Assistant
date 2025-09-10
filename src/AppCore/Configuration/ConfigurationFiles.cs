namespace AppCore.Configuration;

public class ConfigurationFiles
{
    public static string? Directory {
        get {
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Work", "TradingAssistant");
            if (System.IO.Directory.Exists(directory)) {
                return directory;
            }

            directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documents", "Work", "TradingAssistant");
            if (System.IO.Directory.Exists(directory)) {
                return directory;
            }

            return null;
        }
    }

    public static string? UserAppSettings {
        get {
            var directory = Directory;
            if (string.IsNullOrWhiteSpace(directory)) {
                return null;
            }
            return Path.Combine(directory, "appsettings.json");
        }
    }
}
