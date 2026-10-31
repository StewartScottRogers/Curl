namespace Curl.Extension.Protocol.Grpc.Wire;

/// <summary>
/// Thrown when bytes or headers from a gRPC peer break PROTOCOL-HTTP2.md: a Length-Prefixed-Message
/// that is truncated, too long or carries an undefined Compressed-Flag, or a response without the
/// <c>:status</c>, <c>content-type</c> or <c>grpc-status</c> the specification requires.
/// </summary>
internal sealed class GrpcWireException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GrpcWireException" /> class.
    /// </summary>
    /// <param name="message">What the peer got wrong.</param>
    public GrpcWireException(string message)
        : base(message)
    {
    }
}
