using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Slik.Node
{
    internal sealed class Startup
    {
        public static Logger SetupSerilog(string logsFolder) =>
            new LoggerConfiguration()
                .Enrich.WithThreadId()
                .MinimumLevel.Verbose()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("System", LogEventLevel.Warning)
                .WriteTo.File(Path.Combine(logsFolder, $"{Process.GetCurrentProcess().ProcessName}-.log"),
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} ({ThreadId}) [{Level:u3}] {Message:lj} {NewLine}{Exception}",
                    rollingInterval: RollingInterval.Day)
                .WriteTo.Console(LogEventLevel.Information, "[{Level:u3}] {Message:lj}{NewLine}",
                    theme: new AnsiConsoleTheme(new Dictionary<ConsoleThemeStyle, string>
                    {
                        [ConsoleThemeStyle.Text] = "\x1b[38;5;0015m",
                        [ConsoleThemeStyle.SecondaryText] = "\x1b[38;5;0007m",
                        [ConsoleThemeStyle.TertiaryText] = "\x1b[38;5;0008m",
                        [ConsoleThemeStyle.Invalid] = "\x1b[38;5;0011m",
                        [ConsoleThemeStyle.Null] = "\x1b[38;5;0027m",
                        [ConsoleThemeStyle.Name] = "\x1b[38;5;0007m",
                        [ConsoleThemeStyle.String] = "\x1b[38;5;0045m",
                        [ConsoleThemeStyle.Number] = "\x1b[38;5;0200m",
                        [ConsoleThemeStyle.Boolean] = "\x1b[38;5;0027m",
                        [ConsoleThemeStyle.Scalar] = "\x1b[38;5;0085m",
                        [ConsoleThemeStyle.LevelVerbose] = "\x1b[38;5;0007m",
                        [ConsoleThemeStyle.LevelDebug] = "\x1b[38;5;0007m",
                        [ConsoleThemeStyle.LevelInformation] = "\x1b[38;5;0002m",
                        [ConsoleThemeStyle.LevelWarning] = "\x1b[38;5;0011m",
                        [ConsoleThemeStyle.LevelError] = "\x1b[38;5;0197m",
                        [ConsoleThemeStyle.LevelFatal] = "\x1b[38;5;0015m\x1b[48;5;0196m",
                    }))
                .CreateLogger();
    }
}
