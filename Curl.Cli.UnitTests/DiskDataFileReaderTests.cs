namespace Curl.Cli;

/// <summary>
/// Pins <see cref="DiskDataFileReader"/> through its injected file read and standard-input opener,
/// so no test touches the disk or the real standard input.
/// </summary>
[TestClass]
public sealed class DiskDataFileReaderTests
{
    [TestMethod]
    public void TryReadFile_Readable_ReturnsTheBytes()
    {
        DiskDataFileReader reader = new(_ => [1, 2, 3], () => Stream.Null);

        bool read = reader.TryReadFile("body.txt", out byte[] contents);

        Assert.IsTrue(read);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, contents);
    }

    [TestMethod]
    [DataRow(typeof(FileNotFoundException))]
    [DataRow(typeof(DirectoryNotFoundException))]
    [DataRow(typeof(UnauthorizedAccessException))]
    [DataRow(typeof(ArgumentException))]
    [DataRow(typeof(NotSupportedException))]
    public void TryReadFile_Unreadable_ReturnsFalseAndNoBytes(Type exceptionType)
    {
        Exception failure = (Exception)Activator.CreateInstance(exceptionType)!;
        DiskDataFileReader reader = new(_ => throw failure, () => Stream.Null);

        bool read = reader.TryReadFile("missing", out byte[] contents);

        Assert.IsFalse(read);
        Assert.IsEmpty(contents);
    }

    [TestMethod]
    public void TryReadFile_UnexpectedFailure_IsNotSwallowed()
    {
        DiskDataFileReader reader = new(_ => throw new InvalidOperationException(), () => Stream.Null);

        Assert.ThrowsExactly<InvalidOperationException>(() => reader.TryReadFile("x", out _));
    }

    [TestMethod]
    public void ReadStandardInput_Always_ReturnsEveryByteAndClosesTheStream()
    {
        MemoryStream standardInput = new([0x71, 0x20, 0x72, 0x0A]);
        DiskDataFileReader reader = new(_ => [], () => standardInput);

        byte[] contents = reader.ReadStandardInput();

        CollectionAssert.AreEqual(new byte[] { 0x71, 0x20, 0x72, 0x0A }, contents);
        Assert.IsFalse(standardInput.CanRead);
    }

    [TestMethod]
    public void ForProcess_Always_IsTheSameReader()
    {
        Assert.AreSame(DiskDataFileReader.ForProcess, DiskDataFileReader.ForProcess);
    }
}
