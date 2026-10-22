using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

// Upstream's smb server, smbserver.py, on %SMBPORT; the platform reports the smb protocol, as Curl's curl -V does (BL-2860).
internal sealed class UpstreamSmbFront : UpstreamStandInFront
{
    public override UpstreamStandInStage Stage => UpstreamStandInStage.Gopher;

    public override UpstreamCurlPlatform WithFeatures(UpstreamCurlPlatform platform) => platform.WithFeatures(SmbServerConnector.EmulatedServers);

    public override void AddVariables(Dictionary<string, string> variables, string? certificateDirectory) =>
        variables["SMBPORT"] = UpstreamCaseRunner.SmbPort;

    public override IConnector Wrap(IConnector inner, UpstreamStandInContext context) => new SmbServerConnector(context.TestCase, inner);
}
