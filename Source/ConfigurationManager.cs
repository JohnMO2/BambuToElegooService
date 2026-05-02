using System.Text.Json;

namespace BambuToElegooService;

public class ConfigurationManager
{
    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "BambuToElegooService",
        "config.json");

    public static ServiceConfiguration Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize<ServiceConfiguration>(json) ?? new ServiceConfiguration();
            }
        }
        catch
        {
            // If config is corrupted, return new config
        }

        return new ServiceConfiguration();
    }

    public static void Save(ServiceConfiguration config)
    {
        try
        {
            var directory = Path.GetDirectoryName(ConfigPath);
            if (directory != null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(ConfigPath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving configuration: {ex.Message}");
        }
    }
}
