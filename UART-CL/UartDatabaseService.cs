using System.Xml;

namespace UartCL;

public interface IUartDatabaseService
{
    string ParseErrors(string errorCode);

    bool IsLocalDatabaseAvailable();
    Task<bool> DownloadRemoteDatabase(string url, string savePath);
}

public sealed class UartDatabaseService : IUartDatabaseService
{
    // When fetching errors from the PS5 we want to be able to convert the received codes into readable text to make it easier
    // for the user to understand what the problem is. By the time this function is called we should have an up to date XML
    // database to compare error codes with.
    public string ParseErrors(string errorCode)
    {
        string results = "";

        try
        {
            // Check if the XML file exists
            if (File.Exists("errorDB.xml"))
            {
                // Load the XML file
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.Load("errorDB.xml");

                // Get the root node
                XmlNode root = xmlDoc.DocumentElement;

                // Check if the root node is <errorCodes>
                if (root.Name == "errorCodes")
                {
                    // No error was found in the database
                    if (root.ChildNodes.Count == 0)
                    {
                        results = "No result found for error code " + errorCode;
                    }
                    else
                    {
                        // Loop through each errorCode node
                        foreach (XmlNode errorCodeNode in root.ChildNodes)
                        {
                            // Check if the node is <errorCode>
                            if (errorCodeNode.Name == "errorCode")
                            {
                                // Get ErrorCode and Description
                                string errorCodeValue = errorCodeNode.SelectSingleNode("ErrorCode").InnerText;
                                string description = errorCodeNode.SelectSingleNode("Description").InnerText;

                                // Check if the current error code matches the requested error code
                                if (errorCodeValue == errorCode)
                                {
                                    // Output the results
                                    results = "Error code: " + errorCodeValue + Environment.NewLine + "Description: " + description;
                                    break; // Exit the loop after finding the matching error code
                                }
                            }
                        }
                    }
                }
                else
                {
                    results = "Error: Invalid XML database file. Please reconfigure the application, redownload the offline database, or uncheck the option to use the offline database.";
                }
            }
            else
            {
                results = "Error: Local XML file not found.";
            }
        }
        catch (Exception ex)
        {
            results = "Error: " + ex.Message;
        }

        return results;
    }

    public bool IsLocalDatabaseAvailable()
    {
        return File.Exists("errorDB.xml");
    }
    
    public async Task<bool> DownloadRemoteDatabase(string url, string savePath)
    {
        using (HttpClient client = new HttpClient())
        {
            try
            {
                var content = await client.GetStringAsync(url);
                await File.WriteAllTextAsync(savePath, content);
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }
    }
}
