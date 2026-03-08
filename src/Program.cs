using CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using WinAPIMCP.Configuration;
using WinAPIMCP.MCP;
using WinAPIMCP.Services;
using WinAPIMCP.UI.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace WinAPIMCP;

public class CommandLineOptions
{
    [Option('p', "port", Required = false, Default = 3000, HelpText = "Server port")]
    public int Port { get; set; }

    [Option('s', "stdio", Required = false, Default = false, HelpText = "Run in native MCP STDIO mode (no GUI)")]
    public bool Stdio { get; set; }

    [Option('l', "log-level", Required = false, Default = "Info", HelpText = "Logging level (Debug, Info, Warn, Error)")]
    public string LogLevel { get; set; } = "Info";

    [Option("allow-elevated", Required = false, Default = false, HelpText = "Allow interaction with elevated processes")]
    public bool AllowElevated { get; set; }

    [Option('c', "config", Required = false, HelpText = "Path to configuration file")]
    public string? ConfigFile { get; set; }
}

public class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        var parseResult = Parser.Default.ParseArguments<CommandLineOptions>(args);
        var options = parseResult.MapResult(opts => opts, _ => null);
        if (options == null) return 1;

        if (!options.Stdio)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
        }

        try {
            return RunServer(options).GetAwaiter().GetResult();
        } catch (Exception ex) {
            Console.Error.WriteLine("FATAL: " + ex.ToString());
            return 1;
        }
    }

    private static async Task<int> RunServer(CommandLineOptions options)
    {
        ConfigureLogging(options.LogLevel, options.Stdio);

        IHostBuilder hostBuilder = Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices((context, services) =>
            {
                services.AddSingleton<SettingsManager>();
                services.AddSingleton<IActivityTracker, ActivityTracker>();
                services.AddSingleton<IPermissionService, PermissionService>();
                services.AddSingleton<IWindowManager, WindowManager>();
                services.AddSingleton<IProcessManager, ProcessManager>();
                services.AddSingleton<IUIInteractionManager, UIInteractionManager>();
                services.AddSingleton<IMessageSender, MessageSender>();
                services.AddSingleton<ISecurityManager, SecurityManager>();
                services.AddSingleton<WinAPIMCP.Services.Interfaces.IMemoryHijacker, WinAPIMCP.Services.MemoryHijacker>();
                services.AddSingleton<WinAPIMCP.Services.IHookPipeService, WinAPIMCP.Services.HookPipeService>();
                
                if (!options.Stdio)
                {
                    services.AddSingleton<TrayIconService>();
                }
            });

        if (!options.Stdio)
        {
            hostBuilder.ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseUrls($"http://localhost:{options.Port}");
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseCors(policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
                    app.UseEndpoints(endpoints => { endpoints.MapControllers(); });
                });
                webBuilder.ConfigureServices(services =>
                {
                    services.AddControllers().AddApplicationPart(typeof(HttpMcpTransportController).Assembly);
                    services.AddCors();
                });
            });
        }

        using var host = hostBuilder.Build();
        var settingsManager = host.Services.GetRequiredService<SettingsManager>();
        
        if (options.Port != 3000 || options.AllowElevated || !string.IsNullOrEmpty(options.ConfigFile))
        {
            settingsManager.UpdateSetting<object>(s => {
                s.Port = options.Port;
                s.AllowElevated = options.AllowElevated;
                s.ConfigFile = options.ConfigFile;
            });
        }
        
        Log.Information("Starting Windows API MCP Server ({Mode})", options.Stdio ? "STDIO" : "HTTP");
        
        if (options.Stdio)
        {
            var stdioHandler = new WinAPIMCP.MCP.StdioMcpServer(host.Services);
            await stdioHandler.RunAsync();
            return 0;
        }
        else
        {
            var trayIconService = host.Services.GetRequiredService<TrayIconService>();
            trayIconService.Initialize(host);
            Application.Run();
            return 0;
        }
    }

    private static void ConfigureLogging(string logLevel, bool stdioMode)
    {
        var level = logLevel.ToLowerInvariant() switch
        {
            "debug" => Serilog.Events.LogEventLevel.Debug,
            "info" => Serilog.Events.LogEventLevel.Information,
            "warn" => Serilog.Events.LogEventLevel.Warning,
            "error" => Serilog.Events.LogEventLevel.Error,
            _ => Serilog.Events.LogEventLevel.Information
        };

        var config = new LoggerConfiguration().MinimumLevel.Is(level);
        if (stdioMode) config.WriteTo.Console(standardErrorFromLevel: Serilog.Events.LogEventLevel.Verbose);
        else config.WriteTo.Console();
        
        config.WriteTo.File("logs/winapimcp-.log", rollingInterval: RollingInterval.Day);
        Log.Logger = config.CreateLogger();
    }
}
