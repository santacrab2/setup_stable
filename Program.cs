
using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using HtmlAgilityPack;

class Program
{ // Check for network path locations
    static string[] networkPaths = { "OneDrive", "Dropbox", "Mega" };
    static string currentPath = Directory.GetCurrentDirectory();
    static string basePKHeXUrl = "https://projectpokemon.org/home/files/file/1-pkhex/";
    static string downloadUrl = string.Empty;
    static string pluginsFile = "PKHeX-Plugins.zip";
    static string? pluginsRelease = string.Empty;
    static string? mode = "";
    static async Task Main(string[] args)
    {
        Console.WriteLine("Press Enter for Stable. Type 'dev' for development build.");
        mode = Console.ReadLine();
        if(mode == "dev")
            basePKHeXUrl = "https://projectpokemon.org/home/files/file/2445-pkhex-development-build/";

        await DownloadStable();
        Console.WriteLine("Press Enter to Exit.");
        Console.ReadLine();
    }
    private async static Task<bool> DownloadStable()
    {
        Console.WriteLine(mode == "dev"? "PKHeX and PKHeX-Plugins downloader (development builds)":"PKHeX and PKHeX-Plugins downloader (stable releases)");
        Console.WriteLine("Please report any issues with this setup file via GitHub issues at https://github.com/santacrab2/PKHeX-Plugins/issues");
        Console.WriteLine();

       
        foreach (var path in networkPaths)
        {
            if (currentPath.Contains(path, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"WARNING: {path} is detected on your system. Please move the setup file to a different location before running the program.");
                Console.ReadLine();
                return false;
            }
        }

        // Close any open instances of PKHeX
        var pkhexProcesses = Process.GetProcessesByName("pkhex");
        foreach (var process in pkhexProcesses)
        {
            process.Kill();
        }

        // Determine the latest stable plugin release
        using (var client = new HttpClient(new HttpClientHandler { UseCookies = true }))
        {
            // Set user-agent header for GitHub API request
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (compatible; PKHeXDownloader)");

            try
            {
                if (mode != "dev")
                {
                    Console.WriteLine("Determining latest plugin release ...");

                    var pluginsResponse = await client.GetStringAsync("https://api.github.com/repos/santacrab2/PKHeX-Plugins/releases");
                    var baseResponse = await client.GetStringAsync("https://api.github.com/repos/kwsch/PKHeX/releases");

                    pluginsRelease = JsonDocument.Parse(pluginsResponse).RootElement[0].GetProperty("tag_name").GetString();
                    var baseRelease = JsonDocument.Parse(baseResponse).RootElement[0].GetProperty("tag_name").GetString();

                    if (!pluginsRelease!.Equals(baseRelease, StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine("Auto-Legality Mod for the latest stable PKHeX has not been released yet.");
                        Console.WriteLine("Please wait for a new PKHeX-Plugins release before using this setup file.");
                        Console.WriteLine("Alternatively, consider reading the wiki to manually setup ALM with an older PKHeX build.");
                        Console.ReadLine();
                        return false;
                    }
                }
                Console.WriteLine("Fetching the correct download page for PKHeX...");

                // Step 1: Load the HTML content
                var response = await client.GetStringAsync(basePKHeXUrl);

                // Step 2: Load HTML into HtmlAgilityPack
                var document = new HtmlDocument();
                document.LoadHtml(response);

                // Step 3: Debug and extract the correct link
                var linkNode = document.DocumentNode
                    .SelectNodes("//a[contains(@href, 'do=download')]")
                    ?.FirstOrDefault();

                if (linkNode == null)
                    return false;


                downloadUrl = linkNode.GetAttributeValue("href", string.Empty).Replace("&amp;", "&");

                if (!downloadUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    downloadUrl = $"https://projectpokemon.org{downloadUrl}";
                }

                // Step 4: Download the file
                Console.WriteLine("Downloading PKHeX...");
                var fileResponse = await client.GetAsync(downloadUrl);
                fileResponse.EnsureSuccessStatusCode();

                await using (var fileStream = new FileStream("PKHeX.zip", FileMode.Create))
                {
                    await fileResponse.Content.CopyToAsync(fileStream);
                }

                Console.WriteLine("PKHeX downloaded successfully as PKHeX.zip.");

                // Download PKHeX-Plugins.zip
                Console.WriteLine(mode != "dev" ? $"Downloading latest PKHeX-Plugin Release: {pluginsRelease}" : "Downloading latest commit PKHeX-Plugin");
                string downloadPluginUrl = $"https://github.com/santacrab2/PKHeX-Plugins/releases/download/{pluginsRelease}/{pluginsFile}";
                if (mode == "dev")
                {
                    var pluginsResponse = await client.GetStringAsync("https://dev.azure.com/santacrab2/6b94199c-1e18-4ecc-9df5-7957a6984c60/_apis/build/builds?definitions=1&$top=1&resultFilter=succeeded&api-version=6.0");
                    var jsonResponse = JsonDocument.Parse(pluginsResponse);
                    var buildId = jsonResponse.RootElement.GetProperty("value")[0].GetProperty("id").ToString();
                    var artifactResponse = await client.GetStringAsync($"https://dev.azure.com/santacrab2/6b94199c-1e18-4ecc-9df5-7957a6984c60/_apis/build/builds/{buildId}/artifacts?artifactName=PKHeX-Plugins&api-version=6.0");
                    var artifactJsonResponse = JsonDocument.Parse(artifactResponse);
                    downloadPluginUrl = artifactJsonResponse.RootElement.GetProperty("resource").GetProperty("downloadUrl").ToString();

                }
                var pluginResponse = await client.GetAsync(downloadPluginUrl);
                pluginResponse.EnsureSuccessStatusCode();

                using (var pluginFileStream = new FileStream(pluginsFile, FileMode.Create))
                {
                    await pluginResponse.Content.CopyToAsync(pluginFileStream);
                }

                Console.WriteLine("PKHeX-Plugins downloaded successfully as PKHeX-Plugins.zip.");

                // Cleanup old files if they exist
                Console.WriteLine("Cleaning up previous releases if they exist ...");
                CleanupOldFiles();

                // Extract PKHeX
                Console.WriteLine("Extracting PKHeX ...");
                ZipFile.ExtractToDirectory("PKHeX.zip", currentPath, true);

                // Delete PKHeX.zip
                Console.WriteLine("Deleting PKHeX.zip ...");
                File.Delete("PKHeX.zip");

                // Unblock Plugins and Extract
                Console.WriteLine("Unblocking and extracting PKHeX-Plugins ...");
                UnblockFile(pluginsFile);

                if (!Directory.Exists("plugins"))
                {
                    Directory.CreateDirectory("plugins");
                }

                ZipFile.ExtractToDirectory(pluginsFile, "plugins", true);
                //when downloading the artifact directly from azure there is an extra folder
                //in the zip that needs to be handled
                if(mode == "dev")
                {
                    File.Move("plugins/PKHeX-Plugins/AutoModPlugins.dll", "plugins/AutoModPlugins.dll");
                    Directory.Delete("plugins/PKHeX-Plugins");
                }
                // Delete PKHeX-Plugins.zip
                Console.WriteLine("Deleting PKHeX-Plugins.zip ...");
                File.Delete(pluginsFile);

                Console.WriteLine("PKHeX and Plugins setup completed.");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return false;
            }
        }
        
    }
    
    private static void CleanupOldFiles()
    {
        string[] filesToDelete = new string[]
        {
            "plugins/AutoModPlugins.*",
            "plugins/PKHeX.Core.AutoMod.*",
            "plugins/QRPlugins.*",
            "PKHeX.exe",
            "PKHeX.Core.*",
            "PKHeX.exe.*",
            "PKHeX.pdb",
            "PKHeX.Drawing.*",
            "QRCoder.dll"
        };

        foreach (var filePattern in filesToDelete)
        {
            try
            {
                var files = Directory.GetFiles(Directory.GetCurrentDirectory(), filePattern);
                foreach (var file in files)
                {
                    File.Delete(file);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to delete {filePattern}: {ex.Message}");
            }
        }
    }

    // Method to unblock files on Windows (Unblock-File equivalent)
    private static void UnblockFile(string filePath)
    {
        if (File.Exists(filePath))
        {
            File.SetAttributes(filePath, File.GetAttributes(filePath) & ~FileAttributes.ReadOnly);
            File.SetAttributes(filePath, File.GetAttributes(filePath) & ~FileAttributes.Hidden);
        }
    }
}
