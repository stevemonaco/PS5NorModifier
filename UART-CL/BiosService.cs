namespace UartCL;

public enum Ps5Edition { Unknown, Slim, Disc, Digital }

public class BiosService
{
    public byte[]? BiosData { get; private set; }

    private string _editionSlimTag    = "22010101";
    private string _editionDiscTag    = "22020101";
    private string _editionDigitalTag = "22030101";

    public async Task<BiosDump?> LoadBiosDump(string path)
    {
        try
        {
            var data = await File.ReadAllBytesAsync(path);
            return ParseBiosDump(data);
        }
        catch
        {
        }

        return null;
    }

    public BiosDump? ParseBiosDump(byte[] data)
    {
        // First, declare some variables to store for later use
        Ps5Edition PS5Edition;
        string? ConsoleModelInfo;
        string? ModelInfo;
        string? ConsoleSerialNumber;
        string? MotherboardSerialNumber;
        string? WiFiMac;
        string? LANMac;

        // Set the offsets of the BIN file
        long offsetOne = 0x1c7010;
        long offsetTwo = 0x1c7030;
        long serialOffset = 0x1c7210;
        long variantOffset = 0x1c7226;
        long moboSerialOffset = 0x1C7200;
        long WiFiMacOffset = 0x1C73C0;
        long LANMacOffset = 0x1C4020;

        // Declare the offset values (set them to null for now)
        string? offsetOneValue = null;
        string? offsetTwoValue = null;
        string? serialValue = null;
        string? variantValue = null;
        string? moboSerialValue = null;
        string? WiFiMacValue = null;
        string? LANMacValue = null;

        using var reader = new BinaryReader(new MemoryStream(data));

        // PS5 version
        reader.BaseStream.Position = offsetOne;
        offsetOneValue = BitConverter.ToString(reader.ReadBytes(12)).Replace("-", null);

        reader.BaseStream.Position = offsetTwo;
        offsetTwoValue = BitConverter.ToString(reader.ReadBytes(12)).Replace("-", null);

        if (offsetOneValue.Contains(_editionDiscTag))
        {
            PS5Edition = Ps5Edition.Disc;
        }
        else if (offsetTwoValue.Contains(_editionDigitalTag))
        {
            PS5Edition = Ps5Edition.Digital;
        }
        else if (offsetOneValue.Contains(_editionSlimTag) || offsetTwoValue.Contains(_editionSlimTag))
        {
            PS5Edition = Ps5Edition.Slim;
        }
        else
        {
            PS5Edition = Ps5Edition.Unknown;
        }

        reader.BaseStream.Position = variantOffset;
        variantValue = BitConverter.ToString(reader.ReadBytes(19)).Replace("-", null).Replace("FF", null);

        ConsoleModelInfo = Helpers.HexStringToString(variantValue);

        string region = "Unknown Region";
        if (ConsoleModelInfo != null && ConsoleModelInfo.Length >= 3)
        {
            string suffix = ConsoleModelInfo.Substring(ConsoleModelInfo.Length - 3);
            if (RegionMap.Map.ContainsKey(suffix))
            {
                region = RegionMap.Map[suffix];
            }
        }

        ModelInfo = Helpers.HexStringToString(variantValue) + " - " + region;

        reader.BaseStream.Position = serialOffset;
        serialValue = BitConverter.ToString(reader.ReadBytes(17)).Replace("-", null);

        if (serialValue != null)
        {
            ConsoleSerialNumber = Helpers.HexStringToString(serialValue);
            ConsoleSerialNumber = Helpers.HexStringToString(serialValue);

        }
        else
        {
            ConsoleSerialNumber = "Unknown S/N";
        }

        reader.BaseStream.Position = moboSerialOffset;
        moboSerialValue = BitConverter.ToString(reader.ReadBytes(16)).Replace("-", null);

        if (moboSerialValue != null)
        {
            MotherboardSerialNumber = Helpers.HexStringToString(moboSerialValue);
        }
        else
        {
            MotherboardSerialNumber = "Unknown S/N";
        }

        reader.BaseStream.Position = WiFiMacOffset;
        WiFiMacValue = BitConverter.ToString(reader.ReadBytes(6));

        if (WiFiMacValue != null)
        {
            WiFiMac = WiFiMacValue;
        }
        else
        {
            WiFiMac = "Unknown Mac Address";
        }

        reader.BaseStream.Position = LANMacOffset;
        LANMacValue = BitConverter.ToString(reader.ReadBytes(6));

        if (LANMacValue != null)
        {
            LANMac = LANMacValue;
        }
        else
        {
            LANMac = "Unknown Mac Address";
        }

        return new BiosDump()
        {
            Raw = data,
            Edition = PS5Edition,
            ConsoleModelInfo = ConsoleModelInfo,
            ModelInfo = ModelInfo,
            ConsoleSerialNumber = ConsoleSerialNumber,
            MotherboardSerialNumber = MotherboardSerialNumber,
            ConsoleModel = ConsoleModelInfo,
            WiFiMac = WiFiMac,
            LANMac = LANMac,
        };
    }
}