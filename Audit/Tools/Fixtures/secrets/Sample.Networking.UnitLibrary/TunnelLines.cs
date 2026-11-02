namespace Sample;

// Fixture for Find-SecretsInDiagnostics.ps1 -SelfTest: a second production project, with one
// writer that passes no secret. Nothing here is real.
public static class TunnelLines
{
    public static void Write(Events events, string host) => events.ReportInfo($"Establishing HTTP proxy tunnel to {host}");
}
