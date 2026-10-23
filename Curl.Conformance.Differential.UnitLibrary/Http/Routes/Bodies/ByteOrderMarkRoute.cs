using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Bodies;

/// <summary>server.py's <c>/bom</c>: a body that starts with the UTF-8 byte order mark, with no <c>Content-Type</c>; the connection is kept.</summary>
public sealed class ByteOrderMarkRoute : FixedPathRoute
{
    /// <inheritdoc/>
    public override string Path => "/bom";

    /// <inheritdoc/>
    protected override DifferentialHttpResponse BuildResponse(DifferentialHttpRequest request) =>
        new(200, [], "﻿bom start\n"u8.ToArray());

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new ByteOrderMarkRoute());
}
