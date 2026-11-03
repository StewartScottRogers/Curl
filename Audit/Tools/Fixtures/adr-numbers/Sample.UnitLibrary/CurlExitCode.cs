namespace Sample;

// Fixture for Find-AdrNumberMismatches.ps1 -SelfTest: the exit codes the scan maps names to.
public enum CurlExitCode
{
    Ok = 0,
    CouldntResolveHost = 6,
    CouldntConnect = 7,
    WeirdServerReply = 8,
}
