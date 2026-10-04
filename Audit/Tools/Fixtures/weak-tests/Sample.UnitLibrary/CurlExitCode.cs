namespace Sample;

// Fixture for Find-WeakTests.ps1 -SelfTest (BL-1368): the exit codes the scan maps names to.
public enum CurlExitCode
{
    Ok = 0,
    CouldntConnect = 7,
    WriteError = 23,
    ReadError = 26,
}
