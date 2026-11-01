namespace Curl.Extended.Protocol.McpClient.StreamableHttp;

/// <summary>An MCP Streamable HTTP exchange failed for the reason <see cref="Failure"/> names.</summary>
public sealed class McpHttpException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="McpHttpException"/> class.</summary>
    /// <param name="failure">Why the exchange failed.</param>
    /// <param name="message">A description of the failure.</param>
    /// <param name="statusCode">The response's status code, for <see cref="McpHttpFailure.UnexpectedStatus"/>.</param>
    public McpHttpException(McpHttpFailure failure, string message, int? statusCode = null)
        : base(message)
    {
        Failure = failure;
        StatusCode = statusCode;
    }

    /// <summary>Gets why the exchange failed.</summary>
    public McpHttpFailure Failure { get; }

    /// <summary>Gets the status code of the response that failed the exchange, or <see langword="null"/> when no status was at fault.</summary>
    public int? StatusCode { get; }
}
