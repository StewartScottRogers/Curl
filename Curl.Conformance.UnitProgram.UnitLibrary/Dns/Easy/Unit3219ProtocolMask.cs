using Curl.Api.Info;
using Curl.Api.Options;
using Curl.Conformance.UnitProgram.Hashes;
using Libcurl.Info;

namespace Curl.Conformance.UnitProgram.Dns.Easy;

/// <summary>
/// The port of curl 8.22.0's <c>tests/unit/unit3219.c</c>: <c>CURLPROTO_MASK</c> keeps <c>CURLPROTO_HTTP</c> and
/// <c>CURLPROTO_GOPHERS</c>, the highest public bit, and drops <c>CURLPROTO_WS</c>, <c>CURLPROTO_WSS</c> and
/// <c>CURLPROTO_MQTTS</c>, in that order. Curl has no <c>CURLPROTO_MASK</c> macro: <c>url.c</c> applies the mask to
/// the connection's protocol for <c>CURLINFO_PROTOCOL</c>, so the port reads that info, through
/// <c>EasyInfoDispatch</c>, for a transfer reporting each bit, and checks the bit comes back, or 0.
/// </summary>
/// <param name="readProtocol">Gives <c>CURLINFO_PROTOCOL</c> for a transfer whose protocol bit is the argument; Libcurl's by default.</param>
public sealed class Unit3219ProtocolMask(Func<long, long> readProtocol) : IUnitProgram
{
    private static readonly (long Bit, bool Kept, string Message)[] Checks =
    [
        (1L << 0, true, "mask should include HTTP"),
        (1L << 29, true, "mask should include the highest public protocol bit"),
        (1L << 30, false, "mask should exclude websocket protocol bits"),
        (1L << 31, false, "mask should exclude secure websocket protocol bits"),
        (1L << 32, false, "mask should exclude internal-only protocols"),
    ];

    /// <summary>Creates the port over Libcurl's <c>CURLINFO_PROTOCOL</c> reader.</summary>
    public Unit3219ProtocolMask()
        : this(ReadThroughLibcurl)
    {
    }

    /// <inheritdoc />
    public string ProgramName => "unit3219";

    /// <inheritdoc />
    public Task<UnitProgramResult> RunAsync(UnitProgramContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        UnitChecks checks = new(context.StandardError, ProgramName);
        foreach ((long bit, bool kept, string message) in Checks)
        {
            checks.FailUnless(readProtocol(bit) == (kept ? bit : 0), message);
        }

        return Task.FromResult(checks.Result);
    }

    private static long ReadThroughLibcurl(long bit)
    {
        EasyInfoDispatch.Default.GetInfo(CurlInfo.Protocol, new TransferInfo { Protocol = bit }, new EasyOptions(), out object? value);
        return (long)value!;
    }
}
