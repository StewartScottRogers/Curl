using Curl.Testing;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins the two ways a <see cref="FileOpenResult" /> may be built, and the invariant
/// that <see cref="FileOpenResult.IsOpen" /> and <see cref="FileOpenResult.Content" />
/// agree.
/// </summary>
[TestClass]
public sealed class FileOpenResultTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Opened_WithNullContent_ThrowsArgumentNullException()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        Stream? content = null;
        diagnostics.Arrange("content", "null");

        var exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => FileOpenResult.Opened(content!, 0, default));

        diagnostics.Act("exception parameter", exception.ParamName);
        diagnostics.Assert("parameter name", "content", exception.ParamName);
        Assert.AreEqual("content", exception.ParamName);
    }

    [TestMethod]
    public void Opened_WithContent_ReportsOkAndRoundTripsEveryValue()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        using var content = new MemoryStream([1, 2, 3, 4, 5]);
        var lastWriteTimeUtc = new DateTimeOffset(2026, 3, 14, 15, 9, 26, TimeSpan.Zero);
        diagnostics.Arrange("length", 5);
        diagnostics.Arrange("last write time", lastWriteTimeUtc.ToString("O"));

        var result = FileOpenResult.Opened(content, 5, lastWriteTimeUtc);

        diagnostics.Act("status", result.Status);
        diagnostics.Act("is open", result.IsOpen);
        diagnostics.Act("length", result.Length);
        diagnostics.Act("last write time", result.LastWriteTimeUtc?.ToString("O"));
        diagnostics.Assert("status", FileAccessStatus.Ok, result.Status);
        Assert.AreEqual(FileAccessStatus.Ok, result.Status);
        Assert.IsTrue(result.IsOpen);
        Assert.AreSame(content, result.Content);
        Assert.AreEqual(5L, result.Length);
        Assert.AreEqual(lastWriteTimeUtc, result.LastWriteTimeUtc);
    }

    [TestMethod]
    public void Opened_WithNullTimestamp_ReportsOkWithAnUnknownTimestamp()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        using var content = new MemoryStream([1, 2, 3]);
        diagnostics.Arrange("length", 3);
        diagnostics.Arrange("last write time", "null");

        var result = FileOpenResult.Opened(content, 3, null);

        diagnostics.Act("status", result.Status);
        diagnostics.Act("is open", result.IsOpen);
        diagnostics.Act("last write time", result.LastWriteTimeUtc?.ToString("O") ?? "null");
        diagnostics.Assert("status", FileAccessStatus.Ok, result.Status);
        Assert.AreEqual(FileAccessStatus.Ok, result.Status);
        Assert.IsTrue(result.IsOpen);
        Assert.AreSame(content, result.Content);
        Assert.AreEqual(3L, result.Length);
        Assert.IsNull(result.LastWriteTimeUtc);
    }

    [TestMethod]
    public void Failed_ReportsANullTimestamp()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("status", FileAccessStatus.NotFound);

        var result = FileOpenResult.Failed(FileAccessStatus.NotFound);

        diagnostics.Act("last write time", result.LastWriteTimeUtc?.ToString("O") ?? "null");
        diagnostics.Assert("last write time", "null", result.LastWriteTimeUtc?.ToString("O") ?? "null");
        Assert.IsNull(result.LastWriteTimeUtc);
    }

    [TestMethod]
    public void Failed_WithoutAnException_CarriesNoFailureException()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("status", FileAccessStatus.NotFound);

        var result = FileOpenResult.Failed(FileAccessStatus.NotFound);

        diagnostics.Act("failure exception", result.FailureException?.GetType().Name ?? "null");
        diagnostics.Assert("failure exception is null", true, result.FailureException is null);
        Assert.IsNull(result.FailureException);
    }

    [TestMethod]
    public void Failed_WithAnException_RoundTripsIt()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        var failure = new FileNotFoundException("Could not find file '/dir/x'.");
        diagnostics.Arrange("failure message", failure.Message);

        var result = FileOpenResult.Failed(FileAccessStatus.NotFound, failure);

        diagnostics.Act("failure exception", result.FailureException?.Message);
        diagnostics.Act("status", result.Status);
        diagnostics.Assert("same exception", true, ReferenceEquals(failure, result.FailureException));
        Assert.AreSame(failure, result.FailureException);
        AssertIsClosedFailure(result, FileAccessStatus.NotFound);
    }

    [TestMethod]
    public void Opened_CarriesNoFailureException()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        using var content = new MemoryStream([1]);
        diagnostics.Arrange("length", 1);

        var result = FileOpenResult.Opened(content, 1, null);

        diagnostics.Act("failure exception", result.FailureException?.GetType().Name ?? "null");
        diagnostics.Assert("failure exception is null", true, result.FailureException is null);
        Assert.IsNull(result.FailureException);
    }

    [TestMethod]
    public void Failed_WithOk_ThrowsArgumentOutOfRangeException()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        var status = FileAccessStatus.Ok;
        diagnostics.Arrange("status", status);

        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => FileOpenResult.Failed(status));

        diagnostics.Act("exception parameter", exception.ParamName);
        diagnostics.Assert("parameter name", "status", exception.ParamName);
        Assert.AreEqual("status", exception.ParamName);
    }

    [TestMethod]
    public void Failed_WithNotFound_ReportsNotOpenWithNoMetadata()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("status", FileAccessStatus.NotFound);

        var result = FileOpenResult.Failed(FileAccessStatus.NotFound);

        diagnostics.Act("result", DescribeResult(result));
        diagnostics.Assert("status", FileAccessStatus.NotFound, result.Status);
        AssertIsClosedFailure(result, FileAccessStatus.NotFound);
    }

    [TestMethod]
    public void Failed_WithIsDirectory_ReportsNotOpenWithNoMetadata()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("status", FileAccessStatus.IsDirectory);

        var result = FileOpenResult.Failed(FileAccessStatus.IsDirectory);

        diagnostics.Act("result", DescribeResult(result));
        diagnostics.Assert("status", FileAccessStatus.IsDirectory, result.Status);
        AssertIsClosedFailure(result, FileAccessStatus.IsDirectory);
    }

    [TestMethod]
    public void Failed_WithAccessDenied_ReportsNotOpenWithNoMetadata()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("status", FileAccessStatus.AccessDenied);

        var result = FileOpenResult.Failed(FileAccessStatus.AccessDenied);

        diagnostics.Act("result", DescribeResult(result));
        diagnostics.Assert("status", FileAccessStatus.AccessDenied, result.Status);
        AssertIsClosedFailure(result, FileAccessStatus.AccessDenied);
    }

    [TestMethod]
    public void Failed_WithIoError_ReportsNotOpenWithNoMetadata()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("status", FileAccessStatus.IoError);

        var result = FileOpenResult.Failed(FileAccessStatus.IoError);

        diagnostics.Act("result", DescribeResult(result));
        diagnostics.Assert("status", FileAccessStatus.IoError, result.Status);
        AssertIsClosedFailure(result, FileAccessStatus.IoError);
    }

    [TestMethod]
    public void Equals_ForTwoResultsBuiltTheSameWay_ReturnsTrue()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        using var content = new MemoryStream([7, 8, 9]);
        var lastWriteTimeUtc = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        diagnostics.Arrange("length", 3);
        diagnostics.Arrange("last write time", lastWriteTimeUtc.ToString("O"));

        var first = FileOpenResult.Opened(content, 3, lastWriteTimeUtc);
        var second = FileOpenResult.Opened(content, 3, lastWriteTimeUtc);

        diagnostics.Act("equal", first.Equals(second));
        diagnostics.Act("hash codes equal", first.GetHashCode() == second.GetHashCode());
        diagnostics.Assert("results equal", true, first.Equals(second));
        Assert.AreEqual(first, second);
        Assert.AreEqual(first.GetHashCode(), second.GetHashCode());
    }

    [TestMethod]
    public void Equals_ForTwoFailuresWithTheSameStatus_ReturnsTrue()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("status", FileAccessStatus.NotFound);

        var first = FileOpenResult.Failed(FileAccessStatus.NotFound);
        var second = FileOpenResult.Failed(FileAccessStatus.NotFound);

        diagnostics.Act("equal", first.Equals(second));
        diagnostics.Act("hash codes equal", first.GetHashCode() == second.GetHashCode());
        diagnostics.Assert("results equal", true, first.Equals(second));
        Assert.AreEqual(first, second);
        Assert.AreEqual(first.GetHashCode(), second.GetHashCode());
    }

    [TestMethod]
    public void Equals_ForFailuresWithDifferentStatuses_ReturnsFalse()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("statuses", "NotFound, IsDirectory");

        var notFound = FileOpenResult.Failed(FileAccessStatus.NotFound);
        var isDirectory = FileOpenResult.Failed(FileAccessStatus.IsDirectory);

        diagnostics.Act("equal", notFound.Equals(isDirectory));
        diagnostics.Assert("results equal", false, notFound.Equals(isDirectory));
        Assert.AreNotEqual(notFound, isDirectory);
    }

    private static string DescribeResult(FileOpenResult result) =>
        $"status {result.Status}, open {result.IsOpen}, length {result.Length}, "
            + $"content {(result.Content is null ? "null" : "set")}, "
            + $"timestamp {result.LastWriteTimeUtc?.ToString("O") ?? "null"}";

    private static void AssertIsClosedFailure(FileOpenResult result, FileAccessStatus expected)
    {
        Assert.AreEqual(expected, result.Status);
        Assert.IsFalse(result.IsOpen);
        Assert.IsNull(result.Content);
        Assert.AreEqual(0L, result.Length);
        Assert.IsNull(result.LastWriteTimeUtc);
    }

    [TestMethod]
    public void With_SettingEveryProperty_ReturnsCopyWithNewValuesAndLeavesOriginalUnchanged()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        using var content = new MemoryStream([1, 2, 3]);
        var lastWriteTimeUtc = new DateTimeOffset(2026, 3, 14, 15, 9, 26, TimeSpan.Zero);
        var original = FileOpenResult.Failed(FileAccessStatus.NotFound);
        diagnostics.Arrange("original", DescribeResult(original));

        var copy = original with
        {
            Status = FileAccessStatus.Ok,
            Content = content,
            Length = 3,
            LastWriteTimeUtc = lastWriteTimeUtc,
        };

        diagnostics.Act("copy", DescribeResult(copy));
        diagnostics.Act("original after", DescribeResult(original));
        diagnostics.Assert("copy status", FileAccessStatus.Ok, copy.Status);
        Assert.AreEqual(FileAccessStatus.Ok, copy.Status);
        Assert.AreSame(content, copy.Content);
        Assert.AreEqual(3L, copy.Length);
        Assert.AreEqual(lastWriteTimeUtc, copy.LastWriteTimeUtc);
        AssertIsClosedFailure(original, FileAccessStatus.NotFound);
    }
}
