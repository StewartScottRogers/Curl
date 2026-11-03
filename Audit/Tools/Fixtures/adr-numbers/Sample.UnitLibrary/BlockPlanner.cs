namespace Sample;

// Fixture for Find-AdrNumberMismatches.ps1 -SelfTest: the code ADR-0005 and ADR-0006 name, which
// produces exit 7 and exit 8. Nothing here is real.
public static class BlockPlanner
{
    public static CurlExitCode SizeFor(long length) => length < 0 ? CurlExitCode.CouldntConnect : CurlExitCode.WeirdServerReply;
}
