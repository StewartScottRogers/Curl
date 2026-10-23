using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Redirects;

/// <summary>server.py's <c>/post-303</c>: answers 303 to <c>/echo</c>.</summary>
public sealed class Post303Route() : RedirectRoute(303)
{
    /// <inheritdoc/>
    public override bool Matches(DifferentialHttpRequest request) => request.Path == "/post-303";

    /// <inheritdoc/>
    protected override IEnumerable<(string Name, string Value)> HeadersBeforeContentLength(DifferentialResponderOptions options) =>
        [("Location", "/echo")];

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new Post303Route());
}