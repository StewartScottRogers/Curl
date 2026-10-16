using System.Net;

using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// The pieces of one transfer the curl tool's runner has already chosen before connecting, which a
/// <see cref="ManagedEasyTransfer" /> given them through <see cref="ManagedEasyTransfer.ToolInputs" /> puts on the
/// transfer's context as they are, in place of choosing its own from the options (BL-2395, ADR-0554). The runner
/// has also done the transfer's cookie, alt-svc and HSTS file setup and does its cleanup, so a handle given these
/// reads and writes none of those files and takes the runner's <see cref="AltSvc" /> cache.
/// </summary>
/// <param name="Proxy">The proxy <c>TransferPerformer.TrySelectProxyAndCredentials</c> chose, or <see langword="null" /> to connect directly.</param>
/// <param name="LookedUpCredentials">The credentials it looked up, carried in place of <c>-u</c>'s, or <see langword="null" /> to carry <c>-u</c>'s.</param>
/// <param name="Ssh">The SSH options <c>TransferPerformer.ResolveSshOptionsAsync</c> resolved, or <see langword="null" /> for any scheme but <c>scp</c> and <c>sftp</c>.</param>
/// <param name="IfNoneMatchHeaders">The <c>If-None-Match</c> lines <c>--etag-compare</c> added, or <see langword="null" /> for none.</param>
/// <param name="Range">The parsed <c>-r</c> range, or <see langword="null" /> for the whole resource.</param>
/// <param name="ResumeFrom">The <c>-C</c> offset, already resolved for <c>-C -</c>, or <see langword="null" /> for none.</param>
/// <param name="FormBody">The <c>-F</c> body built for this transfer, or <see langword="null" /> without <c>-F</c>.</param>
/// <param name="Upload">The <c>-T</c> source, or <see langword="null" /> without <c>-T</c>.</param>
/// <param name="AltSvc">The transfer's <c>--alt-svc</c> cache the runner opened, or <see langword="null" /> for none.</param>
/// <param name="Url">
/// The transfer URL the runner parsed and checked, transferred in place of a parse of
/// <c>EasyNetworkOptions.Url</c>, which then only names the transfer (BL-2394); <see langword="null" /> to parse that.
/// </param>
internal sealed record EngineToolTransferInputs(
    ProxyEndpoint? Proxy = null,
    NetworkCredential? LookedUpCredentials = null,
    SshOptions? Ssh = null,
    IReadOnlyList<string>? IfNoneMatchHeaders = null,
    ByteRange? Range = null,
    long? ResumeFrom = null,
    HttpRequestBody? FormBody = null,
    Stream? Upload = null,
    AltSvcTransferCache? AltSvc = null,
    CurlUrl? Url = null);
