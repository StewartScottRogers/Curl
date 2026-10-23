using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Curl.Conformance.Differential.IntegrationTests.Host;

/// <summary>
/// Runs one <see cref="DifferentialCase"/> through one binary in its own working directory, as
/// the tester's <c>runner.py</c> <c>run()</c> did: the case's files written first, <c>{U}</c> and
/// <c>{CWD}</c> substituted, no <c>*proxy*</c> variable and an empty home, a timeout reported as
/// a <see langword="null"/> exit code, and the files the run wrote or changed collected afterwards.
/// </summary>
internal sealed partial class DifferentialProcessRunner(string rootDirectory, string baseUrl)
{
    private readonly string _homeDirectory = Directory.CreateDirectory(Path.Combine(rootDirectory, "home")).FullName;

    /// <summary>Gets the working directory <paramref name="differentialCase"/> runs in for the binary tagged <paramref name="tag"/>.</summary>
    public string WorkingDirectoryOf(DifferentialCase differentialCase, string tag) =>
        Path.Combine(rootDirectory, tag, NonWordCharacter().Replace(differentialCase.Name, "_"));

    /// <summary>Runs <paramref name="differentialCase"/> through the binary <paramref name="command"/> starts; the wire is left empty.</summary>
    public async Task<DifferentialRunResult> RunAsync(IReadOnlyList<string> command, DifferentialCase differentialCase, string tag)
    {
        var workingDirectory = Directory.CreateDirectory(WorkingDirectoryOf(differentialCase, tag)).FullName;
        foreach (var (name, content) in differentialCase.Files)
        {
            var path = Path.Combine(workingDirectory, name);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllBytesAsync(path, content);
        }

        var stopwatch = Stopwatch.StartNew();
        using var process = Process.Start(CreateStartInfo(command, differentialCase, workingDirectory))!;
        var standardOutput = ReadAllAsync(process.StandardOutput.BaseStream);
        var standardError = ReadAllAsync(process.StandardError.BaseStream);
        await WriteStandardInputAsync(process, differentialCase.StandardInput);
        var exitCode = await WaitForExitAsync(process, differentialCase.Timeout);
        var elapsed = stopwatch.Elapsed;
        return new DifferentialRunResult(
            exitCode, await standardOutput, await standardError, CollectFiles(workingDirectory, differentialCase.Files), [], elapsed);
    }

    private ProcessStartInfo CreateStartInfo(IReadOnlyList<string> command, DifferentialCase differentialCase, string workingDirectory)
    {
        var start = new ProcessStartInfo(command[0])
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in command.Skip(1))
        {
            start.ArgumentList.Add(argument);
        }

        foreach (var argument in differentialCase.Arguments)
        {
            start.ArgumentList.Add(argument.Replace("{U}", baseUrl, StringComparison.Ordinal).Replace("{CWD}", workingDirectory, StringComparison.Ordinal));
        }

        foreach (var name in start.Environment.Keys.Where(name => name.Contains("proxy", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            start.Environment.Remove(name);
        }

        foreach (var name in (string[])["HOME", "USERPROFILE", "CURL_HOME", "APPDATA"])
        {
            start.Environment[name] = _homeDirectory;
        }

        return start;
    }

    private static async Task WriteStandardInputAsync(Process process, byte[]? standardInput)
    {
        try
        {
            if (standardInput is not null)
            {
                await process.StandardInput.BaseStream.WriteAsync(standardInput);
            }

            process.StandardInput.Close();
        }
        catch (IOException)
        {
            // The binary exited without reading all of its input.
        }
    }

    private static async Task<int?> WaitForExitAsync(Process process, TimeSpan timeout)
    {
        using var timer = new CancellationTokenSource(timeout);
        try
        {
            await process.WaitForExitAsync(timer.Token);
            return process.ExitCode;
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            return null;
        }
    }

    private static async Task<byte[]> ReadAllAsync(Stream stream)
    {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        return buffer.ToArray();
    }

    private static Dictionary<string, byte[]> CollectFiles(string workingDirectory, IReadOnlyDictionary<string, byte[]> caseFiles)
    {
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFiles(workingDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(workingDirectory, path);
            var content = File.ReadAllBytes(path);
            var written = caseFiles.TryGetValue(relative, out var original) ? original
                : caseFiles.TryGetValue(relative.Replace('\\', '/'), out var slashed) ? slashed : null;
            if (written is null || !content.AsSpan().SequenceEqual(written))
            {
                files[relative] = content;
            }
        }

        return files;
    }

    [GeneratedRegex(@"\W")]
    private static partial Regex NonWordCharacter();
}
