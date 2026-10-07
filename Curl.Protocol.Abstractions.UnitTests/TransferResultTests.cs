using Curl.Testing;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins how <see cref="TransferResult" />'s factories fill in
/// <see cref="TransferResult.SourceLastWriteTimeUtc" />, the value <c>-R</c>/<c>--remote-time</c>
/// is applied from.
/// </summary>
[TestClass]
public sealed class TransferResultTests
{
    private static readonly DateTimeOffset SourceTime =
        new(2026, 6, 24, 12, 34, 56, TimeSpan.Zero);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Success_WithoutATimestamp_LeavesSourceLastWriteTimeUtcNull()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("factory", "Success(10)");

        var result = TransferResult.Success(10);

        diagnostics.Act("result", result);
        diagnostics.Assert("SourceLastWriteTimeUtc", null, result.SourceLastWriteTimeUtc);
        Assert.IsNull(result.SourceLastWriteTimeUtc);
        Assert.AreEqual(10L, result.BytesTransferred);
        Assert.IsTrue(result.IsSuccess);
    }

    [TestMethod]
    public void Success_WithATimestamp_CarriesIt()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("factory", $"Success(10, {SourceTime:O})");

        var result = TransferResult.Success(10, SourceTime);

        diagnostics.Act("result", result);
        diagnostics.Assert("SourceLastWriteTimeUtc", SourceTime, result.SourceLastWriteTimeUtc);
        Assert.AreEqual(SourceTime, result.SourceLastWriteTimeUtc);
        Assert.AreEqual(10L, result.BytesTransferred);
        Assert.IsNull(result.ErrorMessage);
    }

    [TestMethod]
    public void Failure_Always_LeavesSourceLastWriteTimeUtcNull()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("factory", "Failure(ReadError, \"failed\")");

        var result = TransferResult.Failure(CurlExitCode.ReadError, "failed");

        diagnostics.Act("result", result);
        diagnostics.Assert("SourceLastWriteTimeUtc", null, result.SourceLastWriteTimeUtc);
        Assert.IsNull(result.SourceLastWriteTimeUtc);
        Assert.IsFalse(result.IsSuccess);
    }

    [TestMethod]
    public void Failure_WithABytesTransferredCount_ReportsIt()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("factory", "Failure(WriteError, \"x\", 5)");

        var result = TransferResult.Failure(CurlExitCode.WriteError, "x", 5);

        diagnostics.Act("BytesTransferred", result.BytesTransferred);
        diagnostics.Assert("BytesTransferred", 5L, result.BytesTransferred);
        Assert.AreEqual(5L, result.BytesTransferred);
    }

    [TestMethod]
    public void Failure_WithoutABytesTransferredCount_ReportsZero()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("factory", "Failure(WriteError, \"x\")");

        var result = TransferResult.Failure(CurlExitCode.WriteError, "x");

        diagnostics.Act("BytesTransferred", result.BytesTransferred);
        diagnostics.Assert("BytesTransferred", 0L, result.BytesTransferred);
        Assert.AreEqual(0L, result.BytesTransferred);
    }

    [TestMethod]
    public void Constructor_WithThreePositionalValues_LeavesSourceLastWriteTimeUtcNull()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("constructor", "TransferResult(Ok, 5, null)");

        var result = new TransferResult(CurlExitCode.Ok, 5, null);

        diagnostics.Act("result", result);
        diagnostics.Assert("SourceLastWriteTimeUtc", null, result.SourceLastWriteTimeUtc);
        Assert.IsNull(result.SourceLastWriteTimeUtc);
    }

    [TestMethod]
    public void With_SettingEveryProperty_ReturnsCopyWithNewValuesAndLeavesOriginalUnchanged()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        var original = TransferResult.Success(10, SourceTime);
        var laterTime = SourceTime.AddHours(1);
        diagnostics.Arrange("original", original);

        var copy = original with
        {
            ExitCode = CurlExitCode.WriteError,
            BytesTransferred = 3,
            ErrorMessage = "failed",
            SourceLastWriteTimeUtc = laterTime,
        };

        diagnostics.Act("copy", copy);
        diagnostics.Act("original", original);
        diagnostics.Assert("copy ExitCode", CurlExitCode.WriteError, copy.ExitCode);
        Assert.AreEqual(CurlExitCode.WriteError, copy.ExitCode);
        Assert.AreEqual(3L, copy.BytesTransferred);
        Assert.AreEqual("failed", copy.ErrorMessage);
        Assert.AreEqual(laterTime, copy.SourceLastWriteTimeUtc);
        Assert.AreEqual(CurlExitCode.Ok, original.ExitCode);
        Assert.AreEqual(10L, original.BytesTransferred);
        Assert.IsNull(original.ErrorMessage);
        Assert.AreEqual(SourceTime, original.SourceLastWriteTimeUtc);
    }

    [TestMethod]
    public void TimeConditionNotMet_WithATimestamp_IsASuccessThatCarriesItAndMovedNothing()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("factory", $"TimeConditionNotMet({SourceTime:O})");

        var result = TransferResult.TimeConditionNotMet(SourceTime);

        diagnostics.Act("result", result);
        diagnostics.Assert("TimeConditionUnmet", true, result.TimeConditionUnmet);
        Assert.IsTrue(result.IsSuccess);
        Assert.IsTrue(result.TimeConditionUnmet);
        Assert.AreEqual(SourceTime, result.SourceLastWriteTimeUtc);
        Assert.AreEqual(0L, result.BytesTransferred);
        Assert.IsNull(result.ErrorMessage);
    }

    [TestMethod]
    public void TimeConditionNotMet_WithoutATimestamp_LeavesSourceLastWriteTimeUtcNull()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("factory", "TimeConditionNotMet()");

        var result = TransferResult.TimeConditionNotMet();

        diagnostics.Act("result", result);
        diagnostics.Assert("TimeConditionUnmet", true, result.TimeConditionUnmet);
        Assert.IsTrue(result.TimeConditionUnmet);
        Assert.IsNull(result.SourceLastWriteTimeUtc);
    }

    [TestMethod]
    public void TimeConditionUnmet_OnEveryOtherFactoryAndTheConstructor_IsFalse()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("sources", "Success, Failure, constructor");

        bool fromSuccess = TransferResult.Success(0).TimeConditionUnmet;
        bool fromFailure = TransferResult.Failure(CurlExitCode.ReadError, "failed").TimeConditionUnmet;
        bool fromConstructor = new TransferResult(CurlExitCode.Ok, 0).TimeConditionUnmet;

        diagnostics.Act("Success", fromSuccess);
        diagnostics.Act("Failure", fromFailure);
        diagnostics.Act("constructor", fromConstructor);
        diagnostics.Assert("Success", false, fromSuccess);
        Assert.IsFalse(TransferResult.Success(0).TimeConditionUnmet);
        Assert.IsFalse(TransferResult.Failure(CurlExitCode.ReadError, "failed").TimeConditionUnmet);
        Assert.IsFalse(new TransferResult(CurlExitCode.Ok, 0).TimeConditionUnmet);
    }

    [TestMethod]
    public void With_SettingSourceLastWriteTimeUtc_KeepsTimeConditionUnmet()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("with", $"SourceLastWriteTimeUtc = {SourceTime:O}");

        var result = TransferResult.TimeConditionNotMet() with { SourceLastWriteTimeUtc = SourceTime };

        diagnostics.Act("result", result);
        diagnostics.Assert("TimeConditionUnmet", true, result.TimeConditionUnmet);
        Assert.IsTrue(result.TimeConditionUnmet);
        Assert.AreEqual(SourceTime, result.SourceLastWriteTimeUtc);
    }

    [TestMethod]
    public void Report_OnEveryFactoryAndTheConstructor_IsNull()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("sources", "Success, TimeConditionNotMet, Failure, constructor");

        TransferReport? fromSuccess = TransferResult.Success(0).Report;
        TransferReport? fromTimeCondition = TransferResult.TimeConditionNotMet().Report;
        TransferReport? fromFailure = TransferResult.Failure(CurlExitCode.ReadError, "failed").Report;
        TransferReport? fromConstructor = new TransferResult(CurlExitCode.Ok, 0).Report;

        diagnostics.Act("Success", fromSuccess);
        diagnostics.Act("TimeConditionNotMet", fromTimeCondition);
        diagnostics.Act("Failure", fromFailure);
        diagnostics.Act("constructor", fromConstructor);
        diagnostics.Assert("Success report", null, fromSuccess);
        Assert.IsNull(TransferResult.Success(0).Report);
        Assert.IsNull(TransferResult.TimeConditionNotMet().Report);
        Assert.IsNull(TransferResult.Failure(CurlExitCode.ReadError, "failed").Report);
        Assert.IsNull(new TransferResult(CurlExitCode.Ok, 0).Report);
    }

    [TestMethod]
    public void With_SettingReport_CarriesItAndKeepsThePositionalValues()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        var report = new TransferReport { ResponseCode = 404 };
        diagnostics.Arrange("report ResponseCode", report.ResponseCode);

        var result = TransferResult.Failure(CurlExitCode.HttpReturnedError, "failed", 7) with { Report = report };

        diagnostics.Act("result", result);
        diagnostics.Assert("BytesTransferred", 7L, result.BytesTransferred);
        Assert.AreSame(report, result.Report);
        Assert.AreEqual(7L, result.BytesTransferred);
        Assert.AreEqual("failed", result.ErrorMessage);
    }

    [TestMethod]
    public void IsConnectionRefused_OnEveryFactory_IsFalseUntilSetWithWith()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("sources", "Success, Failure, Failure with IsConnectionRefused");

        bool fromSuccess = TransferResult.Success(0).IsConnectionRefused;
        bool fromFailure = TransferResult.Failure(CurlExitCode.CouldntConnect, "failed").IsConnectionRefused;
        bool fromWith = (TransferResult.Failure(CurlExitCode.CouldntConnect, "failed") with { IsConnectionRefused = true }).IsConnectionRefused;

        diagnostics.Act("Success", fromSuccess);
        diagnostics.Act("Failure", fromFailure);
        diagnostics.Act("Failure with", fromWith);
        diagnostics.Assert("Failure with", true, fromWith);
        Assert.IsFalse(TransferResult.Success(0).IsConnectionRefused);
        Assert.IsFalse(TransferResult.Failure(CurlExitCode.CouldntConnect, "failed").IsConnectionRefused);
        Assert.IsTrue((TransferResult.Failure(CurlExitCode.CouldntConnect, "failed") with { IsConnectionRefused = true }).IsConnectionRefused);
    }

    [TestMethod]
    public void SourceLastWriteUnixSeconds_PastYear9999_ReadsBackNullFromSourceLastWriteTimeUtc()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("SourceLastWriteUnixSeconds", 1200110860800);
        // 40000-01-01T00:00:00Z, the Last-Modified curl 8.21.0 stamps a file from (BL-1409).
        var result = TransferResult.Success(10) with { SourceLastWriteUnixSeconds = 1200110860800 };

        diagnostics.Act("SourceLastWriteUnixSeconds", result.SourceLastWriteUnixSeconds);
        diagnostics.Act("SourceLastWriteTimeUtc", result.SourceLastWriteTimeUtc);
        diagnostics.Assert("SourceLastWriteUnixSeconds", 1200110860800L, result.SourceLastWriteUnixSeconds);
        Assert.AreEqual(1200110860800L, result.SourceLastWriteUnixSeconds);
        Assert.IsNull(result.SourceLastWriteTimeUtc);
    }

    [TestMethod]
    public void SourceLastWriteTimeUtc_WhenSet_StoresItsWholeUnixSeconds()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("time", $"{SourceTime.AddMilliseconds(750):O}");

        var result = TransferResult.Success(10, SourceTime.AddMilliseconds(750));

        diagnostics.Act("SourceLastWriteUnixSeconds", result.SourceLastWriteUnixSeconds);
        diagnostics.Assert("SourceLastWriteUnixSeconds", SourceTime.ToUnixTimeSeconds(), result.SourceLastWriteUnixSeconds);
        Assert.AreEqual(SourceTime.ToUnixTimeSeconds(), result.SourceLastWriteUnixSeconds);
        Assert.AreEqual(SourceTime, result.SourceLastWriteTimeUtc);
    }

    [TestMethod]
    public void With_SourceLastWriteTimeUtc_ReplacesTheStoredSecondsAndLeavesOriginalUnchanged()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        var original = TransferResult.Success(10, SourceTime);
        var later = SourceTime.AddDays(1);
        diagnostics.Arrange("later", $"{later:O}");

        var copy = original with { SourceLastWriteTimeUtc = later };
        var cleared = original with { SourceLastWriteTimeUtc = null };

        diagnostics.Act("copy", copy);
        diagnostics.Act("cleared", cleared);
        diagnostics.Assert("copy SourceLastWriteTimeUtc", later, copy.SourceLastWriteTimeUtc);
        Assert.AreEqual(later, copy.SourceLastWriteTimeUtc);
        Assert.AreEqual(later.ToUnixTimeSeconds(), copy.SourceLastWriteUnixSeconds);
        Assert.IsNull(cleared.SourceLastWriteUnixSeconds);
        Assert.AreEqual(SourceTime, original.SourceLastWriteTimeUtc);
    }

    [TestMethod]
    public void Equals_ForInRangeTimes_ComparesTheTimes()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("time", $"{SourceTime:O}");

        bool sameTimes = TransferResult.Success(10, SourceTime).Equals(TransferResult.Success(10, SourceTime));
        bool differentTimes = TransferResult.Success(10, SourceTime).Equals(TransferResult.Success(10, SourceTime.AddSeconds(1)));

        diagnostics.Act("same times equal", sameTimes);
        diagnostics.Act("different times equal", differentTimes);
        diagnostics.Assert("same times equal", true, sameTimes);
        Assert.AreEqual(TransferResult.Success(10, SourceTime), TransferResult.Success(10, SourceTime));
        Assert.AreEqual(
            TransferResult.Success(10, SourceTime).GetHashCode(),
            TransferResult.Success(10, SourceTime).GetHashCode());
        Assert.AreNotEqual(TransferResult.Success(10, SourceTime), TransferResult.Success(10, SourceTime.AddSeconds(1)));
        Assert.AreEqual(
            TransferResult.Success(10, SourceTime),
            TransferResult.Success(10) with { SourceLastWriteUnixSeconds = SourceTime.ToUnixTimeSeconds() });
    }

    [TestMethod]
    public void Deconstruct_GivesTheDateTimeOffsetView()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("factory", $"Success(10, {SourceTime:O})");

        var (exitCode, bytes, message, time) = TransferResult.Success(10, SourceTime);

        diagnostics.Act("exitCode", exitCode);
        diagnostics.Act("bytes", bytes);
        diagnostics.Act("message", message);
        diagnostics.Act("time", time);
        diagnostics.Assert("time", SourceTime, time);
        Assert.AreEqual(CurlExitCode.Ok, exitCode);
        Assert.AreEqual(10L, bytes);
        Assert.IsNull(message);
        Assert.AreEqual(SourceTime, time);
    }
}
