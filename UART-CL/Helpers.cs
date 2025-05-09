using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace UartCL;

public static class Helpers
{
    public static string CalculateChecksum(string str)
    {
        // Math stuff. I don't understand it either!
        int sum = 0;
        foreach (char c in str)
        {
            sum += (int)c;
        }
        return str + ":" + (sum & 0xFF).ToString("X2");
    }

    public static string HexStringToString(string hexString)
    {
        if (hexString == null || (hexString.Length & 1) == 1)
        {
            throw new ArgumentException();
        }
        var sb = new StringBuilder();
        for (var i = 0; i < hexString.Length; i += 2)
        {
            var hexChar = hexString.Substring(i, 2);
            sb.Append((char)Convert.ToByte(hexChar, 16));
        }
        return sb.ToString();
    }

    public static IEnumerable<int> PatternAt(byte[] source, byte[] pattern)
    {
        for (int i = 0; i < source.Length; i++)
        {
            if (source.Skip(i).Take(pattern.Length).SequenceEqual(pattern))
            {
                yield return i;
            }
        }
    }

    public static byte[] ConvertHexStringToByteArray(string hexString)
    {
        if (hexString.Length % 2 != 0)
        {
            throw new ArgumentException(String.Format(CultureInfo.InvariantCulture, "The binary key cannot have an odd number of digits: {0}", hexString));
        }

        byte[] data = new byte[hexString.Length / 2];
        for (int index = 0; index < data.Length; index++)
        {
            string byteValue = hexString.Substring(index * 2, 2);
            data[index] = Convert.ToByte(byteValue, 16); // Parse hex string directly
        }

        return data;
    }

    public static void OpenUrl(string url)
    {
        // Let's wait two seconds first
        Thread.Sleep(2000);
        // Wrap this in a try loop so we don't get any unexpected crashes
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            // Catch any errors and let the user know
            Console.WriteLine($"Error opening URL: {ex.Message}");
        }
    }
}
