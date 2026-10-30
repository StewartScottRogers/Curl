using System.Text;
using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Arguments;

namespace Curl.Extension.HttpFile;

/// <summary>
/// Expands <c>--http-file &lt;file&gt;</c> into the curl arguments of every request in the .http file, one
/// <c>--next</c> group each (ADR-1040, ADR-1073). A <c>--http-env &lt;name&gt;</c> in the same group first sets the
/// variables of that environment in <c>http-client.env.json</c> beside the file.
/// </summary>
public sealed class HttpFileArgumentExpander : ITrailingArgumentExpander
{
    private const string Next = "--next";

    /// <inheritdoc />
    public ExtensionOption Option => HttpFileOptions.File;

    /// <inheritdoc />
    public IReadOnlyList<ExtensionOption> CompanionOptions { get; } = [HttpFileOptions.Environment];

    /// <inheritdoc />
    public bool ClaimsPositionalArgument(string argument) => false;

    /// <inheritdoc />
    public IReadOnlyList<string> ArgumentsFor(ClaimedArguments claimed, ArgumentExpansionContext context)
    {
        ArgumentNullException.ThrowIfNull(claimed);
        ArgumentNullException.ThrowIfNull(context);
        byte[] bytes = context.ReadFile(claimed.Value) ?? throw new ArgumentExpansionException($"http-file: cannot read {claimed.Value}", CurlExitCode.ReadError);
        HttpFileVariables variables = new(context.ReadEnvironmentVariable);
        foreach (CompanionOptionValue environment in claimed.CompanionValues)
        {
            HttpFileEnvironment.Load(HttpFileEnvironment.PathBeside(claimed.Value), environment.Value, context, variables);
        }

        IReadOnlyList<HttpFileRequest> requests = HttpFileParser.Parse(Encoding.UTF8.GetString(bytes).TrimStart('﻿'), variables);
        if (requests.Count == 0)
        {
            throw new ArgumentExpansionException($"http-file: no request in {claimed.Value}", CurlExitCode.FailedInit);
        }

        List<string> arguments = [.. requests[0].Arguments()];
        foreach (HttpFileRequest request in requests.Skip(1))
        {
            arguments.Add(Next);
            arguments.AddRange(request.Arguments());
        }

        return arguments;
    }
}
