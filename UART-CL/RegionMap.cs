namespace UartCL;

public static class RegionMap
{
    public static Dictionary<string, string> Map { get; } = new Dictionary<string, string>
    {
        { "00A", "Japan" },
        { "00B", "Japan" },
        { "01A", "US, Canada, (North America)" },
        { "01B", "US, Canada, (North America)" },
        { "15A", "US, Canada, (North America)" },
        { "15B", "US, Canada, (North America)" },
        { "02A", "Australia / New Zealand, (Oceania)" },
        { "02B", "Australia / New Zealand, (Oceania)" },
        { "03A", "United Kingdom / Ireland" },
        { "03B", "United Kingdom / Ireland" },
        { "04A", "Europe / Middle East / Africa" },
        { "04B", "Europe / Middle East / Africa" },
        { "05A", "South Korea" },
        { "05B", "South Korea" },
        { "06A", "Southeast Asia / Hong Kong" },
        { "06B", "Southeast Asia / Hong Kong" },
        { "07A", "Taiwan" },
        { "07B", "Taiwan" },
        { "08A", "Russia, Ukraine, India, Central Asia" },
        { "08B", "Russia, Ukraine, India, Central Asia" },
        { "09A", "Mainland China" },
        { "09B", "Mainland China" },
        { "11A", "Mexico, Central America, South America" },
        { "11B", "Mexico, Central America, South America" },
        { "14A", "Mexico, Central America, South America" },
        { "14B", "Mexico, Central America, South America" },
        { "16A", "Europe / Middle East / Africa" },
        { "16B", "Europe / Middle East / Africa" },
        { "18A", "Singapore, Korea, Asia" },
        { "18B", "Singapore, Korea, Asia" }
    };
}
