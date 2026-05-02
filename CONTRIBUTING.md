# Contributing to BambuToElegoo Bridge Service

Thank you for your interest in contributing! 🎉

## How to Contribute

### Reporting Bugs

Before creating bug reports, please check existing issues to avoid duplicates. When creating a bug report, include:

- **Clear title and description**
- **Steps to reproduce** the issue
- **Expected vs actual behavior**
- **Screenshots** if applicable
- **Environment details**:
  - Windows version
  - .NET version
  - Printer model
  - BambuStudio/OrcaSlicer version

### Suggesting Features

Feature suggestions are welcome! Please:

- **Check existing issues** to avoid duplicates
- **Clearly describe** the feature and its benefits
- **Explain use cases** - how would this help users?
- **Consider implementation** - any technical constraints?

### Pull Requests

1. **Fork the repository**
2. **Create a feature branch**:
   ```bash
   git checkout -b feature/amazing-feature
   ```

3. **Make your changes**:
   - Follow existing code style
   - Add comments for complex logic
   - Update documentation if needed

4. **Test thoroughly**:
   - Test in both interactive and service modes
   - Test with multiple printers if possible
   - Ensure no regressions

5. **Commit with clear messages**:
   ```bash
   git commit -m "Add feature: detailed description"
   ```

6. **Push to your fork**:
   ```bash
   git push origin feature/amazing-feature
   ```

7. **Open a Pull Request**:
   - Describe what changed and why
   - Reference any related issues
   - Include testing details

## Development Setup

### Prerequisites

- **Visual Studio 2022+** or **.NET 10.0 SDK**
- **Windows 10/11**
- **Git**
- **Elegoo printer** (for testing)

### Getting Started

1. Clone your fork:
   ```bash
   git clone https://github.com/YOUR-USERNAME/BambuToElegooService.git
   cd BambuToElegooService
   ```

2. Open in Visual Studio or VS Code

3. Build the solution:
   ```bash
   dotnet build
   ```

4. Run in debug mode:
   ```bash
   dotnet run
   ```

## Code Style

- **Use meaningful variable names**
- **Add XML comments** for public methods
- **Keep methods focused** - one responsibility per method
- **Handle exceptions** appropriately
- **Follow C# naming conventions**:
  - PascalCase for classes, methods, properties
  - camelCase for local variables
  - _camelCase for private fields

### Example

```csharp
/// <summary>
/// Uploads a file to the specified printer
/// </summary>
/// <param name="filePath">Full path to the G-code file</param>
/// <param name="printerIp">IP address of the target printer</param>
/// <returns>True if upload succeeded, false otherwise</returns>
public async Task<bool> UploadFileAsync(string filePath, string printerIp)
{
    try
    {
        // Implementation here
    }
    catch (Exception ex)
    {
        _logger.LogError($"Upload failed: {ex.Message}");
        return false;
    }
}
```

## Project Structure

```
BambuToElegooService/
├── Program.cs                  # Entry point, dual-mode logic
├── ElegooClient.cs            # Printer communication
├── OctoPrintServer.cs         # HTTP server (OctoPrint API)
├── PrinterBridgeService.cs    # Windows Service implementation
├── ConfigurationManager.cs    # JSON config handling
├── PrinterConfiguration.cs    # Data models
├── EccFolderInstaller.cs      # Profile installation
└── install-service.ps1        # Installation script
```

## Testing

### Manual Testing Checklist

- [ ] Interactive mode starts correctly
- [ ] Printer connection test works
- [ ] Configuration is saved properly
- [ ] Service installs successfully
- [ ] Service starts automatically
- [ ] File uploads work
- [ ] Auto-print starts successfully
- [ ] Multiple printers work
- [ ] Service survives reboot

### Adding Tests

We welcome unit tests! Currently, the project doesn't have automated tests, but contributions to add testing infrastructure are highly appreciated.

## Documentation

When adding features:

- Update **README.md** if user-facing
- Update **CHANGELOG.md** with changes
- Add code comments for complex logic
- Update XML documentation comments

## Questions?

- Open a [Discussion](https://github.com/yourusername/BambuToElegooService/discussions)
- Check existing [Issues](https://github.com/yourusername/BambuToElegooService/issues)
- Review the [README](README.md)

## Code of Conduct

Be respectful and constructive. We're all here to make 3D printing better! 🖨️

---

**Thank you for contributing!** Every contribution, no matter how small, makes a difference. 🙏
