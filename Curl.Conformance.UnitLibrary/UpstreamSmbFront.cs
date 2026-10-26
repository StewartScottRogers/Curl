using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

// Upstream's smb server, smbserver.py, on %SMBPORT (BL-2860). The platform does not report the smb protocol, as curl 8.22.0's
// reference builds and Curl have none, so upstream's smb cases are skipped (ADR-0829).
internal sealed class UpstreamSmbFront : UpstreamStandInFront
{
    public override UpstreamStandInStage Stage => UpstreamStandInStage.Gopher;

    public override void AddVariables(Dictionary<string, string> variables, string? certificateDirectory) =>
        variables["SMBPORT"] = UpstreamCaseRunner.SmbPort;

    public override IConnector Wrap(IConnector inner, UpstreamStandInContext context) => new SmbServerConnector(context.TestCase, inner);
}
