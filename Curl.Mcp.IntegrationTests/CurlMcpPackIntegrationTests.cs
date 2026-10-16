using System.Diagnostics;
using System.IO.Compression;
using System.Xml.Linq;

namespace Curl.Mcp;

/// <summary>
/// Runs <c>dotnet pack</c> on Curl.Mcp into a temporary folder and reads the package it makes
/// (BL-2388): <c>.mcp/server.json</c> inside, and the McpServer package type in its nuspec. It runs
/// the SDK as a child process and writes to the disk, so it is an Integration test (ADR-0421).
/// </summary>
[TestClass]
[TestCategory("Integration")]
[DoNotParallelize]
public sealed class CurlMcpPackIntegrationTests
{
    [TestMethod]
    public async Task Pack_MakesAnMcpServerPackageHoldingServerJson()
    {
        string output = Path.Combine(Path.GetTempPath(), $"curl-mcp-pack-{Guid.NewGuid():N}");
        try
        {
            string log = await PackAsync(output);

            string package = Path.Combine(output, "Curl.Mcp.0.1.0.nupkg");
            Assert.IsTrue(File.Exists(package), log);
            using ZipArchive archive = ZipFile.OpenRead(package);
            Assert.IsNotNull(archive.GetEntry(".mcp/server.json"));
            ZipArchiveEntry nuspec = archive.GetEntry("Curl.Mcp.nuspec")!;
            await using Stream nuspecStream = nuspec.Open();
            XDocument document = await XDocument.LoadAsync(nuspecStream, LoadOptions.None, CancellationToken.None);
            string[] packageTypes = [.. document.Descendants().Where(element => element.Name.LocalName == "packageType").Select(element => (string)element.Attribute("name")!)];
            CollectionAssert.Contains(packageTypes, "McpServer");
        }
        finally
        {
            if (Directory.Exists(output))
            {
                Directory.Delete(output, recursive: true);
            }
        }
    }

    private static async Task<string> PackAsync(string output)
    {
        ProcessStartInfo startInfo = new(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (string argument in new[] { "pack", Path.Combine(RepositoryRoot(), "Curl.Mcp", "Curl.Mcp.csproj"), "-c", "Release", "-o", output, "-nologo" })
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("dotnet pack did not start.");
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();
        using CancellationTokenSource timeout = new(TimeSpan.FromMinutes(10));
        await process.WaitForExitAsync(timeout.Token);
        string log = await standardOutput + await standardError;
        Assert.AreEqual(0, process.ExitCode, log);
        return log;
    }

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Curl.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException($"No Curl.slnx above {AppContext.BaseDirectory}.");
    }
}
