namespace BambuToElegooService;

public class PrinterConfiguration
{
    public string IpAddress { get; set; } = "";
    public string Name { get; set; } = "";
    public int Port { get; set; } = 8080;
}

public class ServiceConfiguration
{
    public List<PrinterConfiguration> Printers { get; set; } = new();
    public bool ElegooFoldersCopied { get; set; } = false;
}
