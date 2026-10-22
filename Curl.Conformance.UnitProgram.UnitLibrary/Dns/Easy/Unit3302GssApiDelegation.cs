using Curl.Api.Codes;
using Curl.Api.Options;
using Curl.Api.Options.Authentication;
using Curl.Conformance.UnitProgram.Hashes;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.UnitProgram.Dns.Easy;

/// <summary>
/// The port of curl 8.22.0's <c>tests/unit/unit3302.c</c>: <c>CURLOPT_GSSAPI_DELEGATION</c> set to
/// <c>CURLGSSAPI_DELEGATION_FLAG</c>, <c>_POLICY_FLAG</c>, both, <c>_NONE</c> and <c>0xFF</c>, each returning
/// <c>CURLE_OK</c> and storing the value, <c>0xFF</c> masked to both flags. Curl has no <c>data-&gt;set</c>: the
/// stored value is read back from the handle's <see cref="EasyOptions" />, as
/// <see cref="EasyAuthenticationOptions.GssApiDelegation" />.
/// </summary>
/// <param name="setDelegation">Sets the option on a handle and gives the code and the value read back; Libcurl's by default.</param>
public sealed class Unit3302GssApiDelegation(Func<CurlEasy, long, (CurlCode Code, GssApiDelegation Stored)> setDelegation) : IUnitProgram
{
    private static readonly (long Value, GssApiDelegation Stored, string Name, string StoredMessage)[] Checks =
    [
        (2L, GssApiDelegation.Flag, "DELEGATION_FLAG", "DELEGATION_FLAG not stored in data->set"),
        (1L, GssApiDelegation.PolicyFlag, "DELEGATION_POLICY_FLAG", "DELEGATION_POLICY_FLAG not stored in data->set"),
        (3L, GssApiDelegation.Flag | GssApiDelegation.PolicyFlag, "both flags", "both delegation flags not stored in data->set"),
        (0L, GssApiDelegation.None, "DELEGATION_NONE", "gssapi_delegation not cleared by DELEGATION_NONE"),
        (0xFFL, GssApiDelegation.Flag | GssApiDelegation.PolicyFlag, "0xFF", "unknown bits not masked off"),
    ];

    /// <summary>Creates the port over <see cref="CurlEasy.SetOption" /> and <see cref="CurlEasy.Options" />.</summary>
    public Unit3302GssApiDelegation()
        : this(SetThroughLibcurl)
    {
    }

    /// <inheritdoc />
    public string ProgramName => "unit3302";

    /// <inheritdoc />
    public async Task<UnitProgramResult> RunAsync(UnitProgramContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        UnitChecks checks = new(context.StandardError, ProgramName);
        await using CurlLibrary library = CurlGlobal.Init(CurlGlobalFlags.All);
        await using CurlEasy easy = library.EasyInit();
        foreach ((long value, GssApiDelegation stored, string name, string storedMessage) in Checks)
        {
            (CurlCode code, GssApiDelegation actual) = setDelegation(easy, value);
            checks.FailUnless(code == CurlCode.Ok, $"setopt {name} returned error");
            checks.FailUnless(actual == stored, storedMessage);
        }

        return checks.Result;
    }

    private static (CurlCode Code, GssApiDelegation Stored) SetThroughLibcurl(CurlEasy easy, long value) =>
        (easy.SetOption(CurlOption.GssapiDelegation, value), easy.Options.Authentication.GssApiDelegation);
}
