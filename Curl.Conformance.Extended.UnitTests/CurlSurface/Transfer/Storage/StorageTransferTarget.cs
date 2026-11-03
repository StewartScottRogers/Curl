using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.Storage;

/// <summary>One storage scheme as the transfer checks see it: the reference server holding one object, the URL of that object and of one that is missing.</summary>
/// <param name="Host">The host curl connects to over TLS on port 443.</param>
/// <param name="ObjectUrl">The URL of the object <paramref name="CreateServer" /> holds.</param>
/// <param name="MissingUrl">A URL the server answers with an HTTP error status.</param>
/// <param name="RemoteName">The file name <c>-O</c> takes from <paramref name="ObjectUrl" />.</param>
/// <param name="Body">The object's bytes.</param>
/// <param name="Credentials">The options that authorize the transfer, if the scheme needs any.</param>
/// <param name="CreateServer">Makes a reference server that holds the object.</param>
internal sealed record StorageTransferTarget(
    string Host,
    string ObjectUrl,
    string MissingUrl,
    string RemoteName,
    byte[] Body,
    IReadOnlyList<string> Credentials,
    Func<IReferenceServer> CreateServer)
{
    /// <summary>The port every storage scheme reaches its service on: HTTPS.</summary>
    public const int Port = 443;
}
