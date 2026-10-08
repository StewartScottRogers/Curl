namespace Curl.Protocol.Abstractions;

// Self-test fixture for Measure-ExitCodeGap.ps1: no member for 6 (a gap).
public enum CurlExitCode
{
    Ok = 0,

    UnsupportedProtocol = 1,

    FailedInit = 2,

    UrlMalformat = 3,

    NotBuiltIn = 4,

    CouldntResolveProxy = 5,
}
