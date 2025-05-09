namespace UartCL;

public record class BiosDump
{
    public required byte[] Raw { get; init; }
    public Ps5Edition? Edition { get; init; }
    public string? ConsoleModelInfo { get; init; }
    public string? ModelInfo { get; init; }
    public string? ConsoleSerialNumber { get; init; }
    public string? MotherboardSerialNumber { get; init; }
    public string? ConsoleModel { get; init; }
    public string? WiFiMac { get; init; }
    public string? LANMac { get; init; }
}
