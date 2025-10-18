namespace FantasyCalcScrape.Configuration;

public class LoggingConfiguration
{
    public string LogLevel { get; set; } = "Information";
    public bool EnableFileLogging { get; set; } = true;
    public bool EnableConsoleLogging { get; set; } = true;
    public int LogRetentionDays { get; set; } = 30;
}