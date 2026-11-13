using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Transfer;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Composition;

/// <summary>
/// Refuses an option group that gives two extension options which each replace the request body (<c>--graphql</c>,
/// <c>--jsonrpc</c>, <c>--connect-rpc</c>, <c>--grpc-web</c>): only one body can be sent, so the transfer fails with
/// exit 2 naming both options and sends nothing, as curl refuses options it cannot use together (ADR-1262).
/// </summary>
public static class BodyReplacingHookExclusivity
{
    /// <summary>Gets the body-replacing options, in the order a refusal names them.</summary>
    internal static IReadOnlyList<ExtensionOption> Options { get; } =
    [
        GraphQl.GraphQlOptions.Document,
        Curl.Extension.JsonRpc.JsonRpcExtension.Option,
        ConnectRpc.ConnectRpcExtension.Option,
        Curl.Extension.GrpcWeb.GrpcWebExtension.Option,
    ];

    /// <summary>Gives a hook that refuses the transfer when an option group gave two body-replacing options.</summary>
    /// <param name="values">The extension options the option group gave.</param>
    /// <returns>The refusing hook, or <see langword="null" /> when at most one body-replacing option was given.</returns>
    public static ITransferHook? HookFor(ExtensionOptionValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        List<string> given = [.. GivenNames(values)];
        return given.Count < 2 ? null : new RefusingHook($"--{given[0]} and --{given[1]} cannot be used together");
    }

    /// <summary>Gives the names of the body-replacing options given; <c>--graphql-introspect</c> counts as <c>--graphql</c>'s body.</summary>
    private static IEnumerable<string> GivenNames(ExtensionOptionValues values)
    {
        if (values.IsGiven(GraphQl.GraphQlOptions.Document))
        {
            yield return GraphQl.GraphQlOptions.Document.Name;
        }
        else if (values.IsGiven(GraphQl.GraphQlOptions.Introspect))
        {
            yield return GraphQl.GraphQlOptions.Introspect.Name;
        }

        foreach (ExtensionOption option in Options.Skip(1).Where(values.IsGiven))
        {
            yield return option.Name;
        }
    }

    private sealed class RefusingHook(string message) : ITransferHook
    {
        public ValueTask<TransferResult> ExecuteAsync(ITransferContext context, Func<ITransferContext, ValueTask<TransferResult>> next) =>
            ValueTask.FromResult(TransferResult.Failure(CurlExitCode.FailedInit, message));
    }
}
