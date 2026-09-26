namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins the two ways a <see cref="FileOpenResult" /> may be built, and the invariant
/// that <see cref="FileOpenResult.IsOpen" /> and <see cref="FileOpenResult.Content" />
/// agree.
/// </summary>
[TestClass]
public sealed class FileOpenResultTests
{
    [TestMethod]
    public void Opened_WithNullContent_ThrowsArgumentNullException()
    {
        Stream? content = null;

        var exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => FileOpenResult.Opened(content!, 0, default));

        Assert.AreEqual("content", exception.ParamName);
    }

    [TestMethod]
    public void Opened_WithContent_ReportsOkAndRoundTripsEveryValue()
    {
        using var content = new MemoryStream([1, 2, 3, 4, 5]);
        var lastWriteTimeUtc = new DateTimeOffset(2026, 3, 14, 15, 9, 26, TimeSpan.Zero);

        var result = FileOpenResult.Opened(content, 5, lastWriteTimeUtc);

        Assert.AreEqual(FileAccessStatus.Ok, result.Status);
        Assert.IsTrue(result.IsOpen);
        Assert.AreSame(content, result.Content);
        Assert.AreEqual(5L, result.Length);
        Assert.AreEqual(lastWriteTimeUtc, result.LastWriteTimeUtc);
    }

    [TestMethod]
    public void Failed_WithOk_ThrowsArgumentOutOfRangeException()
    {
        var status = FileAccessStatus.Ok;

        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => FileOpenResult.Failed(status));

        Assert.AreEqual("status", exception.ParamName);
    }

    [TestMethod]
    public void Failed_WithNotFound_ReportsNotOpenWithNoMetadata()
    {
        var result = FileOpenResult.Failed(FileAccessStatus.NotFound);

        AssertIsClosedFailure(result, FileAccessStatus.NotFound);
    }

    [TestMethod]
    public void Failed_WithIsDirectory_ReportsNotOpenWithNoMetadata()
    {
        var result = FileOpenResult.Failed(FileAccessStatus.IsDirectory);

        AssertIsClosedFailure(result, FileAccessStatus.IsDirectory);
    }

    [TestMethod]
    public void Failed_WithAccessDenied_ReportsNotOpenWithNoMetadata()
    {
        var result = FileOpenResult.Failed(FileAccessStatus.AccessDenied);

        AssertIsClosedFailure(result, FileAccessStatus.AccessDenied);
    }

    [TestMethod]
    public void Failed_WithIoError_ReportsNotOpenWithNoMetadata()
    {
        var result = FileOpenResult.Failed(FileAccessStatus.IoError);

        AssertIsClosedFailure(result, FileAccessStatus.IoError);
    }

    [TestMethod]
    public void Equals_ForTwoResultsBuiltTheSameWay_ReturnsTrue()
    {
        using var content = new MemoryStream([7, 8, 9]);
        var lastWriteTimeUtc = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

        var first = FileOpenResult.Opened(content, 3, lastWriteTimeUtc);
        var second = FileOpenResult.Opened(content, 3, lastWriteTimeUtc);

        Assert.AreEqual(first, second);
        Assert.AreEqual(first.GetHashCode(), second.GetHashCode());
    }

    [TestMethod]
    public void Equals_ForTwoFailuresWithTheSameStatus_ReturnsTrue()
    {
        var first = FileOpenResult.Failed(FileAccessStatus.NotFound);
        var second = FileOpenResult.Failed(FileAccessStatus.NotFound);

        Assert.AreEqual(first, second);
        Assert.AreEqual(first.GetHashCode(), second.GetHashCode());
    }

    [TestMethod]
    public void Equals_ForFailuresWithDifferentStatuses_ReturnsFalse()
    {
        var notFound = FileOpenResult.Failed(FileAccessStatus.NotFound);
        var isDirectory = FileOpenResult.Failed(FileAccessStatus.IsDirectory);

        Assert.AreNotEqual(notFound, isDirectory);
    }

    private static void AssertIsClosedFailure(FileOpenResult result, FileAccessStatus expected)
    {
        Assert.AreEqual(expected, result.Status);
        Assert.IsFalse(result.IsOpen);
        Assert.IsNull(result.Content);
        Assert.AreEqual(0L, result.Length);
        Assert.AreEqual(default(DateTimeOffset), result.LastWriteTimeUtc);
    }
}
