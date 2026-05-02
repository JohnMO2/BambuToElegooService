using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using BambuToElegooService;
using System.Security.Principal;
using System.Diagnostics;

// Determine if running as Windows Service or interactive
var isService = !Environment.UserInteractive || args.Contains("--service");

if (isService)
{
    // Run as Windows Service
    var host = Host.CreateDefaultBuilder(args)
        .UseWindowsService(options =>
        {
            options.ServiceName = "BambuToElegooService";
        })
        .ConfigureServices(services =>
        {
            services.AddHostedService(sp =>
                new PrinterBridgeService(
                    sp.GetRequiredService<ILogger<PrinterBridgeService>>(),
                    isInteractive: false));
        })
        .Build();

    await host.RunAsync();
}
else
{
    // Check if running as administrator
    var isAdmin = IsRunningAsAdministrator();

    if (!isAdmin)
    {
        Console.WriteLine("╔═══════════════════════════════════════════════╗");
        Console.WriteLine("║   BambuLab to Elegoo Bridge Service Manager   ║");
        Console.WriteLine("╚═══════════════════════════════════════════════╝\n");
        Console.WriteLine("⚠️  ADMINISTRATOR PRIVILEGES REQUIRED");
        Console.WriteLine("\nThis application needs administrator rights to:");
        Console.WriteLine("  • Install Elegoo printer profiles to Program Files");
        Console.WriteLine("  • Copy profiles to system directories");
        Console.WriteLine("\nAttempting to restart with administrator privileges...\n");

        try
        {
            var startInfo = new ProcessStartInfo
            {
                UseShellExecute = true,
                WorkingDirectory = Environment.CurrentDirectory,
                FileName = Environment.ProcessPath ?? "BambuToElegooService.exe",
                Verb = "runas" // Request elevation
            };

            Process.Start(startInfo);
            return; // Exit this instance
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Failed to elevate privileges: {ex.Message}");
            Console.WriteLine("\nPlease right-click the executable and select 'Run as administrator'");
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
            return;
        }
    }

    // Run in interactive mode with admin privileges
    await RunInteractiveModeAsync();
}

static bool IsRunningAsAdministrator()
{
    try
    {
        var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
    catch
    {
        return false;
    }
}

static async Task RunInteractiveModeAsync()
{
    Console.WriteLine("╔═══════════════════════════════════════════════╗");
    Console.WriteLine("║   BambuLab to Elegoo Bridge Service Manager   ║");
    Console.WriteLine("╚═══════════════════════════════════════════════╝\n");

    // ============================================
    // CRITICAL FIRST STEP: Install Elegoo Profiles
    // This is the bare minimum needed to use Elegoo printers with Bambu Studio
    // ============================================
    Console.WriteLine("═══════════════════════════════════════════════");
    Console.WriteLine("STEP 1: Installing Elegoo Printer Profiles");
    Console.WriteLine("═══════════════════════════════════════════════\n");
    Console.WriteLine("ℹ️  Checking ECC profiles in Bambu Studio...");
    Console.WriteLine("   This is REQUIRED for Bambu Studio to recognize your Elegoo printer.\n");

    bool profilesInstalled = ElegooFolderInstaller.CopyElegooFolders(isInteractive: true);

    if (!profilesInstalled)
    {
        Console.WriteLine("\n⚠️  CRITICAL: Profile installation encountered issues.");
        Console.WriteLine("   ECC profiles are essential for Bambu Studio compatibility.");
        Console.WriteLine("   Without them, you cannot use Elegoo printers with Bambu Studio.\n");
        Console.Write("Continue anyway? (y/n): ");
        var response = Console.ReadLine()?.Trim().ToLower();
        if (response != "y" && response != "yes")
        {
            Console.WriteLine("\nExiting. Please resolve profile installation issues and try again.");
            Console.WriteLine("Make sure you have administrator privileges.");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
            return;
        }
    }
    else
    {
        Console.WriteLine("\n✅ ECC profiles are ready!\n");
    }

    // Now load configuration and continue with printer setup
    var config = ConfigurationManager.Load();

    // Mark profiles as installed
    if (!config.ElegooFoldersCopied)
    {
        config.ElegooFoldersCopied = true;
        ConfigurationManager.Save(config);
    }

    Console.WriteLine("═══════════════════════════════════════════════");
    Console.WriteLine("STEP 2: Printer Configuration");
    Console.WriteLine("═══════════════════════════════════════════════\n");

    // Test existing printers and remove unreachable ones
    var printersToRemove = new List<PrinterConfiguration>();

    if (config.Printers.Count > 0)
    {
        Console.WriteLine("Testing existing printer connections...\n");

        foreach (var printer in config.Printers)
        {
            Console.Write($"  Testing {printer.Name} ({printer.IpAddress})... ");

            var client = await ElegooClient.CreateAsync(printer.IpAddress, quiet: true);

            if (client == null)
            {
                Console.WriteLine("✗ UNREACHABLE");
                Console.Write($"    Remove this printer from services? (y/n): ");
                var response = Console.ReadLine()?.Trim().ToLower();

                if (response == "y" || response == "yes")
                {
                    printersToRemove.Add(printer);
                    Console.WriteLine($"    Marked for removal");
                }
            }
            else
            {
                Console.WriteLine("✓ OK");
            }
        }

        // Remove unreachable printers
        foreach (var printer in printersToRemove)
        {
            config.Printers.Remove(printer);
        }

        if (printersToRemove.Count > 0)
        {
            ConfigurationManager.Save(config);
            Console.WriteLine($"\n✓ Removed {printersToRemove.Count} printer(s) from configuration\n");
        }
        else
        {
            Console.WriteLine();
        }
    }

    // Add new printer
    Console.WriteLine("Add new printer:");
    Console.Write("  Enter printer IP address (or press Enter to skip): ");
    var newIp = Console.ReadLine()?.Trim();

    if (!string.IsNullOrWhiteSpace(newIp))
    {
        Console.WriteLine($"\n  Connecting to {newIp}...");
        var newClient = await ElegooClient.CreateAsync(newIp, quiet: false);

        if (newClient != null)
        {
            Console.Write("  Enter a friendly name for this printer: ");
            var name = Console.ReadLine()?.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                name = $"Elegoo_{newIp.Replace(".", "_")}";
            }

            // Find next available port
            var nextPort = 8080;
            while (config.Printers.Any(p => p.Port == nextPort))
            {
                nextPort++;
            }

            config.Printers.Add(new PrinterConfiguration
            {
                IpAddress = newIp,
                Name = name,
                Port = nextPort
            });

            ConfigurationManager.Save(config);
            Console.WriteLine($"\n✓ Added printer '{name}' (listening on port {nextPort})");
        }
        else
        {
            Console.WriteLine("\n✗ Could not connect to printer. Printer not added.");
        }
    }

    // Show current configuration
    Console.WriteLine("\n" + new string('─', 50));
    Console.WriteLine("Current Configuration:");
    Console.WriteLine(new string('─', 50));

    if (config.Printers.Count == 0)
    {
        Console.WriteLine("  No printers configured.");
    }
    else
    {
        foreach (var printer in config.Printers)
        {
            Console.WriteLine($"  • {printer.Name}");
            Console.WriteLine($"    IP: {printer.IpAddress}");
            Console.WriteLine($"    Port: {printer.Port}");
            Console.WriteLine();
        }
    }

    if (config.Printers.Count > 0)
    {
        Console.WriteLine(new string('─', 50));
        Console.WriteLine("\nStarting printer bridge services...\n");

        // Create a host for interactive mode
        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddHostedService(sp =>
                    new PrinterBridgeService(
                        sp.GetRequiredService<ILogger<PrinterBridgeService>>(),
                        isInteractive: true));
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders(); // Don't log to console in interactive mode
            })
            .Build();

        var cts = new CancellationTokenSource();
        var runTask = host.RunAsync(cts.Token);

        Console.WriteLine("\nPress Ctrl+C or any key to stop all services...");

        // Wait for user input
        var readTask = Task.Run(() => Console.ReadKey(true));
        await Task.WhenAny(readTask, runTask);

        Console.WriteLine("\nShutting down...");
        cts.Cancel();

        try
        {
            await runTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        Console.WriteLine("All services stopped.");
    }
    else
    {
        Console.WriteLine("No printers to start. Run the application again to add printers.");
    }

    Console.WriteLine("\nPress any key to exit...");
    Console.ReadKey();
}
