using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

// The stand-in for upstream's test sshd (BL-1916), in front of the case's other stand-ins.
internal sealed class UpstreamSshFront(UpstreamSshServer sshServer) : UpstreamStandInFront
{
    public override UpstreamStandInStage Stage => UpstreamStandInStage.Outermost;

    public override void AddVariables(Dictionary<string, string> variables, string? certificateDirectory)
    {
        variables["SSHPORT"] = UpstreamSshServer.SshPort.ToString(CultureInfo.InvariantCulture);
        variables["USER"] = sshServer.User;
        variables["SFTP_PWD"] = string.Empty;
        variables["SCP_PWD"] = string.Empty;
        variables["SSHSRVMD5"] = sshServer.HostKeyMd5;
        variables["SSHSRVSHA256"] = sshServer.HostKeySha256;
        variables["SSHKEYALGO"] = sshServer.HostKeyAlgorithm;
    }

    // sshserver.pl writes the client's key pair into the server's log folder before the case runs.
    public override void PrepareLogDirectory(string logDirectory)
    {
        Directory.CreateDirectory($"{logDirectory}/server");
        File.WriteAllBytes($"{logDirectory}/server/curl_client_key", sshServer.ClientPrivateKeyFile.ToArray());
        File.WriteAllBytes($"{logDirectory}/server/curl_client_key.pub", sshServer.ClientPublicKeyFile.ToArray());
    }

    // Upstream's sshd runs the real scp and sftp-server, which write an upload where the URL names it; the SCP and SFTP
    // cases name %LOGDIR/upload.%TESTNUMBER, the file runtests.pl compares with <verify><upload>.
    public override byte[] UploadedBytes(string logDirectory, int testNumber)
    {
        string upload = $"{logDirectory}/upload.{testNumber.ToString(CultureInfo.InvariantCulture)}";
        return File.Exists(upload) ? File.ReadAllBytes(upload) : [];
    }

    public override IConnector Wrap(IConnector inner, UpstreamStandInContext context) => sshServer.InFrontOf(inner);
}
