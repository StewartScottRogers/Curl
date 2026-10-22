namespace Curl.Mcp;

/// <summary>
/// The folder every file a tool reads or writes must lie in, <c>CURL_MCP_ROOT</c>, and the check
/// that a path, with <c>..</c> and symbolic links resolved, lies inside it (ADR-0548, ADR-0557).
/// </summary>
public sealed class McpConfinementRoot
{
    /// <summary>The environment variable that names the root.</summary>
    public const string VariableName = "CURL_MCP_ROOT";

    /// <summary>How many symbolic links deep a resolution follows before it calls the path a loop.</summary>
    internal const int MaximumLinkDepth = 40;

    private static readonly char[] Separators = [System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar];

    private readonly string rootWithSeparator = string.Empty;

    private readonly StringComparison comparison;

    /// <summary>Creates the root for the folder <paramref name="rootPath" />, with its symbolic links resolved.</summary>
    /// <param name="rootPath">The absolute path of an existing folder.</param>
    /// <exception cref="ArgumentException"><paramref name="rootPath" /> is null, empty, relative or not an existing folder.</exception>
    public McpConfinementRoot(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        if (!System.IO.Path.IsPathFullyQualified(rootPath))
        {
            throw new ArgumentException($"{VariableName} must be an absolute path; '{rootPath}' is relative.", nameof(rootPath));
        }

        if (!Directory.Exists(rootPath))
        {
            throw new ArgumentException($"{VariableName} names '{rootPath}', which is not an existing folder.", nameof(rootPath));
        }

        ResolvedRoot = ResolveLinks(System.IO.Path.GetFullPath(rootPath), 0);
        rootWithSeparator = System.IO.Path.EndsInDirectorySeparator(ResolvedRoot)
            ? ResolvedRoot
            : ResolvedRoot + System.IO.Path.DirectorySeparatorChar;
        comparison = ComparisonFor(ResolvedRoot, Directory.Exists);
    }

    private McpConfinementRoot()
    {
    }

    /// <summary>The state with no <c>CURL_MCP_ROOT</c> set, in which every path is refused.</summary>
    public static McpConfinementRoot NotConfigured { get; } = new();

    /// <summary>The root folder with its symbolic links resolved; <see langword="null" /> when no root is set.</summary>
    public string? ResolvedRoot { get; }

    /// <summary>Whether a root is set; when it is not, <see cref="Check(string)" /> refuses every path.</summary>
    public bool IsConfigured => ResolvedRoot is not null;

    /// <summary>
    /// Reads <c>CURL_MCP_ROOT</c> through <paramref name="readVariable" />: unset is
    /// <see cref="NotConfigured" />, and any other value must name an existing folder.
    /// </summary>
    /// <param name="readVariable">Reads an environment variable by name, returning <see langword="null" /> when it is unset.</param>
    /// <returns>The root the variable names, or <see cref="NotConfigured" />.</returns>
    /// <exception cref="ArgumentException">The variable is set but empty, relative or not an existing folder.</exception>
    public static McpConfinementRoot FromEnvironment(Func<string, string?> readVariable)
    {
        ArgumentNullException.ThrowIfNull(readVariable);
        string? value = readVariable(VariableName);
        return value is null ? NotConfigured : new McpConfinementRoot(value);
    }

    /// <summary>
    /// Decides whether <paramref name="path" /> lies inside the root. A relative path resolves
    /// against the root. The path is resolved twice - as written, with <c>..</c> collapsed first
    /// as Windows does, and step by step, following each symbolic link before a <c>..</c> as Unix
    /// does - and is inside only when both lie inside. A path that does not exist yet is judged by
    /// its nearest existing ancestor.
    /// </summary>
    /// <param name="path">The path a tool would read or write.</param>
    /// <returns>The verdict; a path outside the root is a result, never an exception.</returns>
    public McpPathCheck Check(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (ResolvedRoot is null)
        {
            return new McpPathCheck(path, McpPathVerdict.NoRootConfigured, null,
                $"'{path}' cannot be used: {VariableName} is not set, so the server refuses all file access.");
        }

        try
        {
            string asWritten = ResolveLinks(System.IO.Path.GetFullPath(path, ResolvedRoot), 0);
            string stepByStep = ResolveLinks(System.IO.Path.Combine(ResolvedRoot, path), 0);
            // The readings differ only when a link precedes a "..". The path is inside only if both
            // are, so report the step-by-step one when it is outside and the as-written one otherwise.
            string resolved = IsInside(stepByStep) ? asWritten : stepByStep;
            return IsInside(resolved)
                ? new McpPathCheck(path, McpPathVerdict.Inside, resolved, $"'{path}' resolves to '{resolved}', inside {VariableName}.")
                : new McpPathCheck(path, McpPathVerdict.Outside, resolved,
                    $"'{path}' resolves to '{resolved}', which is outside {VariableName} ('{ResolvedRoot}').");
        }
        catch (Exception exception) when (IsResolutionFailure(exception))
        {
            return new McpPathCheck(path, McpPathVerdict.Unresolvable, null, $"'{path}' cannot be resolved: {exception.Message}");
        }
    }

    /// <summary>Whether <paramref name="exception" /> means a path could not be resolved, rather than a fault in the server.</summary>
    /// <param name="exception">The exception resolving the path threw.</param>
    /// <returns><see langword="true" /> for an invalid path, an input/output failure or a denied access.</returns>
    internal static bool IsResolutionFailure(Exception exception) =>
        exception is ArgumentException or IOException or UnauthorizedAccessException;

    /// <summary>
    /// Measures the file system's case rule at the root: when the root's path with every letter's
    /// case swapped names an existing folder, names compare ignoring case; otherwise, or when the
    /// path has no letters, they compare exactly, which can only refuse more.
    /// </summary>
    /// <param name="resolvedRoot">The resolved root.</param>
    /// <param name="directoryExists">Says whether a folder exists.</param>
    /// <returns>The comparison for paths under the root.</returns>
    internal static StringComparison ComparisonFor(string resolvedRoot, Func<string, bool> directoryExists)
    {
        string swapped = string.Create(resolvedRoot.Length, resolvedRoot, static (span, source) =>
        {
            for (int index = 0; index < source.Length; index++)
            {
                char letter = source[index];
                span[index] = char.IsUpper(letter) ? char.ToLowerInvariant(letter) : char.ToUpperInvariant(letter);
            }
        });
        return swapped != resolvedRoot && directoryExists(swapped) ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    }

    /// <summary>
    /// Resolves the rooted <paramref name="path" /> one name at a time: <c>..</c> leaves the folder
    /// reached so far, an existing symbolic link is replaced by its resolved target, and a name that
    /// does not exist is kept as written.
    /// </summary>
    /// <param name="path">A rooted path.</param>
    /// <param name="linkDepth">How many symbolic links deep this resolution already is.</param>
    /// <returns>The path with every existing symbolic link and every <c>..</c> resolved.</returns>
    /// <exception cref="IOException">The links nest deeper than <see cref="MaximumLinkDepth" />, as a loop does.</exception>
    internal static string ResolveLinks(string path, int linkDepth)
    {
        string current = System.IO.Path.GetPathRoot(path)!;
        foreach (string name in path[current.Length..].Split(Separators, StringSplitOptions.RemoveEmptyEntries))
        {
            current = name switch
            {
                "." => current,
                ".." => System.IO.Path.GetDirectoryName(current) ?? current,
                _ => FollowLink(System.IO.Path.Join(current, name), linkDepth),
            };
        }

        return current;
    }

    /// <summary>
    /// <paramref name="path" /> itself when it is not an existing symbolic link; otherwise its target,
    /// resolved in turn. A relative target resolves against the link's folder.
    /// </summary>
    private static string FollowLink(string path, int linkDepth)
    {
        FileInfo entry = new(path);
        // FileInfo.Attributes is -1 for a name that does not exist, which cannot be a link.
        if ((int)entry.Attributes == -1 || entry.LinkTarget is not string target)
        {
            return path;
        }

        if (linkDepth == MaximumLinkDepth)
        {
            throw new IOException($"'{path}' nests more than {MaximumLinkDepth} symbolic links deep, as a loop does.");
        }

        return ResolveLinks(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path)!, target), linkDepth + 1);
    }

    /// <summary>Whether <paramref name="resolved" /> is the root or lies under it; the trailing separator stops <c>/root-other</c> matching <c>/root</c>.</summary>
    private bool IsInside(string resolved) =>
        string.Equals(resolved, ResolvedRoot, comparison) || resolved.StartsWith(rootWithSeparator, comparison);
}
