namespace Sample;

// Fixture for Find-SecretsInDiagnostics.ps1 -SelfTest. Nothing here is real: four writes that
// pass a secret (one after =>), which the scan must find, and five calls and a declaration it
// must leave alone.
public static class AuthLines
{
    public static void Write(Events events, Credential credential, System.IO.TextWriter trace, Options options, string bearerToken, System.Threading.CancellationToken cancellationToken, System.IO.Stream stream)
    {
        events.ReportInfo($"Server auth using AWS_SIGV4 with user '{credential.UserName}' {credential.Password}");
        events.ReportInfo($"Proxy auth using Basic with user '{credential.UserName}'");
        events.ReportInfo("Password prompt skipped: -u gave one");
        events.ReportWarning($"cancelled {cancellationToken.IsCancellationRequested}");
        // events.ReportInfo($"old {credential.Password}");
        trace.Write($"passphrase {options.KeyPassphrase}");
        stream.Write(System.Text.Encoding.ASCII.GetBytes($"Authorization: Basic {credential.Password}"));
    }

    public static void Bearer(Events events, string bearerToken) =>
        events.ReportInfo($"Server auth using Bearer {bearerToken}");

    public static void Tunnel(Events events, Credential credential) => events.ReportInfo($"Proxy auth using Basic with user '{credential.UserName}:{credential.Password}'");

    private static System.Threading.Tasks.Task<bool> LogPasswordAsync(string password) => System.Threading.Tasks.Task.FromResult(password.Length > 0);
}
