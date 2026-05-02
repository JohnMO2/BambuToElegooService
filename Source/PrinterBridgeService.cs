using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BambuToElegooService;

public class PrinterBridgeService : BackgroundService
{
    private readonly ILogger<PrinterBridgeService> _logger;
    private readonly List<PrinterInstance> _printerInstances = new();
    private readonly bool _isInteractive;

    public PrinterBridgeService(ILogger<PrinterBridgeService> logger, bool isInteractive = false)
    {
        _logger = logger;
        _isInteractive = isInteractive;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // FIRST PRIORITY: Install Elegoo profiles (required for Bambu Studio)
        Log("═══════════════════════════════════════════════");
        Log("Installing Elegoo Printer Profiles...");
        Log("═══════════════════════════════════════════════");

        var config = ConfigurationManager.Load();

        // Always attempt to install profiles (idempotent operation)
        Log("Checking for ECC profiles in Bambu Studio...");
        try
        {
            if (ElegooFolderInstaller.CopyElegooFolders(_isInteractive))
            {
                config.ElegooFoldersCopied = true;
                ConfigurationManager.Save(config);
                Log("✓ ECC profiles verified/installed successfully");
            }
            else
            {
                Log("⚠ Profile installation completed with warnings");
            }
        }
        catch (Exception ex)
        {
            Log($"⚠ Profile installation error: {ex.Message}");
            Log("Service will continue, but Bambu Studio may not recognize printer");
        }

        Log("═══════════════════════════════════════════════");
        Log("Starting Printer Bridge Services...");
        Log("═══════════════════════════════════════════════");

        // Start all configured printers
        foreach (var printerConfig in config.Printers)
        {
            try
            {
                Log($"Starting service for printer: {printerConfig.Name} ({printerConfig.IpAddress})");

                var elegooClient = await ElegooClient.CreateAsync(printerConfig.IpAddress);

                if (elegooClient != null)
                {
                    var server = new OctoPrintServer(printerConfig.Port, elegooClient, _isInteractive);
                    await server.StartAsync();

                    _printerInstances.Add(new PrinterInstance
                    {
                        Configuration = printerConfig,
                        Server = server,
                        Client = elegooClient
                    });

                    Log($"✓ Printer service started: {printerConfig.Name} on port {printerConfig.Port}");
                }
                else
                {
                    Log($"✗ Could not connect to printer: {printerConfig.Name} ({printerConfig.IpAddress})");
                }
            }
            catch (Exception ex)
            {
                Log($"✗ Error starting printer service {printerConfig.Name}: {ex.Message}");
            }
        }

        if (_printerInstances.Count == 0)
        {
            Log("No printer services running. Service will remain active for monitoring.");
        }

        // Keep service running
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        Log("Stopping all printer services...");

        foreach (var instance in _printerInstances)
        {
            try
            {
                await instance.Server.StopAsync();
                Log($"Stopped: {instance.Configuration.Name}");
            }
            catch (Exception ex)
            {
                Log($"Error stopping {instance.Configuration.Name}: {ex.Message}");
            }
        }

        await base.StopAsync(cancellationToken);
    }

    private void Log(string message)
    {
        if (_isInteractive)
        {
            Console.WriteLine(message);
        }
        _logger.LogInformation(message);
    }

    private class PrinterInstance
    {
        public PrinterConfiguration Configuration { get; set; } = null!;
        public OctoPrintServer Server { get; set; } = null!;
        public ElegooClient Client { get; set; } = null!;
    }
}
