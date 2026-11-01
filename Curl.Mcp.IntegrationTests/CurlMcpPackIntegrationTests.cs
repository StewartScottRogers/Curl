using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Xml.Linq;

namespace Curl.Mcp;

/// <summary>
/// Runs <c>dotnet pack</c> on Curl.Mcp into a temporary folder and reads the package it makes
/// (BL-2388): <c>.mcp/server.json</c> inside, and the McpServer package type in its nuspec. It runs
/// the SDK as a child process and writes to the disk, so it is an Integration test (ADR-0421). Curl.Mcp.csproj
/// sets <c>PublishAot</c> (ADR-0621, BL-2517), so a plain pack makes only the tool package, never the six
/// runtime packages that once pushed the mcp-pack job past its time limit (BL-2561). The test passes no
/// <c>-p:PublishAot=true</c> of its own: a global property reaches every project Curl.Mcp references, and that
/// recompiled all of them, about 100, and made this one test take minutes on a CI runner (BL-3512). It packs in the
/// configuration this test assembly was built in, so the pack reuses that build rather than compiling every
/// referenced project again: Debug locally, Release in ci.yml's mcp-pack job. It packs with
/// <c>--disable-build-servers</c>: MSBuild nodes and the compiler server that a pack starts inherit its
/// redirected output and hold it open until they go idle, about 15 minutes, so reading that output to its end
/// waited for them.
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
        foreach (string argument in new[] { "pack", Path.Combine(RepositoryRoot(), "Curl.Mcp", "Curl.Mcp.csproj"), "-c", BuildConfiguration(), "-o", output, "-nologo", "--disable-build-servers" })
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

    private static string BuildConfiguration() =>
        typeof(CurlMcpPackIntegrationTests).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration
        ?? throw new InvalidOperationException("The test assembly has no AssemblyConfigurationAttribute.");

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
