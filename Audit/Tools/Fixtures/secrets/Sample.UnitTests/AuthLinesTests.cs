namespace Sample.UnitTests;

// Fixture for Find-SecretsInDiagnostics.ps1 -SelfTest: a test project, which the scan never
// reads although it writes a password. Nothing here is real.
public static class AuthLinesTests
{
    public static void Write(Events events, Credential credential) => events.ReportInfo($"{credential.Password}");
}
