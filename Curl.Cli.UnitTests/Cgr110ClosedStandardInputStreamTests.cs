namespace Curl.Cli;

/// <summary>
/// Pins CGR-110 part 1 (BL-3972): a standard input the process was started with closed is a
/// <see cref="ClosedStandardInputStream"/>, which reads as end of file but makes
/// <see cref="DiskDataFileReader.ReadStandardInput"/> throw, while <c>-K -</c> reads it as an
/// empty config file, as curl 8.22.0 does (ADR-0948).
/// </summary>
[TestClass]
public sealed class Cgr110ClosedStandardInputStreamTests
{
    private const string Url = "http://127.0.0.1/a";

    [TestMethod]
    public void Read_ClosedStandardInput_ReadsEndOfFile()
    {
        using ClosedStandardInputStream stream = new();

        Assert.AreEqual(0, stream.Read(new byte[4], 0, 4));
        Assert.AreEqual(-1, stream.ReadByte());
    }

    [TestMethod]
    public void Capabilities_ClosedStandardInput_ReadOnlyAndNotSeekable()
    {
        using ClosedStandardInputStream stream = new();
        stream.Flush();

        Assert.IsTrue(stream.CanRead);
        Assert.IsFalse(stream.CanSeek);
        Assert.IsFalse(stream.CanWrite);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position = 0);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(0));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Write(new byte[1], 0, 1));
    }

    [TestMethod]
    public void InPlaceOfNull_StreamNull_IsAClosedStandardInput()
    {
        Assert.IsInstanceOfType<ClosedStandardInputStream>(ClosedStandardInputStream.InPlaceOfNull(Stream.Null));
    }

    [TestMethod]
    public void InPlaceOfNull_AnyOtherStream_IsThatStream()
    {
        using MemoryStream standardInput = new([1]);

        Assert.AreSame(standardInput, ClosedStandardInputStream.InPlaceOfNull(standardInput));
    }

    [TestMethod]
    public void InPlaceOfNull_Null_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => ClosedStandardInputStream.InPlaceOfNull(null!));
    }

    [TestMethod]
    public void ReadStandardInput_ClosedStandardInput_ThrowsIOException()
    {
        DiskDataFileReader reader = DiskDataFileReader.ForPlatform(true).WithStandardInput(() => new ClosedStandardInputStream());

        Assert.ThrowsExactly<IOException>(() => reader.ReadStandardInput());
    }

    [TestMethod]
    public void ReadStandardInput_StreamNull_ReadsEmpty()
    {
        DiskDataFileReader reader = DiskDataFileReader.ForPlatform(true).WithStandardInput(() => Stream.Null);

        Assert.IsEmpty(reader.ReadStandardInput());
    }

    [TestMethod]
    public void ReadStandardInput_NormalStream_ReadsItsBytes()
    {
        DiskDataFileReader reader = DiskDataFileReader.ForPlatform(true).WithStandardInput(() => new MemoryStream([1, 2]));

        CollectionAssert.AreEqual(new byte[] { 1, 2 }, reader.ReadStandardInput());
    }

    [TestMethod]
    public void Parse_ConfigFileFromClosedStandardInput_ReadsAsEmpty()
    {
        DiskDataFileReader reader = DiskDataFileReader.ForPlatform(true).WithStandardInput(() => new ClosedStandardInputStream());

        CommandLineParseResult result = CommandLineParser.Parse(["-K", "-", Url], _ => true, new EmptyPasswordPrompt(), reader, true);

        Assert.IsNull(result.Refusal);
        Assert.IsEmpty(result.Options!.Headers);
    }
}
