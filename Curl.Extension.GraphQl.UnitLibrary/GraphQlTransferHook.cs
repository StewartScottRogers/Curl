using System.Text.Json;
using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.GraphQl;

/// <summary>
/// Sends one HTTP transfer as a GraphQL-over-HTTP request (ADR-1026): a POST of the JSON request
/// <see cref="GraphQlRequestEncoding" /> writes, with its <c>Accept</c> header, and, once the response is in,
/// one readable line on stderr for each error it reports. The response body reaches curl's output unchanged.
/// A transfer that is not HTTP runs as curl runs it.
/// </summary>
public sealed class GraphQlTransferHook : ITransferHook
{
    private readonly string? documentPath;
    private readonly bool introspect;
    private readonly string? variables;
    private readonly string? operationName;
    private readonly TextWriter errorOutput;

    private GraphQlTransferHook(string? documentPath, bool introspect, string? variables, string? operationName, TextWriter errorOutput)
    {
        this.documentPath = documentPath;
        this.introspect = introspect;
        this.variables = variables;
        this.operationName = operationName;
        this.errorOutput = errorOutput;
    }

    /// <summary>Gives the hook when an option group gave <c>--graphql</c> or <c>--graphql-introspect</c>, listing errors on stderr.</summary>
    /// <param name="values">What the option group gave the extension options.</param>
    /// <returns>The hook, or <see langword="null" /> when neither option was given.</returns>
    public static ITransferHook? ActivatedBy(ExtensionOptionValues values) => ActivatedBy(values, Console.Error);

    /// <summary>Gives the hook when an option group gave <c>--graphql</c> or <c>--graphql-introspect</c>.</summary>
    /// <param name="values">What the option group gave the extension options.</param>
    /// <param name="errorOutput">Where the response's errors are listed.</param>
    /// <returns>The hook, or <see langword="null" /> when neither option was given.</returns>
    internal static ITransferHook? ActivatedBy(ExtensionOptionValues values, TextWriter errorOutput)
    {
        ArgumentNullException.ThrowIfNull(values);
        bool introspect = values.IsGiven(GraphQlOptions.Introspect);
        string? documentPath = values.ValueOf(GraphQlOptions.Document);
        return documentPath is null && !introspect
            ? null
            : new GraphQlTransferHook(
                documentPath,
                introspect,
                values.ValueOf(GraphQlOptions.Variables),
                values.ValueOf(GraphQlOptions.Operation),
                errorOutput);
    }

    /// <inheritdoc />
    public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context, Func<ITransferContext, ValueTask<TransferResult>> next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
        if (context.Http is not { } http)
        {
            return await next(context).ConfigureAwait(false);
        }

        (byte[]? body, TransferResult? failure) = await RequestBodyAsync(context.CancellationToken).ConfigureAwait(false);
        if (failure is not null)
        {
            return failure;
        }

        HttpRequestOptions graphQlHttp = http with
        {
            Body = new BytesBody(body!, GraphQlRequestEncoding.ContentType),
            Headers = WithAccept(http.Headers),
        };
        using MemoryStream copy = new();
        TransferResult result = await next(new GraphQlTransferContext(context, new CopyingStream(context.Output, copy), graphQlHttp)).ConfigureAwait(false);
        foreach (string line in GraphQlErrorListing.LinesOf(copy.ToArray()))
        {
            await errorOutput.WriteLineAsync(line).ConfigureAwait(false);
        }

        return result;
    }

    private static IReadOnlyList<string> WithAccept(IReadOnlyList<string> headers) =>
        headers.Any(header => header.StartsWith("Accept:", StringComparison.OrdinalIgnoreCase))
            ? headers
            : [.. headers, GraphQlRequestEncoding.AcceptHeader];

    private async Task<(byte[]? Body, TransferResult? Failure)> RequestBodyAsync(CancellationToken cancellationToken)
    {
        string? query = introspect ? GraphQlIntrospectionQuery.Text : await ReadTextAsync(documentPath!, cancellationToken).ConfigureAwait(false);
        if (query is null)
        {
            return (null, TransferResult.Failure(CurlExitCode.ReadError, $"Failed to read the GraphQL document {documentPath}"));
        }

        JsonElement? variablesObject = null;
        if (variables is not null)
        {
            variablesObject = await VariablesObjectAsync(variables, cancellationToken).ConfigureAwait(false);
            if (variablesObject is null)
            {
                return (null, TransferResult.Failure(CurlExitCode.ReadError, $"GraphQL variables {variables} are not a JSON object"));
            }
        }

        return (GraphQlRequestEncoding.Encode(query, introspect ? null : operationName, variablesObject), null);
    }

    private static async Task<JsonElement?> VariablesObjectAsync(string variables, CancellationToken cancellationToken)
    {
        string? text = variables.StartsWith('@') ? await ReadTextAsync(variables[1..], cancellationToken).ConfigureAwait(false) : variables;
        return text is null ? null : GraphQlRequestEncoding.VariablesOf(text);
    }

    private static async Task<string?> ReadTextAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            return await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }
}
