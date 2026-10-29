using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Arguments;

namespace Curl.Extension.OpenApi;

/// <summary>
/// Expands <c>--openapi &lt;spec&gt; --operation &lt;id&gt; name=value...</c> into the curl arguments that call the
/// operation (ADR-1040, ADR-1051, ADR-1075): the method, the server URL with the path and query, the header and
/// cookie parameters, and the body. A <c>--openapi-server &lt;url&gt;</c> in the same group replaces the description's
/// server URL.
/// </summary>
public sealed class OpenApiArgumentExpander : ITrailingArgumentExpander
{
    private const string NameSymbols = "_-.[]";

    /// <inheritdoc />
    public ExtensionOption Option => OpenApiOptions.Specification;

    /// <inheritdoc />
    public IReadOnlyList<ExtensionOption> CompanionOptions { get; } = [OpenApiOptions.Operation, OpenApiOptions.Server];

    /// <inheritdoc />
    /// <remarks>Takes <c>name=value</c>, where the name is letters, digits and <c>_ - . [ ]</c>; a URL has a <c>:</c> or <c>/</c> before any <c>=</c>, so it stays a URL.</remarks>
    public bool ClaimsPositionalArgument(string argument)
    {
        ArgumentNullException.ThrowIfNull(argument);
        int equals = argument.IndexOf('=', StringComparison.Ordinal);
        return equals > 0 && argument[..equals].All(character => char.IsAsciiLetterOrDigit(character) || NameSymbols.Contains(character, StringComparison.Ordinal));
    }

    /// <inheritdoc />
    public IReadOnlyList<string> ArgumentsFor(ClaimedArguments claimed, ArgumentExpansionContext context)
    {
        ArgumentNullException.ThrowIfNull(claimed);
        ArgumentNullException.ThrowIfNull(context);
        string[] operations = [.. CompanionValues(claimed, OpenApiOptions.Operation)];
        if (operations.Length != 1)
        {
            throw OpenApiFailure.Of(operations.Length == 0 ? "--operation <id> names the operation to call" : "one --operation per --openapi");
        }

        byte[] bytes = context.ReadFile(claimed.Value) ?? throw new ArgumentExpansionException($"openapi: cannot read {claimed.Value}", CurlExitCode.ReadError);
        OpenApiOperation operation = OpenApiDocument.Parse(bytes, claimed.Value).FindOperation(operations[0]);
        return OpenApiRequest.Arguments(operation, CompanionValues(claimed, OpenApiOptions.Server).LastOrDefault(), ValuesByName(claimed.PositionalArguments));
    }

    private static IEnumerable<string> CompanionValues(ClaimedArguments claimed, ExtensionOption option) =>
        claimed.CompanionValues.Where(companion => companion.Option == option).Select(companion => companion.Value);

    private static OrderedDictionary<string, List<string>> ValuesByName(IReadOnlyList<string> arguments)
    {
        OrderedDictionary<string, List<string>> values = new(StringComparer.Ordinal);
        foreach (string argument in arguments)
        {
            int equals = argument.IndexOf('=', StringComparison.Ordinal);
            string name = argument[..equals];
            if (!values.TryGetValue(name, out List<string>? named))
            {
                named = [];
                values.Add(name, named);
            }

            named.Add(argument[(equals + 1)..]);
        }

        return values;
    }
}
