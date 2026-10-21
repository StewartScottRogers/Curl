using System.Text;
using Curl.Api.Info;
using Curl.Console;

namespace Curl.Conformance;

public sealed partial class UpstreamConformanceTests
{
    // The curl tool's -w cases that set curl's debug-build test variables (970, 972, 1981) rely on Debug: CURL_TIME fixes
    // %time{}'s clock and the getinfo times, speeds and local port, CURL_DEBUG_SIZE the header and request sizes, and
    // CURL_VERSION what %{json} prints as curl_version. The harness reports Debug to a case setting CURL_VERSION or
    // CURL_DEBUG_SIZE, or CURL_TIME without naming HSTS (whose cases have their own rule), and RunCurlAsync has Curl
    // honour all three (DebugWriteOutStandIn, ADR-0732, BL-2909).
    private static bool SetsWriteOutDebugVariable(string text) =>
        (text.Contains("\nCURL_TIME=", StringComparison.Ordinal) && !text.Contains("\nHSTS\n", StringComparison.Ordinal))
        || text.Contains("\nCURL_VERSION=", StringComparison.Ordinal)
        || text.Contains("\nCURL_DEBUG_SIZE=", StringComparison.Ordinal);

    [TestMethod]
    [DataRow("<features>\nDebug\n</features>\n<setenv>\nCURL_TIME=1754037103\n</setenv>\n", true)]
    [DataRow("<features>\nDebug\n</features>\n<setenv>\nCURL_VERSION=fake\n</setenv>\n", true)]
    [DataRow("<features>\nDebug\n</features>\n<setenv>\nCURL_DEBUG_SIZE=4019\n</setenv>\n", true)]
    [DataRow("<features>\nDebug\n</features>\n<setenv>\nCURL_OTHER=13\n</setenv>\n", false)]
    public void PlatformFor_CaseSettingAWriteOutDebugVariable_ReportsDebugOnlyThen(string client, bool reportsDebug)
    {
        byte[] testFile = Encoding.Latin1.GetBytes($"<testcase>\n<client>\n<server>\nhttp\n</server>\n{client}</client>\n</testcase>\n");

        UpstreamCurlPlatform platform = PlatformFor(testFile);

        Assert.AreEqual(reportsDebug, platform.Features.Contains("Debug"));
    }

    // test1981: CURL_TIME=1754037103 is 2025-08-01 08:31:43 UTC, and its microseconds are 1754037103 % 1000000.
    [TestMethod]
    public void ClockOr_CurlTimeSet_IsFixedAtThoseSecondsWithTheRemainderAsMicroseconds()
    {
        DebugWriteOutStandIn standIn = DebugWriteOutStandIn.Of(name => name == "CURL_TIME" ? "1754037103" : null)!;

        TimeProvider clock = standIn.ClockOr(TimeProvider.System);

        Assert.AreEqual(new DateTimeOffset(2025, 8, 1, 8, 31, 43, TimeSpan.Zero).AddTicks(37103 * 10), clock.GetUtcNow());
    }

    [TestMethod]
    public void ClockOr_NoCurlTime_IsTheRunClock()
    {
        DebugWriteOutStandIn standIn = DebugWriteOutStandIn.Of(name => name == "CURL_VERSION" ? "fake" : null)!;

        TimeProvider clock = standIn.ClockOr(TimeProvider.System);

        Assert.AreSame(TimeProvider.System, clock);
    }

    [TestMethod]
    [DataRow("253402300800")]
    [DataRow("-13")]
    [DataRow("thirteen")]
    public void Of_CurlTimeNotAHonouredNumber_IsNull(string value)
    {
        DebugWriteOutStandIn? standIn = DebugWriteOutStandIn.Of(name => name == "CURL_TIME" ? value : null);

        Assert.IsNull(standIn);
    }

    [TestMethod]
    public void LibraryVersionOr_CurlVersionSet_IsThatText()
    {
        DebugWriteOutStandIn standIn = DebugWriteOutStandIn.Of(name => name == "CURL_VERSION" ? "curl-unit-test-fake-version" : null)!;

        Assert.AreEqual("curl-unit-test-fake-version", standIn.LibraryVersionOr("libcurl/8.22.0"));
    }

    [TestMethod]
    public void LibraryVersionOr_NoCurlVersion_IsTheMeasuredVersion()
    {
        Assert.AreEqual("libcurl/8.22.0", DebugWriteOutStandIn.None.LibraryVersionOr("libcurl/8.22.0"));
    }

    // test970 and test972: CURL_TIME=13 and CURL_DEBUG_SIZE=4019 give %{json} local_port 13, every time 0.000013,
    // speeds of 13 and sizes of 4019.
    [TestMethod]
    public void Apply_CurlTimeAndDebugSizeSet_FixesThePortTimesSpeedsAndSizes()
    {
        DebugWriteOutStandIn standIn = DebugWriteOutStandIn.Of(name => name switch
        {
            "CURL_TIME" => "13",
            "CURL_DEBUG_SIZE" => "4019",
            _ => null,
        })!;
        TimeSpan thirteenMicroseconds = TimeSpan.FromTicks(130);

        TransferInfo info = standIn.Apply(new TransferInfo { LocalPort = 50000, HeaderSize = 1, RequestSize = 2, TotalTime = TimeSpan.FromSeconds(1), SpeedDownload = 445 });

        Assert.AreEqual(13, info.LocalPort);
        Assert.AreEqual(4019, info.HeaderSize);
        Assert.AreEqual(4019, info.RequestSize);
        Assert.AreEqual(13, info.SpeedDownload);
        Assert.AreEqual(13, info.SpeedUpload);
        TimeSpan[] times = [info.TotalTime, info.NameLookupTime, info.ConnectTime, info.AppConnectTime, info.PreTransferTime, info.PostTransferTime, info.StartTransferTime, info.RedirectTime, info.QueueTime];
        Assert.IsTrue(times.All(time => time == thirteenMicroseconds));
    }

    [TestMethod]
    public void OfHonoured_RunNotHonouringTheVariables_IsNull()
    {
        Assert.IsNull(DebugWriteOutStandIn.OfHonoured(false, _ => "13"));
    }

    [TestMethod]
    public void OfHonoured_RunHonouringTheVariables_ReadsThem()
    {
        Assert.AreEqual(new DebugWriteOutStandIn(13, 13, "13"), DebugWriteOutStandIn.OfHonoured(true, _ => "13"));
    }

    [TestMethod]
    public void OrNone_NoStandIn_IsNone()
    {
        Assert.AreSame(DebugWriteOutStandIn.None, DebugWriteOutStandIn.OrNone(null));
    }

    [TestMethod]
    public void Apply_NothingSet_LeavesTheInfoAlone()
    {
        TransferInfo measured = new() { LocalPort = 50000, HeaderSize = 1 };

        TransferInfo info = DebugWriteOutStandIn.None.Apply(measured);

        Assert.AreEqual(measured, info);
    }
}
