using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using HtmlAgilityPack;

class Program
{
    static async Task Main(string[] args)
    {

        Console.WriteLine("PKHeX and PKHeX-Plugins downloader (stable releases)");
        Console.WriteLine("Please report any issues with this setup file via GitHub issues at https://github.com/santacrab2/PKHeX-Plugins/issues");
        Console.WriteLine();

        // Check for network path locations
        string[] networkPaths = { "OneDrive", "Dropbox", "Mega" };
        string currentPath = Directory.GetCurrentDirectory();
        string pluginsRepo = "santacrab2/PKHeX-Plugins";
        string baseRepo = "kwsch/PKHeX";
        string releasesUrl = $"https://api.github.com/repos/{pluginsRepo}/releases";
        string baseReleasesUrl = $"https://api.github.com/repos/{baseRepo}/releases";
        string basePKHeXUrl = "https://projectpokemon.org/home/files/file/1-pkhex/";
        string downloadUrl = string.Empty;
        string pluginsFile = "PKHeX-Plugins.zip";
        string tag = string.Empty;
        foreach (var path in networkPaths)
        {
            if (currentPath.Contains(path, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"WARNING: {path} is detected on your system. Please move the setup file to a different location before running the program.");
                Console.ReadLine();
                return;
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
                Console.WriteLine("Determining latest plugin release ...");

                var pluginsResponse = await client.GetStringAsync(releasesUrl);
                var baseResponse = await client.GetStringAsync(baseReleasesUrl);

                var pluginsRelease = JsonDocument.Parse(pluginsResponse).RootElement[0].GetProperty("tag_name").GetString();
                var baseRelease = JsonDocument.Parse(baseResponse).RootElement[0].GetProperty("tag_name").GetString();

                if (!pluginsRelease!.Equals(baseRelease, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Auto-Legality Mod for the latest stable PKHeX has not been released yet.");
                    Console.WriteLine("Please wait for a new PKHeX-Plugins release before using this setup file.");
                    Console.WriteLine("Alternatively, consider reading the wiki to manually setup ALM with an older PKHeX build.");
                    Console.ReadLine();
                    return;
                }
                tag = pluginsRelease;
                Console.WriteLine("Fetching the correct download page for PKHeX...");

                // Step 1: Load the HTML content
                var response = await client.GetStringAsync(basePKHeXUrl);
                Console.WriteLine("Page content fetched. Debugging HTML:");

                // Step 2: Load HTML into HtmlAgilityPack
                var document = new HtmlDocument();
                document.LoadHtml(response);

                // Step 3: Debug and extract the correct link
                var linkNode = document.DocumentNode
                    .SelectNodes("//a[contains(@href, 'do=download')]")
                    ?.FirstOrDefault();

                if (linkNode == null)
                    return;
                

                downloadUrl = linkNode.GetAttributeValue("href", string.Empty).Replace("&amp;", "&");

                if (!downloadUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    downloadUrl = $"https://projectpokemon.org{downloadUrl}";
                }

                Console.WriteLine($"Download link obtained: {downloadUrl}");

                // Step 4: Download the file
                Console.WriteLine("Downloading PKHeX...");
                var fileResponse = await client.GetAsync(downloadUrl);
                fileResponse.EnsureSuccessStatusCode();

                await using (var fileStream = new FileStream("PKHeX.zip", FileMode.Create))
                {
                    await fileResponse.Content.CopyToAsync(fileStream);
                }

                Console.WriteLine("PKHeX downloaded successfully as PKHeX.zip.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
        using (var client = new HttpClient(new HttpClientHandler { UseCookies = true }))
        {
            // Download PKHeX-Plugins.zip
            Console.WriteLine($"Downloading latest PKHeX-Plugin Release: {tag}");
            string downloadPluginUrl = $"https://github.com/{pluginsRepo}/releases/download/{tag}/{pluginsFile}";

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
            ZipFile.ExtractToDirectory("PKHeX.zip", Directory.GetCurrentDirectory());

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

            ZipFile.ExtractToDirectory(pluginsFile, "plugins");

            // Delete PKHeX-Plugins.zip
            Console.WriteLine("Deleting PKHeX-Plugins.zip ...");
            File.Delete(pluginsFile);

            Console.WriteLine("PKHeX and Plugins setup completed.");
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
