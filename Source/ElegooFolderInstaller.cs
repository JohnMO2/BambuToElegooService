namespace BambuToElegooService;

public static class ElegooFolderInstaller
{
    public static bool CopyElegooFolders(bool isInteractive)
    {
        var exeDirectory = AppContext.BaseDirectory;
        var elegooSourcePath = Path.Combine(exeDirectory, "Elegoo");

        if (!Directory.Exists(elegooSourcePath))
        {
            if (isInteractive)
            {
                Console.WriteLine("⚠ Elegoo folder not found next to executable. Skipping ECC profile installation.");
            }
            return true; // Not a critical error
        }

        var targetPaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
                "BambuStudio", "system", "Elegoo"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), 
                "Bambu Studio", "resources", "profiles", "Elegoo")
        };

        var customPaths = new List<string>();

        for (int i = 0; i < targetPaths.Length; i++)
        {
            var targetPath = targetPaths[i];
            var pathDescription = i == 0 ? "AppData BambuStudio path" : "Program Files Bambu Studio path";

            // Check if parent directory exists
            var parentDir = Directory.GetParent(targetPath)?.FullName;

            if (parentDir == null || !Directory.Exists(parentDir))
            {
                if (isInteractive)
                {
                    Console.WriteLine($"\n⚠ Could not find {pathDescription}: {targetPath}");
                    Console.Write("Enter custom path (or press Enter to skip): ");
                    var customPath = Console.ReadLine()?.Trim();

                    if (!string.IsNullOrWhiteSpace(customPath) && Directory.Exists(Path.GetDirectoryName(customPath)))
                    {
                        customPaths.Add(customPath);
                    }
                    else
                    {
                        Console.WriteLine($"  Skipping {pathDescription}");
                        continue;
                    }
                }
                else
                {
                    // In non-interactive mode, skip missing paths
                    continue;
                }
            }
            else
            {
                customPaths.Add(targetPath);
            }
        }

        // Copy to all valid paths
        foreach (var targetPath in customPaths)
        {
            try
            {
                // Check if ECC machine profile already exists (Elegoo folder exists in every install, but ECC might not)
                var eccMachinePath = Path.Combine(targetPath, "machine", "ECC");
                if (Directory.Exists(eccMachinePath))
                {
                    if (isInteractive)
                    {
                        Console.WriteLine($"✓ ECC profiles already exist at: {targetPath}");
                    }
                    continue;
                }

                // Create target directory if it doesn't exist and copy
                Directory.CreateDirectory(targetPath);
                CopyDirectory(elegooSourcePath, targetPath);

                if (isInteractive)
                {
                    Console.WriteLine($"✓ ECC profiles copied to: {targetPath}");
                }
            }
            catch (Exception ex)
            {
                if (isInteractive)
                {
                    Console.WriteLine($"✗ Error copying to {targetPath}: {ex.Message}");
                }
            }
        }

        return true;
    }

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        var dir = new DirectoryInfo(sourceDir);

        if (!dir.Exists)
            throw new DirectoryNotFoundException($"Source directory not found: {sourceDir}");

        Directory.CreateDirectory(destDir);

        foreach (FileInfo file in dir.GetFiles())
        {
            string targetFilePath = Path.Combine(destDir, file.Name);
            file.CopyTo(targetFilePath, false);
        }

        foreach (DirectoryInfo subDir in dir.GetDirectories())
        {
            string newDestinationDir = Path.Combine(destDir, subDir.Name);
            CopyDirectory(subDir.FullName, newDestinationDir);
        }
    }
}
