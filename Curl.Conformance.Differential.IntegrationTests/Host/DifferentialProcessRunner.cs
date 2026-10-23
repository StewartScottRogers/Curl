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
    private readonly Dictionary<(string Tag, string Name), string> _directories = [];
    private int _runCount;
    private readonly string _homeDirectory = Directory.CreateDirectory(Path.Combine(rootDirectory, "home")).FullName;

    /// <summary>Gets the working directory <paramref name="differentialCase"/> last ran in for the binary tagged <paramref name="tag"/>; every run gets a directory of its own.</summary>
    public string WorkingDirectoryOf(DifferentialCase differentialCase, string tag) => _directories[(tag, differentialCase.Name)];

    /// <summary>Runs <paramref name="differentialCase"/> through the binary <paramref name="command"/> starts; the wire is left empty.</summary>
    public async Task<DifferentialRunResult> RunAsync(IReadOnlyList<string> command, DifferentialCase differentialCase, string tag)
    {
        var workingDirectory = Directory.CreateDirectory(
            Path.Combine(rootDirectory, tag, $"{_runCount++}_{NonWordCharacter().Replace(differentialCase.Name, "_")}")).FullName;
        _directories[(tag, differentialCase.Name)] = workingDirectory;
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
        using var timer = new CancellationTokenSource(differentialCase.Timeout);
        var exitCode = await RunToExitAsync(process, differentialCase.StandardInput, timer.Token);
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

    private static async Task WriteStandardInputAsync(Process process, byte[]? standardInput, CancellationToken cancellationToken)
    {
        try
        {
            if (standardInput is not null)
            {
                await process.StandardInput.BaseStream.WriteAsync(standardInput, cancellationToken);
            }
        }
        catch (IOException)
        {
            // The binary exited without reading all of its input.
        }
        finally
        {
            CloseStandardInput(process);
        }
    }

    private static void CloseStandardInput(Process process)
    {
        try
        {
            process.StandardInput.Close();
        }
        catch (IOException)
        {
            // The pipe is already broken: the binary has gone.
        }
    }

    private static async Task<int?> RunToExitAsync(Process process, byte[]? standardInput, CancellationToken timeout)
    {
        try
        {
            await WriteStandardInputAsync(process, standardInput, timeout);
            await process.WaitForExitAsync(timeout);
            return process.ExitCode;
        }        catch (OperationCanceledException)
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
