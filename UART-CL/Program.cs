﻿using System.IO.Ports;
using System.Management;
using System.Text;
using System.Text.RegularExpressions;
using UartCL;

using Console = System.Console;
using ColorfulConsole = Colorful.Console;

#region Reminders (remove before publishing)
// Add check inside sub menu to confirm that the selected .bin file is a valid PS5 dump
#endregion

// Set the name of the application
string appTitle = "UART-CL by TheCod3r";

bool showMenu = false;

// Set the application title
Console.Title = appTitle;

UartDatabaseService dbService = new();

#region Obtain the friendly name of the available COM ports
static string GetFriendlyName(string portName)
{
    // Declare the friendly name variable for later use
    string friendlyName = portName;
    // We'll wrap this in a try loop simply because this isn't available on all platforms
    try
    {
        // This is basically an SQL query. Let's search for the details of the ports based on the port name
        // Again, this is just for Windows based devices
        using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE Name LIKE '%" + portName + "%'"))
        {
            // Loop through and output the friendly name
            foreach (var port in searcher.Get())
            {
                friendlyName = port["Name"].ToString();
            }
        }
    }
    // Catch errors. This would probably only happen on Linux systems
    catch(Exception ex)
    {
        // If there is an error, we'll just declare that we don't know the name of the port
        friendlyName = "Unknown Port Name";
    }
    // Send the friendly name (or unknown port name string) back to the main code for output
    return friendlyName;
}
#endregion

#region Console header
static void ShowHeader()
{
    // This is the header.
    Console.Clear();
    ColorfulConsole.WriteAscii("UART-CL v1.0.0.0");
    ColorfulConsole.WriteAscii("by TheCod3r");
    Console.WriteLine("");
    Console.WriteLine("");
    Console.WriteLine("UART-CL is a command line UART tool to assist in the diagnosis and repair of PlayStation 5 consoles using UART.");
    Console.WriteLine("For more information on how to connect to UART you can use the options below or read the ReadMe.");
    Console.WriteLine("");
}
#endregion

#region Check if error database exists
// Let's check and see if the database exists. If not, download it!
if (!dbService.IsLocalDatabaseAvailable())
{
    ShowHeader();
    Console.WriteLine("Downloading latest database file. Please wait...");
    var success = await dbService.DownloadRemoteDatabase("http://uartcodes.com/xml.php", "errorDB.xml");

    if (success)
    {
        Console.WriteLine("Database downloaded successfully...");
        showMenu = true;
    }
    else
    {
        Console.WriteLine("Could not download the latest database file. Please ensure you're connected to the internet!");
        Environment.Exit(0);
    }
}
else
{
    // The database file exists. Continue to the main menu
    showMenu = true;
}
#endregion

#region Display main menu
// Show the menu if showMenu is set to true
while (showMenu)
{
    showMenu = await MainMenu(appTitle);
}
#endregion

#region Display sub menu for working with BIOS files
static void RunSubMenu(string appTitle)
{
    string pathToDump = "";
    bool subMenuRunning = true;

    while (subMenuRunning)
    {
        // Check to see if we are working with a file. If so, add it to the app title so the user is aware...
        if(!string.IsNullOrEmpty(pathToDump))
        {
            Console.Title = appTitle + " - Working with file: " + pathToDump;
        }

        Console.ForegroundColor = ConsoleColor.Blue;
        Console.WriteLine("In order to work with a BIOS dump file, you will need to load it into memory first.");
        Console.WriteLine("Be sure to choose a file to work with by choosing option 1 before choosing any other option...");
        Console.ResetColor();
        Console.WriteLine("");
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("Choose an option:");
        Console.ResetColor();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("1. Load a BIOS dump (.bin)");
        Console.ResetColor();
        Console.WriteLine("2. View BIOS information");
        Console.WriteLine("3. Convert to Digital edition");
        Console.WriteLine("4. Convert to Disc edition");
        Console.WriteLine("5. Convert to Slim edition");
        Console.WriteLine("6. Change serial number");
        Console.WriteLine("7. Change motherboard serial number");
        Console.WriteLine("8. Change console model number");
        Console.WriteLine("X. Return to previous menu");
        Console.Write("\nEnter your choice: ");
        switch (Console.ReadLine())
        {
            #region Load a dump file
            case "1":
                Console.WriteLine("In order to work with a BIOS file, you must first choose a file to work with.");
                Console.WriteLine("This needs to be a valid .bin file containing your BIOS dump file.");
                Console.WriteLine("You will need to know the full file path of your .bin file in order to continue.");
                Console.WriteLine();

                string userInput;
                do
                {
                    Console.Write("Enter the full file path (type 'exit' to quit): ");
                    userInput = Console.ReadLine().Trim(); // Trim to remove any leading/trailing whitespace

                    if (string.IsNullOrWhiteSpace(userInput))
                    {
                        Console.WriteLine("Invalid input. File path cannot be blank.");
                    }
                    else if (userInput.Equals("exit", StringComparison.OrdinalIgnoreCase))
                    {
                        // User wants to return to the sub-menu
                        break; // Exit the current method and return to the sub-menu
                    }
                    else if (!File.Exists(userInput))
                    {
                        Console.WriteLine("The file path you entered does not exist. Please enter the path to a valid .bin file.");
                    }
                    else if (Path.GetExtension(userInput) != ".bin")
                    {
                        Console.WriteLine("The file you provided is not a .bin file. Please enter a valid .bin file path.");
                    }
                    else
                    {
                        // User provided a valid .bin file path, you can proceed with it
                        pathToDump = userInput;
                        long length = new System.IO.FileInfo(pathToDump).Length;
                        Console.WriteLine("Selected file: " + pathToDump + " - File Size: " + length.ToString() + " bytes (" + length / 1024 / 1024 + "MB)");
                        Thread.Sleep(1000);
                        break; // Exit the loop
                    }
                } while (true);
                break;
            #endregion
            #region View BIOS information
            case "2":
                // First check to confirm that we've selected a file to work with
                if(!string.IsNullOrEmpty(pathToDump)) // If the pathToDump string isn't empty or null, we can try and work with it
                {
                    // Let's try and get some information from the .bin file
                    long lengthBytes = new System.IO.FileInfo(pathToDump).Length; // File size of the .bin file in bytes
                    long lengthMB = lengthBytes / 1024 / 1024; // File size of the .bin file in MB

                    // First, declare some variables to store for later use
                    string PS5Version;
                    string ConsoleModelInfo;
                    string ModelInfo;
                    string ConsoleSerialNumber;
                    string MotherboardSerialNumber;
                    string ConsoleModel;
                    string WiFiMac;
                    string LANMac;

                    // Set the offsets of the BIN file
                    long offsetOne = 0x1c7010;
                    long offsetTwo = 0x1c7030;
                    long serialOffset = 0x1c7210;
                    long variantOffset = 0x1c7226;
                    long moboSerialOffset = 0x1C7200;
                    long WiFiMacOffset = 0x1C73C0;
                    long LANMacOffset = 0x1C4020;

                    // Declare the offset values (set them to null for now)
                    string offsetOneValue = null;
                    string offsetTwoValue = null;
                    string serialValue = null;
                    string variantValue = null;
                    string moboSerialValue = null;
                    string WiFiMacValue = null;
                    string LANMacValue = null;

                    #region Get PS5 version
                    try
                    {
                        BinaryReader reader = new BinaryReader(new FileStream(pathToDump, FileMode.Open));
                        //Set the position of the reader
                        reader.BaseStream.Position = offsetOne;
                        //Read the offset
                        offsetOneValue = BitConverter.ToString(reader.ReadBytes(12)).Replace("-", null);
                        reader.Close();
                    }
                    catch
                    {
                        // Obviously this value is invalid, so null the value and move on
                        offsetOneValue = null;
                    }

                    try
                    {
                        BinaryReader reader = new BinaryReader(new FileStream(pathToDump, FileMode.Open));
                        //Set the position of the reader
                        reader.BaseStream.Position = offsetOne;
                        //Read the offset
                        offsetTwoValue = BitConverter.ToString(reader.ReadBytes(12)).Replace("-", null);
                        reader.Close();
                    }
                    catch
                    {
                        // Obviously this value is invalid, so null the value and move on
                        offsetTwoValue = null;
                    }

                    if (offsetOneValue.Contains("22020101"))
                    {
                        PS5Version = "Disc Edition";
                    }
                    else if (offsetTwoValue.Contains("22030101"))
                    {
                        PS5Version = "Digital Edition";
                    }
                    else if (offsetOneValue.Contains("22010101") || offsetTwoValue.Contains("22010101"))
                    {
                        PS5Version = "Slim Edition";
                    }
                    else
                    {
                        PS5Version = "Unknown Model";
                    }
                    #endregion

                    #region Get console model and region
                    try
                    {
                        using (BinaryReader reader = new BinaryReader(new FileStream(pathToDump, FileMode.Open)))
                        {
                            reader.BaseStream.Position = variantOffset;
                            variantValue = BitConverter.ToString(reader.ReadBytes(19)).Replace("-", null).Replace("FF", null);
                        }
                    }
                    catch
                    {
                        // Catch any exceptions and ignore, setting variantValue to null
                    }

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
                    #endregion

                    #region Get Console Serial Number

                    try
                    {
                        BinaryReader reader = new BinaryReader(new FileStream(pathToDump, FileMode.Open));
                        //Set the position of the reader
                        reader.BaseStream.Position = serialOffset;
                        //Read the offset
                        serialValue = BitConverter.ToString(reader.ReadBytes(17)).Replace("-", null);
                        reader.Close();
                    }
                    catch
                    {
                        // Obviously this value is invalid, so null the value and move on
                        serialValue = null;
                    }



                    if (serialValue != null)
                    {
                        ConsoleSerialNumber = Helpers.HexStringToString(serialValue);
                        ConsoleSerialNumber = Helpers.HexStringToString(serialValue);

                    }
                    else
                    {
                        ConsoleSerialNumber = "Unknown S/N";
                    }

                    #endregion

                    #region Get Motherboard Serial Number

                    try
                    {
                        BinaryReader reader = new BinaryReader(new FileStream(pathToDump, FileMode.Open));
                        //Set the position of the reader
                        reader.BaseStream.Position = moboSerialOffset;
                        //Read the offset
                        moboSerialValue = BitConverter.ToString(reader.ReadBytes(16)).Replace("-", null);
                        reader.Close();
                    }
                    catch
                    {
                        // Obviously this value is invalid, so null the value and move on
                        moboSerialValue = null;
                    }



                    if (moboSerialValue != null)
                    {
                        MotherboardSerialNumber = Helpers.HexStringToString(moboSerialValue);
                    }
                    else
                    {
                        MotherboardSerialNumber = "Unknown S/N";
                    }

                    #endregion

                    #region Extract WiFi Mac Address

                    try
                    {
                        BinaryReader reader = new BinaryReader(new FileStream(pathToDump, FileMode.Open));
                        //Set the position of the reader
                        reader.BaseStream.Position = WiFiMacOffset;
                        //Read the offset
                        WiFiMacValue = BitConverter.ToString(reader.ReadBytes(6));
                        reader.Close();
                    }
                    catch
                    {
                        // Obviously this value is invalid, so null the value and move on
                        WiFiMacValue = null;
                    }

                    if (WiFiMacValue != null)
                    {
                        WiFiMac = WiFiMacValue;
                    }
                    else
                    {
                        WiFiMac = "Unknown Mac Address";
                    }

                    #endregion

                    #region Extract LAN Mac Address

                    try
                    {
                        BinaryReader reader = new BinaryReader(new FileStream(pathToDump, FileMode.Open));
                        //Set the position of the reader
                        reader.BaseStream.Position = LANMacOffset;
                        //Read the offset
                        LANMacValue = BitConverter.ToString(reader.ReadBytes(6));
                        reader.Close();
                    }
                    catch
                    {
                        // Obviously this value is invalid, so null the value and move on
                        LANMacValue = null;
                    }

                    if (LANMacValue != null)
                    {
                        LANMac = LANMacValue;
                    }
                    else
                    {
                        LANMac = "Unknown Mac Address";
                    }

                    #endregion

                    #region Start show data for the BIOS dump
                    // Start showing the data we've found to the user
                    Console.WriteLine("File size: " + lengthBytes + " bytes (" + lengthMB + "MB)"); // The file size of the .bin file
                    Console.WriteLine("PS5 Version: " + PS5Version); // Show the version info (disc/digital/slim)
                    Console.WriteLine("Console Model: " + ModelInfo); // Show the console variant (CFI-XXXX)
                    Console.WriteLine("Console Serial Number: " + ConsoleSerialNumber); // Show the serial number. This serial is the one the disc drive would use
                    Console.WriteLine("Motherboard Serial Number: " + MotherboardSerialNumber); // Show the serial number to the motherboard. This is different to the console serial
                    Console.WriteLine("WiFi Mac Address: " + WiFiMac); // WiFi mac address
                    Console.WriteLine("LAN Mac Address: " + LANMac); // LAN mac address
                    // Just a blank line
                    Console.WriteLine("");
                    Console.WriteLine("Press Enter to continue...");
                    Console.ReadLine();
                    #endregion
                    break;
                }
                else
                {
                    // No file has been selected. Let the user know
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("You must select a .bin file to read before proceeding. Please select a valid .bin file and try again.");
                    Console.ResetColor();
                    Console.WriteLine("");
                    Console.WriteLine("Press Enter to continue...");
                    Console.ReadLine();
                    break;
                }
            #endregion
            #region Convert to "Digital" edition
            case "3":
                // First check to confirm that we've selected a file to work with
                if (!string.IsNullOrEmpty(pathToDump)) // If the pathToDump string isn't empty or null, we can try and work with it
                {
                    bool confirmed = false;
                    while (!confirmed)
                    {
                        Console.ForegroundColor = ConsoleColor.Blue;
                        Console.WriteLine("Are you sure you want to set the console as \"Digital\" edition?");
                        Console.ResetColor();

                        Console.WriteLine("Type 'yes' to confirm or 'no' to cancel.");

                        string changeConfirmation = Console.ReadLine().Trim().ToLower();

                        // Check user input
                        if (changeConfirmation == "yes")
                        {
                            string PS5Version; // String to store the PS5 version

                            // Declare offsets to obtain current version info
                            long offsetOne = 0x1c7010;
                            long offsetTwo = 0x1c7030;
                            string offsetOneValue = null;
                            string offsetTwoValue = null;

                            // Get PS5 version
                            try
                            {
                                using (BinaryReader reader = new BinaryReader(new FileStream(pathToDump, FileMode.Open)))
                                {
                                    reader.BaseStream.Position = offsetOne;
                                    offsetOneValue = BitConverter.ToString(reader.ReadBytes(4)).Replace("-", string.Empty);

                                    reader.BaseStream.Position = offsetTwo;
                                    offsetTwoValue = BitConverter.ToString(reader.ReadBytes(4)).Replace("-", string.Empty);
                                }
                            }
                            catch (Exception ex)
                            {
                                // Handle any exceptions that occur while reading the file
                                Console.WriteLine("Error reading the binary file: " + ex.Message);
                                Console.WriteLine("Please try again! Press Enter to continue...");
                                Console.ReadLine();
                                confirmed = true;
                                break;
                            }

                            if (offsetOneValue.Contains("22030101") || offsetTwoValue.Contains("22030101"))
                            {
                                // The BIOS file already contains digital edition flags
                                Console.WriteLine("The .bin file you're working with is already a digital edition. No changes are needed.");
                                Console.WriteLine("Press Enter to continue...");
                                Console.ReadLine();

                                confirmed = true;
                                break;
                            }
                            else
                            {
                                // Modify the values to set the file as "Digital Edition"
                                try
                                {
                                    byte[] find = Helpers.ConvertHexStringToByteArray(Regex.Replace("22010101", "0x|[ ,]", string.Empty).Normalize().Trim());
                                    byte[] replace = Helpers.ConvertHexStringToByteArray(Regex.Replace("22030101", "0x|[ ,]", string.Empty).Normalize().Trim());

                                    byte[] bytes = File.ReadAllBytes(pathToDump);
                                    foreach (int index in Helpers.PatternAt(bytes, find))
                                    {
                                        for (int i = index, replaceIndex = 0; i < bytes.Length && replaceIndex < replace.Length; i++, replaceIndex++)
                                        {
                                            bytes[i] = replace[replaceIndex];
                                        }
                                        File.WriteAllBytes(pathToDump, bytes);
                                    }

                                    byte[] find2 = Helpers.ConvertHexStringToByteArray(Regex.Replace("22020101", "0x|[ ,]", string.Empty).Normalize().Trim());
                                    byte[] replace2 = Helpers.ConvertHexStringToByteArray(Regex.Replace("22030101", "0x|[ ,]", string.Empty).Normalize().Trim());

                                    foreach (int index in Helpers.PatternAt(bytes, find2))
                                    {
                                        for (int i = index, replaceIndex = 0; i < bytes.Length && replaceIndex < replace.Length; i++, replaceIndex++)
                                        {
                                            bytes[i] = replace2[replaceIndex];
                                        }
                                        File.WriteAllBytes(pathToDump, bytes);
                                    }

                                    Console.WriteLine("Your BIOS file has been updated successfully. The new .bin file will now report to");
                                    Console.WriteLine("the PlayStation 5 as a 'digital edition' console.");
                                    Console.WriteLine("Press Enter to continue...");
                                    Console.ReadLine();
                                    confirmed = true;
                                    break;
                                }
                                catch (Exception ex)
                                {
                                    // Handle any exceptions that occur while writing to the file
                                    Console.WriteLine("Error updating the binary file: " + ex.Message);
                                    Console.WriteLine("Please try again! Press Enter to continue...");
                                    Console.ReadLine();
                                    confirmed = true;
                                    break;
                                }
                            }
                        }
                        else if (changeConfirmation == "no")
                        {
                            // User cancelled. Break the loop and go back to the menu!
                            confirmed = true;
                            break;
                        }
                        else
                        {
                            Console.WriteLine("Invalid input. Please type 'yes' to confirm or 'no' to cancel.");
                        }
                    }
                }
                else
                {
                    // No file has been selected. Let the user know
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("You must select a .bin file to read before proceeding. Please select a valid .bin file and try again.");
                    Console.ResetColor();
                    Console.WriteLine("");
                    Console.WriteLine("Press Enter to continue...");
                    Console.ReadLine();
                    break;
                }
                break;

            #endregion
            #region Convert to "Disc" edition
            case "4":
                // First check to confirm that we've selected a file to work with
                if (!string.IsNullOrEmpty(pathToDump)) // If the pathToDump string isn't empty or null, we can try and work with it
                {
                    bool confirmed = false;
                    while (!confirmed)
                    {
                        Console.ForegroundColor = ConsoleColor.Blue;
                        Console.WriteLine("Are you sure you want to set the console as \"Disc\" edition?");
                        Console.ResetColor();

                        Console.WriteLine("Type 'yes' to confirm or 'no' to cancel.");

                        string changeConfirmation = Console.ReadLine().Trim().ToLower();

                        // Check user input
                        if (changeConfirmation == "yes")
                        {
                            string PS5Version; // String to store the PS5 version

                            // Declare offsets to obtain current version info
                            long offsetOne = 0x1c7010;
                            long offsetTwo = 0x1c7030;
                            string offsetOneValue = null;
                            string offsetTwoValue = null;

                            // Get PS5 version
                            try
                            {
                                using (BinaryReader reader = new BinaryReader(new FileStream(pathToDump, FileMode.Open)))
                                {
                                    reader.BaseStream.Position = offsetOne;
                                    offsetOneValue = BitConverter.ToString(reader.ReadBytes(4)).Replace("-", string.Empty);

                                    reader.BaseStream.Position = offsetTwo;
                                    offsetTwoValue = BitConverter.ToString(reader.ReadBytes(4)).Replace("-", string.Empty);
                                }
                            }
                            catch (Exception ex)
                            {
                                // Handle any exceptions that occur while reading the file
                                Console.WriteLine("Error reading the binary file: " + ex.Message);
                                Console.WriteLine("Please try again! Press Enter to continue...");
                                Console.ReadLine();
                                confirmed = true;
                                break;
                            }

                            if (offsetOneValue.Contains("22020101") || offsetTwoValue.Contains("22020101"))
                            {
                                // The BIOS file already contains disc edition flags
                                Console.WriteLine("The .bin file you're working with is already a disc edition. No changes are needed.");
                                Console.WriteLine("Press Enter to continue...");
                                Console.ReadLine();

                                confirmed = true;
                                break;
                            }
                            else
                            {
                                // Modify the values to set the file as "Disc Edition"
                                try
                                {
                                    byte[] find = Helpers.ConvertHexStringToByteArray(Regex.Replace("22010101", "0x|[ ,]", string.Empty).Normalize().Trim());
                                    byte[] replace = Helpers.ConvertHexStringToByteArray(Regex.Replace("22020101", "0x|[ ,]", string.Empty).Normalize().Trim());

                                    byte[] bytes = File.ReadAllBytes(pathToDump);
                                    foreach (int index in Helpers.PatternAt(bytes, find))
                                    {
                                        for (int i = index, replaceIndex = 0; i < bytes.Length && replaceIndex < replace.Length; i++, replaceIndex++)
                                        {
                                            bytes[i] = replace[replaceIndex];
                                        }
                                        File.WriteAllBytes(pathToDump, bytes);
                                    }

                                    byte[] find2 = Helpers.ConvertHexStringToByteArray(Regex.Replace("22030101", "0x|[ ,]", string.Empty).Normalize().Trim());
                                    byte[] replace2 = Helpers.ConvertHexStringToByteArray(Regex.Replace("22020101", "0x|[ ,]", string.Empty).Normalize().Trim());

                                    foreach (int index in Helpers.PatternAt(bytes, find2))
                                    {
                                        for (int i = index, replaceIndex = 0; i < bytes.Length && replaceIndex < replace.Length; i++, replaceIndex++)
                                        {
                                            bytes[i] = replace2[replaceIndex];
                                        }
                                        File.WriteAllBytes(pathToDump, bytes);
                                    }

                                    Console.WriteLine("Your BIOS file has been updated successfully. The new .bin file will now report to");
                                    Console.WriteLine("the PlayStation 5 as a 'disc edition' console.");
                                    Console.WriteLine("Press Enter to continue...");
                                    Console.ReadLine();
                                    confirmed = true;
                                    break;
                                }
                                catch (Exception ex)
                                {
                                    // Handle any exceptions that occur while writing to the file
                                    Console.WriteLine("Error updating the binary file: " + ex.Message);
                                    Console.WriteLine("Please try again! Press Enter to continue...");
                                    Console.ReadLine();
                                    confirmed = true;
                                    break;
                                }
                            }
                        }
                        else if (changeConfirmation == "no")
                        {
                            // User cancelled. Break the loop and go back to the menu!
                            confirmed = true;
                            break;
                        }
                        else
                        {
                            Console.WriteLine("Invalid input. Please type 'yes' to confirm or 'no' to cancel.");
                        }
                    }
                }
                else
                {
                    // No file has been selected. Let the user know
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("You must select a .bin file to read before proceeding. Please select a valid .bin file and try again.");
                    Console.ResetColor();
                    Console.WriteLine("");
                    Console.WriteLine("Press Enter to continue...");
                    Console.ReadLine();
                    break;
                }
                break;
            #endregion
            #region Convert to "Slim" edition
            case "5":
                // First check to confirm that we've selected a file to work with
                if (!string.IsNullOrEmpty(pathToDump)) // If the pathToDump string isn't empty or null, we can try and work with it
                {
                    bool confirmed = false;
                    while (!confirmed)
                    {
                        Console.ForegroundColor = ConsoleColor.Blue;
                        Console.WriteLine("Are you sure you want to set the console as \"Slim\" edition?");
                        Console.ResetColor();

                        Console.WriteLine("Type 'yes' to confirm or 'no' to cancel.");

                        string changeConfirmation = Console.ReadLine().Trim().ToLower();

                        // Check user input
                        if (changeConfirmation == "yes")
                        {
                            string PS5Version; // String to store the PS5 version

                            // Declare offsets to obtain current version info
                            long offsetOne = 0x1c7010;
                            long offsetTwo = 0x1c7030;
                            string offsetOneValue = null;
                            string offsetTwoValue = null;

                            // Get PS5 version
                            try
                            {
                                using (BinaryReader reader = new BinaryReader(new FileStream(pathToDump, FileMode.Open)))
                                {
                                    reader.BaseStream.Position = offsetOne;
                                    offsetOneValue = BitConverter.ToString(reader.ReadBytes(4)).Replace("-", string.Empty);

                                    reader.BaseStream.Position = offsetTwo;
                                    offsetTwoValue = BitConverter.ToString(reader.ReadBytes(4)).Replace("-", string.Empty);
                                }
                            }
                            catch (Exception ex)
                            {
                                // Handle any exceptions that occur while reading the file
                                Console.WriteLine("Error reading the binary file: " + ex.Message);
                                Console.WriteLine("Please try again! Press Enter to continue...");
                                Console.ReadLine();
                                confirmed = true;
                                break;
                            }

                            if (offsetOneValue.Contains("22010101") || offsetTwoValue.Contains("22010101"))
                            {
                                // The BIOS file already contains slim edition flags
                                Console.WriteLine("The .bin file you're working with is already a slim edition. No changes are needed.");
                                Console.WriteLine("Press Enter to continue...");
                                Console.ReadLine();

                                confirmed = true;
                                break;
                            }
                            else
                            {
                                // Modify the values to set the file as "Slim Edition"
                                try
                                {
                                    byte[] find = Helpers.ConvertHexStringToByteArray(Regex.Replace("22020101", "0x|[ ,]", string.Empty).Normalize().Trim());
                                    byte[] replace = Helpers.ConvertHexStringToByteArray(Regex.Replace("22010101", "0x|[ ,]", string.Empty).Normalize().Trim());

                                    byte[] bytes = File.ReadAllBytes(pathToDump);
                                    foreach (int index in Helpers.PatternAt(bytes, find))
                                    {
                                        for (int i = index, replaceIndex = 0; i < bytes.Length && replaceIndex < replace.Length; i++, replaceIndex++)
                                        {
                                            bytes[i] = replace[replaceIndex];
                                        }
                                        File.WriteAllBytes(pathToDump, bytes);
                                    }

                                    byte[] find2 = Helpers.ConvertHexStringToByteArray(Regex.Replace("22030101", "0x|[ ,]", string.Empty).Normalize().Trim());
                                    byte[] replace2 = Helpers.ConvertHexStringToByteArray(Regex.Replace("22010101", "0x|[ ,]", string.Empty).Normalize().Trim());

                                    foreach (int index in Helpers.PatternAt(bytes, find2))
                                    {
                                        for (int i = index, replaceIndex = 0; i < bytes.Length && replaceIndex < replace.Length; i++, replaceIndex++)
                                        {
                                            bytes[i] = replace2[replaceIndex];
                                        }
                                        File.WriteAllBytes(pathToDump, bytes);
                                    }

                                    Console.WriteLine("Your BIOS file has been updated successfully. The new .bin file will now report to");
                                    Console.WriteLine("the PlayStation 5 as a 'slim edition' console.");
                                    Console.WriteLine("Press Enter to continue...");
                                    Console.ReadLine();
                                    confirmed = true;
                                    break;
                                }
                                catch (Exception ex)
                                {
                                    // Handle any exceptions that occur while writing to the file
                                    Console.WriteLine("Error updating the binary file: " + ex.Message);
                                    Console.WriteLine("Please try again! Press Enter to continue...");
                                    Console.ReadLine();
                                    confirmed = true;
                                    break;
                                }
                            }
                        }
                        else if (changeConfirmation == "no")
                        {
                            // User cancelled. Break the loop and go back to the menu!
                            confirmed = true;
                            break;
                        }
                        else
                        {
                            Console.WriteLine("Invalid input. Please type 'yes' to confirm or 'no' to cancel.");
                        }
                    }
                }
                else
                {
                    // No file has been selected. Let the user know
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("You must select a .bin file to read before proceeding. Please select a valid .bin file and try again.");
                    Console.ResetColor();
                    Console.WriteLine("");
                    Console.WriteLine("Press Enter to continue...");
                    Console.ReadLine();
                    break;
                }
                break;
            #endregion
            #region Change serial number
            case "6":
                if (!string.IsNullOrEmpty(pathToDump)) // If the pathToDump string isn't empty or null, we can try and work with it
                {
                    // Create a true false to allow us to loop until the user changes the serial or cancels the operation
                    bool jobDone = false;

                    while (!jobDone)
                    {
                        // Set the serial number offset and value
                        long serialOffset = 0x1c7210;
                        string oldSerial = null;
                        try
                        {
                            using (BinaryReader reader = new BinaryReader(new FileStream(pathToDump, FileMode.Open)))
                            {
                                //Set the position of the reader
                                reader.BaseStream.Position = serialOffset;
                                //Read the offset
                                oldSerial = Encoding.UTF8.GetString(reader.ReadBytes(17));
                            }
                        }
                        catch
                        {
                            // Obviously this value is invalid, so null the value and move on
                            oldSerial = null;
                        }

                        bool newSerialValid = false;
                        while (!newSerialValid)
                        {
                            Console.ForegroundColor = ConsoleColor.Blue;
                            Console.WriteLine("Enter the new serial number you would like to save (type 'exit' to exit): ");
                            Console.ResetColor();

                            string newSerial = Console.ReadLine().Trim();

                            if (newSerial == "")
                            {
                                // The serial number is blank
                                Console.WriteLine("Invalid serial number entered. The new serial should be characters and letters.");
                            }
                            else if (newSerial == oldSerial)
                            {
                                Console.WriteLine("The new serial number matches the old serial number. Please enter a different value and try again...");
                            }
                            else if (newSerial == "exit")
                            {
                                jobDone = true;
                                break;
                            }
                            else
                            {
                                try
                                {
                                    byte[] existingFile;
                                    using (BinaryReader reader = new BinaryReader(new FileStream(pathToDump, FileMode.Open)))
                                    {
                                        // Get the contents of the dump into memory
                                        existingFile = reader.ReadBytes((int)reader.BaseStream.Length);
                                    }

                                    byte[] oldSerialBytes = Encoding.UTF8.GetBytes(oldSerial);
                                    byte[] newSerialBytes = Encoding.UTF8.GetBytes(newSerial);

                                    // Ensure the new serial number is either padded or truncated to fit 17 characters
                                    byte[] newSerialBytesPadded = new byte[17];
                                    Array.Copy(newSerialBytes, newSerialBytesPadded, Math.Min(newSerialBytes.Length, 17));

                                    // Find the index of the old serial number in the file
                                    int index = Helpers.PatternAt(existingFile, oldSerialBytes).FirstOrDefault();

                                    if (index != -1)
                                    {
                                        // Replace the old serial number with the new one
                                        for (int i = 0; i < newSerialBytesPadded.Length; i++)
                                        {
                                            existingFile[index + i] = newSerialBytesPadded[i];
                                        }

                                        // Write modified bytes back to the file
                                        using (BinaryWriter writer = new BinaryWriter(new FileStream(pathToDump, FileMode.Create)))
                                        {
                                            writer.Write(existingFile);
                                        }

                                        Console.WriteLine("SUCCESS: The new serial number has been updated successfully. Press enter to continue...");
                                        Console.ReadLine();
                                        jobDone = true;
                                        break;
                                    }
                                    else
                                    {
                                        Console.WriteLine("Failed to find the old serial number in the file. Aborting...");
                                        jobDone = true;
                                    }
                                }
                                catch (System.ArgumentException ex)
                                {
                                    // Something went wrong. Notify the user
                                    Console.ForegroundColor = ConsoleColor.Red;
                                    Console.WriteLine("An error occurred while attempting to make changes to your dump file. Please try again.");
                                    Console.ResetColor();
                                }
                            }
                        }
                    }
                }
                else
                {
                    // No file has been selected. Let the user know
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("You must select a .bin file to read before proceeding. Please select a valid .bin file and try again.");
                    Console.ResetColor();
                    Console.WriteLine("");
                    Console.WriteLine("Press Enter to continue...");
                    Console.ReadLine();
                    break;
                }
                break;
            #endregion
            #region Change motherboard serial number
            case "7":
                if (!string.IsNullOrEmpty(pathToDump)) // If the pathToDump string isn't empty or null, we can try and work with it
                {
                    // Declare variables to store console serial
                    long moboOffset = 0x1C7200;
                    string moboValue = null;
                    string MotherboardSerial;

                    try
                    {
                        using (BinaryReader reader = new BinaryReader(new FileStream(pathToDump, FileMode.Open)))
                        {
                            reader.BaseStream.Position = moboOffset;
                            moboValue = BitConverter.ToString(reader.ReadBytes(16)).Replace("-", null);
                        }
                    }
                    catch
                    {
                        // Catch any exceptions and ignore, setting MotherboardSerial to null
                    }

                    MotherboardSerial = Helpers.HexStringToString(moboValue);

                    if (!string.IsNullOrEmpty(moboValue) != null && !string.IsNullOrEmpty(MotherboardSerial))
                    {
                        // Create a loop to prevent the app from returning to the main menu
                        bool isDone = false;
                        while (!isDone)
                        {
                            // Show the current motherboard serial to the user
                            Console.ForegroundColor = ConsoleColor.Blue;
                            Console.WriteLine("Current motherboard serial: " + MotherboardSerial);
                            Console.ResetColor();

                            // Ask the user to enter the new serial number
                            Console.WriteLine("Please enter a new motherboard serial number (type 'exit' to go back): ");

                            string newSerial = Console.ReadLine();

                            if (string.IsNullOrEmpty(newSerial))
                            {
                                // The user did not enter a valid string
                                Console.WriteLine("Please enter a valid model number to continue...");
                            }
                            else if (newSerial.Length != 16)
                            {
                                // The entered serial number is an invalid length
                                Console.WriteLine("The new motherboard serial you entered is invalid. The motherboard serial should be exactly 16 characters in length.");
                            }
                            else if (newSerial == "exit")
                            {
                                // The user wants to exit this menu
                                isDone = true;
                                break;
                            }
                            else
                            {
                                // Everything seems OK. Now we can change the serial number
                                try
                                {
                                    byte[] oldSerial = Encoding.UTF8.GetBytes(MotherboardSerial);
                                    string oldSerialHex = Convert.ToHexString(oldSerial);

                                    byte[] newSerialBytes = Encoding.UTF8.GetBytes(newSerial);
                                    string newSerialHex = Convert.ToHexString(newSerialBytes);

                                    byte[] find = Helpers.ConvertHexStringToByteArray(Regex.Replace(oldSerialHex, "0x|[ ,]", string.Empty).Normalize().Trim());
                                    byte[] replace = Helpers.ConvertHexStringToByteArray(Regex.Replace(newSerialHex, "0x|[ ,]", string.Empty).Normalize().Trim());

                                    byte[] bytes = File.ReadAllBytes(pathToDump);
                                    foreach (int index in Helpers.PatternAt(bytes, find))
                                    {
                                        for (int i = index, replaceIndex = 0; i < bytes.Length && replaceIndex < replace.Length; i++, replaceIndex++)
                                        {
                                            bytes[i] = replace[replaceIndex];
                                        }
                                        File.WriteAllBytes(pathToDump, bytes);
                                    }

                                    Console.WriteLine("The new motherboard serial number you entered been saved successfully.");
                                    Console.WriteLine("Press Enter to continue...");
                                    Console.ReadLine();
                                    isDone = true;
                                    break;

                                }
                                catch (System.ArgumentException ex)
                                {
                                    Console.WriteLine("An error occurred while writing to the BIOS dump. Please try again..." + ex.Message);
                                    Console.WriteLine("Press Enter to continue...");
                                    Console.ReadLine();
                                    isDone = true;
                                    break;
                                }
                            }
                        }
                    }
                    else
                    {
                        // No file has been selected. Let the user know
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Could not parse your selected .bin file. Please ensure your selected file is a valid PlayStation 5 file and try again.");
                        Console.ResetColor();
                        Console.WriteLine("");
                        Console.WriteLine("Press Enter to continue...");
                        Console.ReadLine();
                        break;
                    }

                }
                else
                {
                    // No file has been selected. Let the user know
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("You must select a .bin file to read before proceeding. Please select a valid .bin file and try again.");
                    Console.ResetColor();
                    Console.WriteLine("");
                    Console.WriteLine("Press Enter to continue...");
                    Console.ReadLine();
                    break;
                }
                break;
            #endregion
            #region Change console model
            case "8":

                if (!string.IsNullOrEmpty(pathToDump)) // If the pathToDump string isn't empty or null, we can try and work with it
                {
                    // Declare variables to store console model
                    long variantOffset = 0x1c7226;
                    string variantValue = null;
                    string ConsoleModel;

                    try
                    {
                        using (BinaryReader reader = new BinaryReader(new FileStream(pathToDump, FileMode.Open)))
                        {
                            reader.BaseStream.Position = variantOffset;
                            variantValue = BitConverter.ToString(reader.ReadBytes(19)).Replace("-", null).Replace("FF", null);
                        }
                    }
                    catch
                    {
                        // Catch any exceptions and ignore, setting variantValue to null
                    }

                    ConsoleModel = Helpers.HexStringToString(variantValue);

                    if(!string.IsNullOrEmpty(variantValue) != null && !string.IsNullOrEmpty(ConsoleModel))
                    {
                        // Create a loop to prevent the app from returning to the main menu
                        bool isDone = false;
                        while (!isDone)
                        {
                            // Show the current model to the user
                            Console.ForegroundColor = ConsoleColor.Blue;
                            Console.WriteLine("Current model: " + ConsoleModel);
                            Console.ResetColor();

                            // Ask the user to enter the new model
                            Console.WriteLine("Please enter the model you would like to set your dump file to (type 'exit' to go back): ");

                            string newModel = Console.ReadLine();

                            if (string.IsNullOrEmpty(newModel))
                            {
                                // The user did not enter a valid string
                                Console.WriteLine("Please enter a valid model number to continue...");
                            } else if (newModel.Length < 9)
                            {
                                // The entered model is an invalid length
                                Console.WriteLine("The new model you entered is invalid. The model should be 9 characters long starting with 'CFI-', followed by 4 numbers and a letter.");
                            }else if (!newModel.StartsWith("CFI-"))
                            {
                                Console.WriteLine("The new model you entered is invalid. The model should be 9 characters long starting with 'CFI-', followed by 4 numbers and a letter.");
                            }else if (newModel == "exit")
                            {
                                // The user wants to exit this menu
                                isDone = true;
                                break;
                            }
                            else
                            {
                                // Everything seems OK. Now we can change the model
                                try
                                {
                                    byte[] oldModel = Encoding.UTF8.GetBytes(ConsoleModel);
                                    string oldModelHex = Convert.ToHexString(oldModel);

                                    byte[] newModelBytes = Encoding.UTF8.GetBytes(newModel);
                                    string newModelHex = Convert.ToHexString(newModelBytes);

                                    byte[] find = Helpers.ConvertHexStringToByteArray(Regex.Replace(oldModelHex, "0x|[ ,]", string.Empty).Normalize().Trim());
                                    byte[] replace = Helpers.ConvertHexStringToByteArray(Regex.Replace(newModelHex, "0x|[ ,]", string.Empty).Normalize().Trim());

                                    byte[] bytes = File.ReadAllBytes(pathToDump);
                                    foreach (int index in Helpers.PatternAt(bytes, find))
                                    {
                                        for (int i = index, replaceIndex = 0; i < bytes.Length && replaceIndex < replace.Length; i++, replaceIndex++)
                                        {
                                            bytes[i] = replace[replaceIndex];
                                        }
                                        File.WriteAllBytes(pathToDump, bytes);
                                    }

                                    Console.WriteLine("The new console model you chose has been saved successfully.");
                                    Console.WriteLine("Press Enter to continue...");
                                    Console.ReadLine();
                                    isDone = true;
                                    break;

                                }
                                catch (System.ArgumentException ex)
                                {
                                    Console.WriteLine("An error occurred while writing to the BIOS dump. Please try again..." + ex.Message);
                                    Console.WriteLine("Press Enter to continue...");
                                    Console.ReadLine();
                                    isDone = true;
                                    break;
                                }
                            }
                        }
                    }
                    else
                    {
                        // No file has been selected. Let the user know
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Could not parse your selected .bin file. Please ensure your selected file is a valid PlayStation 5 file and try again.");
                        Console.ResetColor();
                        Console.WriteLine("");
                        Console.WriteLine("Press Enter to continue...");
                        Console.ReadLine();
                        break;
                    }

                }
                else
                {
                    // No file has been selected. Let the user know
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("You must select a .bin file to read before proceeding. Please select a valid .bin file and try again.");
                    Console.ResetColor();
                    Console.WriteLine("");
                    Console.WriteLine("Press Enter to continue...");
                    Console.ReadLine();
                    break;
                }
                break;
            #endregion
            #region Exit sub menu
            // I put two cases here for the exit option, one for capital "X" and one for lower case.
            case "X":
                // We should reset the app title first!
                Console.Title = appTitle;
                subMenuRunning = false;
                break;
            case "x":
                // We should reset the app title first!
                Console.Title = appTitle;
                subMenuRunning = false;
                break;
                #endregion
        }
    }

}
        #endregion

#region Main
static async Task<bool> MainMenu(string appTitle)
{
    var dbService = new UartDatabaseService();

    Console.Clear();
    ShowHeader();
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("Choose an option:");
    Console.ResetColor();
    Console.WriteLine("1. Get error codes from PS5");
    Console.WriteLine("2. Clear error codes on PS5");
    Console.WriteLine("3. Enter custom UART command");
    Console.WriteLine("4. BIOS Dump Tools");
    Console.WriteLine("5. View readme guide");
    // Thanks for leaving this here!
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("6. Buy TheCod3r a coffee");
    Console.ResetColor();
    Console.WriteLine("7. Update error database");
    Console.WriteLine("X. Exit application");
    Console.ForegroundColor = ConsoleColor.Red;
    Console.Write("\nEnter your choice: ");
    Console.ResetColor();

    #region Menu Options
    switch (Console.ReadLine())
    {
        #region Get Error Codes From PS5
        case "1":
            // Declare a variable to store the selected COM port
            string selectedPort;
            // Declare a string array for a list of available port names
            string[] ports = SerialPort.GetPortNames();

            // No COM ports found. Let the user know and go back to the main menu
            if (ports.Length == 0)
            {
                Console.WriteLine("No communication devices were found on this system.");
                Console.WriteLine("Please insert a UART compatible device and try again.");
                Console.WriteLine("Press Enter to continue...");
                Console.ReadLine();
                return true;
            }

            // Devices were found. Iterate through and present them in the form of a menu
            Console.WriteLine("Available devices:");
            for (int i = 0; i < ports.Length; i++)
            {
                string friendlyName = GetFriendlyName(ports[i]);
                Console.WriteLine($"{i + 1}. {ports[i]} - {friendlyName}");
            }

            // Declare an integer for the index of the selected port
            int selectedPortIndex;
            // Add each port to the ports array
            do
            {
                Console.WriteLine("");
                Console.Write("Enter the number of the COM port you want to use: ");
            } while (!int.TryParse(Console.ReadLine(), out selectedPortIndex) || selectedPortIndex < 1 || selectedPortIndex > ports.Length);

            // Get the selected port and store it inside the selectedPort string
            selectedPort = ports[selectedPortIndex - 1];

            // Select and lock the chosen device
            SerialPort serialPort = new SerialPort(selectedPort);
            // Configure settings for the selected device
            serialPort.BaudRate = 115200; // The PS5 requires a BAUD rate of 115200
            serialPort.RtsEnable = true; // We need to enable ready to send (RTS) mode
            // Now we can get a list of error codes. We're going to wrap this in a try loop to prevent unexpected crashes
            try
            {
                // Open the selected port for use
                serialPort.Open();
                // Let's display the selected port (change the color to blue) so the user is aware of what device is in use
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine("Selected port: " + GetFriendlyName(selectedPort));
                // Reset the foreground color to default before proceeding
                Console.ResetColor();

                // Let's start grabbing error codes from the PS5
                int loopLimit = 10;
                // Create a list to store error codes in
                List<string> UARTLines = new();

                // When grabbing error codes, we want to grab the first 10 errors from the system. Let's create a loop
                for (var i = 0; i <= loopLimit; i++)
                {
                    // Create a command variable depending on what number we're at in the loop (where "i" is the current number)
                    var command = $"errlog {i}";
                    // Add the checksum to the command
                    var checksum = Helpers.CalculateChecksum(command);
                    // Send the current command to the UART device
                    serialPort.WriteLine(checksum);

                    // Read the UART response
                    var line = serialPort.ReadLine();

                    // Ensure we have a valid response, then add it to the errors list
                    if (!string.Equals($"{command}:{checksum:X2}", line, StringComparison.InvariantCultureIgnoreCase))
                    {
                        if (!line.Contains("errlog"))
                        {
                            // Let's make sure we haven't already added the same error code to the error list
                            // This way we only show each error code once and keep the output window clean
                            if (!UARTLines.Contains(line))
                            {
                                UARTLines.Add(line);
                            }
                        }
                    }
                }

                // Now let's iterate through the lines and show them to the user
                foreach (var l in UARTLines)
                {
                    var split = l.Split(' ');
                    if (!split.Any()) continue;
                    switch (split[0])
                    {
                        case "NG":
                            break;
                        case "OK":
                            var errorCode = split[2];
                            if (errorCode.StartsWith("FFFFFF"))
                            {
                                // The returned code is blank
                                Console.ForegroundColor = ConsoleColor.Blue;
                                Console.WriteLine("No error displayed");
                                Console.ResetColor();
                            }
                            else
                            {
                                // Now that the error code has been isolated from the rest of the junk sent by the system
                                // let's check it against the database. The error database will need to return XML results
                                Console.ForegroundColor = ConsoleColor.Green;
                                string errorResult = dbService.ParseErrors(errorCode);
                                Console.WriteLine(errorResult);
                                Console.ResetColor();
                            }
                            break;
                        default:
                            break;
                    }
                }

                // Wait for user input
                Console.WriteLine("");
                Console.WriteLine("Press Enter to continue...");
                Console.ReadLine();

                // Before exiting, close and free up the selected device
                serialPort.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred while connecting to your selected device.");
                Console.WriteLine("Error details:");
                Console.WriteLine(ex.Message);
                Console.WriteLine("Press Enter to continue...");
                Console.ReadLine();
            }

            return true;
        #endregion
        #region Clear UART codes
        case "2":
            // Declare a string array for a list of available port names
            ports = SerialPort.GetPortNames();

            // No COM ports found. Let the user know and go back to the main menu
            if (ports.Length == 0)
            {
                Console.WriteLine("No communication devices were found on this system.");
                Console.WriteLine("Please insert a UART compatible device and try again.");
                Console.WriteLine("Press Enter to continue...");
                Console.ReadLine();
                return true;
            }

            // Devices were found. Iterate through and present them in the form of a menu
            Console.WriteLine("Available devices:");
            for (int i = 0; i < ports.Length; i++)
            {
                string friendlyName = GetFriendlyName(ports[i]);
                Console.WriteLine($"{i + 1}. {ports[i]} - {friendlyName}");
            }

            // Add each port to the ports array
            do
            {
                Console.WriteLine("");
                Console.Write("Enter the number of the COM port you want to use: ");
            } while (!int.TryParse(Console.ReadLine(), out selectedPortIndex) || selectedPortIndex < 1 || selectedPortIndex > ports.Length);

            // Get the selected port and store it inside the selectedPort string
            selectedPort = ports[selectedPortIndex - 1];

            // Select and lock the chosen device
            serialPort = new SerialPort(selectedPort);
            // Configure settings for the selected device
            serialPort.BaudRate = 115200; // The PS5 requires a BAUD rate of 115200
            serialPort.RtsEnable = true; // We need to enable ready to send (RTS) mode
            // Now we can wipe the error codes. We're going to wrap this in a try loop to prevent unexpected crashes
            try
            {
                // Open the selected port for use
                serialPort.Open();
                // Let's display the selected port (change the color to blue) so the user is aware of what device is in use
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine("Selected port: " + GetFriendlyName(selectedPort));
                // Reset the foreground color to default before proceeding
                Console.ResetColor();

                var checksum = Helpers.CalculateChecksum("errlog clear");
                serialPort.WriteLine(checksum);

                List<string> UARTLines = new();

                do
                {
                    var line = serialPort.ReadLine();
                    UARTLines.Add(line);
                } while (serialPort.BytesToRead != 0);

                foreach (var l in UARTLines)
                {
                    Console.WriteLine(l);
                }

                Console.WriteLine("Press Enter to continue...");

                // Job done. Continue
                Console.ReadLine();

                // Before exiting, close and free up the selected device
                serialPort.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred while connecting to your selected device.");
                Console.WriteLine("Error details:");
                Console.WriteLine(ex.Message);
                Console.WriteLine("Press Enter to continue...");
                Console.ReadLine();
            }
            return true;
        #endregion
        #region Custom UART command
        case "3":
            // Declare a string array for a list of available port names
            ports = SerialPort.GetPortNames();

            // No COM ports found. Let the user know and go back to the main menu
            if (ports.Length == 0)
            {
                Console.WriteLine("No communication devices were found on this system.");
                Console.WriteLine("Please insert a UART compatible device and try again.");
                Console.WriteLine("Press Enter to continue...");
                Console.ReadLine();
                return true;
            }

            // Devices were found. Iterate through and present them in the form of a menu
            Console.WriteLine("Available devices:");
            for (int i = 0; i < ports.Length; i++)
            {
                string friendlyName = GetFriendlyName(ports[i]);
                Console.WriteLine($"{i + 1}. {ports[i]} - {friendlyName}");
            }

            // Add each port to the ports array
            do
            {
                Console.WriteLine("");
                Console.Write("Enter the number of the COM port you want to use: ");
            } while (!int.TryParse(Console.ReadLine(), out selectedPortIndex) || selectedPortIndex < 1 || selectedPortIndex > ports.Length);

            // Get the selected port and store it inside the selectedPort string
            selectedPort = ports[selectedPortIndex - 1];

            // Select and lock the chosen device
            serialPort = new SerialPort(selectedPort);
            // Configure settings for the selected device
            serialPort.BaudRate = 115200; // The PS5 requires a BAUD rate of 115200
            serialPort.RtsEnable = true; // We need to enable ready to send (RTS) mode

            // Now we can run the custom command. We're going to wrap this in a try loop to prevent unexpected crashes
            try
            {
                // Open the selected port for use
                serialPort.Open();
                // Let's display the selected port (change the color to blue) so the user is aware of what device is in use
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine("Selected port: " + GetFriendlyName(selectedPort));
                // Reset the foreground color to default before proceeding
                Console.ResetColor();

                while (true) // Loop until user provides a valid command or 'exit'
                {
                    // Ask the user for their custom UART command
                    Console.Write("Please enter a custom command to send (type exit to quit): ");
                    // Get the command which the user entered
                    string UARTCommand = Console.ReadLine();

                    // If the user types exit, we want to return to the main menu
                    if (UARTCommand == "exit")
                    {
                        break; // Exit the while loop and return to the main menu
                    }
                    else if (!string.IsNullOrEmpty(UARTCommand)) // If the command is not empty or null
                    {
                        var checksum = Helpers.CalculateChecksum(UARTCommand);
                        serialPort.WriteLine(checksum);

                        List<string> UARTLines = new();

                        do
                        {
                            var line = serialPort.ReadLine();
                            UARTLines.Add(line);
                        } while (serialPort.BytesToRead != 0);

                        foreach (var l in UARTLines)
                        {
                            Console.WriteLine(l);
                        }

                        Console.WriteLine("Press Enter to continue...");
                        Console.ReadLine();
                    }
                    else
                    {
                        // The user didn't type anything. We need to ask them to type their command again!
                        Console.WriteLine("Please enter a valid command.");
                    }
                }

                // Before exiting, close and free up the selected device
                serialPort.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred while connecting to your selected device.");
                Console.WriteLine("Error details:");
                Console.WriteLine(ex.Message);
                Console.WriteLine("Press Enter to continue...");
                Console.ReadLine();
            }
            return true;
        #endregion
        #region BIOS Dump Tools (Sub Menu)
        case "4":
            // This is a sub menu for working with BIOS files. IMO it's not very elegant to have sub menus but at least this way
            // we don't have to use different apps for working with BIOS files...

            RunSubMenu(appTitle);
            return true;
        #endregion
        #region Launch readme
        case "5":
            Console.WriteLine("UARL-CL is designed with simplicity in mind. This command line application makes it quick and easy");
            Console.WriteLine("to obtain error codes from your PlayStation 5 console.");
            Console.WriteLine("UART stands for Universal Asynchronous Receiver-Transmitter. UART allows you to send and receive commands");
            Console.WriteLine("to any compatible serial communications device.");
            Console.WriteLine();
            Console.WriteLine("The PlayStation 5 has UART functionality built in. Unfortunately Sony don't make it easy to understand what");
            Console.WriteLine("is happening with the machine when you request error codes, which is why this application exists. UART-CL is");
            Console.WriteLine("a command-line spin off to the PS5 NOR and UART tool for Windows, which allows you to communicate via serial");
            Console.WriteLine("to your PlayStation 5. You can grab error codes from the system at the click of a button (well, a few clicks)");
            Console.WriteLine("and the software will automatically check the error codes received and attempt to convert them into plain text.");
            Console.WriteLine("This is done by splitting the error codes up into useful sections and then comparing those error codes against a");
            Console.WriteLine("database of codes collected by the repair community. If the code exists in the database, the application will");
            Console.WriteLine("automatically grab the error details and output them for you on the screen so you can figure out your next move.");
            Console.WriteLine("");
            Console.WriteLine("Where does the database come from?");
            Console.WriteLine("The database is downloaded on first launch from uartcodes.com/xml.php. The download page is hosted by \"TheCod3r\"");
            Console.WriteLine("for free and is simply a PHP script which fetches all known error codes from the uartcodes.com database and converts");
            Console.WriteLine("them into an XML document. An XML document makes it quick and easy to work with and it's a more elegant solution to");
            Console.WriteLine("provide a database which doesn't rely on an internet connection. It can also be updated whenever you like, free of charge.");
            Console.WriteLine("");
            Console.WriteLine("How do I use this to fix my PS5?");
            Console.WriteLine("You'll need a compatible serial communication (UART) device first of all. Most devices that have a transmit, receive, and");
            Console.WriteLine("ground pin and that can provide 3.3v instead of 5v should work, and you can buy one for a few bucks on eBay, Amazon or AliExpress.");
            Console.WriteLine("");
            Console.WriteLine("Once you have a compatible device, you'll need to:");
            Console.WriteLine("Solder the transmit pin on the device to the receive pin on the PS5.");
            Console.WriteLine("Solder the receive pin on the device to the transmit pin on the PS5.");
            Console.WriteLine("Solder ground on the device to ground on the PS5.");
            Console.WriteLine("Connect the PS5 power chord to the PS5 power supply (do not turn on the console)");
            Console.WriteLine("Use this software and select either option 1, 2 or 3 to run commands.");
            Console.WriteLine("Choose your device from the list of available devices.");
            Console.WriteLine("Let the software do the rest. Then working out a plan for the actual repair is up to you. We can't do everything ;)");
            Console.WriteLine("");
            Console.WriteLine("As a personal note from myself (TheCod3r). I want to thank you for trusting my software. I'm an electronics technician primarily");
            Console.WriteLine("and I write code for fun (ironic since my name is TheCod3r, I know!). I would also like to thank the following people for inspiring me:");
            Console.WriteLine("");
            Console.WriteLine("Louis Rossmann, FightToRepair.org, Jessa Jones (iPad Rehab), Andy-Man (PS5 Wee Tools), my YouTube viewers, my Patreon supporters and my mom!");
            Console.WriteLine("");
            Console.WriteLine("#FuckBwE");
            Console.WriteLine("Be sure to use the hashtag. It really pisses him off!");

            Console.WriteLine("Press Enter to continue...");
            Console.ReadLine();
            return true;
        #endregion
        #region Buy me a coffee
        case "6":
            // Thanks for buying me a coffee :)
            Console.WriteLine("Thanks for buying me a coffee. I'll redirect you in your default browser...");
            Helpers.OpenUrl("https://www.streamelements.com/thecod3r/tip");
            Console.WriteLine("Press Enter to continue...");
            Console.ReadLine();
            return true;
        #endregion
        #region Update XML database
        case "7":
            Console.WriteLine("Downloading latest database file. Please wait...");

            bool success = await dbService.DownloadRemoteDatabase("http://uartcodes.com/xml.php", "errorDB.xml");

            if (success)
            {
                Console.WriteLine("Database downloaded successfully...");
                Console.WriteLine("Press Enter to continue...");
            }
            else
            {
                Console.WriteLine("Could not download the latest database file. Please ensure you're connected to the internet!");
                Console.WriteLine("Press Enter to continue...");
            }
            Console.ReadLine();
            return true;
        default:
            Console.WriteLine("Invalid choice. Please try again.");
            Console.WriteLine("Press Enter to continue...");
            Console.ReadLine();
            return true;
        #endregion
        #region Exit Application
        case "X":
            // Run the exit environment command to close the application
            Environment.Exit(0);
            return true;
        case "x":
            // Run the exit environment command to close the application
            Environment.Exit(0);
            return true;
            #endregion
    }
    #endregion
}
#endregion