using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.Options;
using Libcurl.Callbacks.ReadWrite;
using Libcurl.Options;

namespace Libcurl.UnitTests.Callbacks.ReadWrite;

[TestClass]
public sealed class EasyReadWriteCallbackSettersTests
{
    [TestMethod]
    public void SetOption_WriteReadAndHeaderFunctions_SetTheirCallbacksAndNullClearsThem()
    {
        var options = new EasyOptions();
        WriteCallback write = data => data.Length;
        ReadCallback read = _ => 0;
        HeaderCallback header = line => line.Length;

        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.Writefunction, write));
        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.Readfunction, read));
        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.Headerfunction, header));
        Assert.AreSame(write, options.Callbacks.Write);
        Assert.AreSame(read, options.Callbacks.Read);
        Assert.AreSame(header, options.Callbacks.Header);

        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.Writefunction, null));
        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.Readfunction, null));
        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.Headerfunction, null));
        Assert.IsNull(options.Callbacks.Write);
        Assert.IsNull(options.Callbacks.Read);
        Assert.IsNull(options.Callbacks.Header);
    }

    [TestMethod]
    [DataRow(CurlOption.Writefunction)]
    [DataRow(CurlOption.Readfunction)]
    [DataRow(CurlOption.Headerfunction)]
    public void SetOption_FunctionGivenAnotherDelegateType_ReturnsBadFunctionArgumentAndChangesNothing(CurlOption option)
    {
        var options = new EasyOptions();
        var before = options;

        Assert.AreEqual(CurlCode.BadFunctionArgument, Set(ref options, option, new Func<int>(() => 1)));
        Assert.AreSame(before, options);
    }

    [TestMethod]
    [DataRow(CurlOption.Writefunction)]
    [DataRow(CurlOption.Readfunction)]
    [DataRow(CurlOption.Headerfunction)]
    public void SetOption_FunctionGivenANonDelegate_ReturnsBadFunctionArgument(CurlOption option)
    {
        var options = new EasyOptions();

        Assert.AreEqual(CurlCode.BadFunctionArgument, Set(ref options, option, "fwrite"));
    }

    [TestMethod]
    public void SetOption_WriteDataStreamWithNoFunction_WritesTheBodyToTheStream()
    {
        var options = new EasyOptions();
        var body = new MemoryStream();

        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.Writedata, body));
        long took = options.Callbacks.Write!("hello"u8);

        Assert.AreEqual(5, took);
        Assert.AreEqual("hello", Encoding.ASCII.GetString(body.ToArray()));
    }

    [TestMethod]
    public void SetOption_HeaderDataStreamWithNoFunction_WritesTheHeaderLinesToTheStream()
    {
        var options = new EasyOptions();
        var headers = new MemoryStream();

        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.Headerdata, headers));
        long took = options.Callbacks.Header!("HTTP/1.1 200 OK\r\n"u8);

        Assert.AreEqual(17, took);
        Assert.AreEqual("HTTP/1.1 200 OK\r\n", Encoding.ASCII.GetString(headers.ToArray()));
    }

    [TestMethod]
    public void SetOption_ReadDataStreamWithNoFunction_ReadsTheUploadFromTheStream()
    {
        var options = new EasyOptions();
        byte[] buffer = new byte[8];

        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.Readdata, new MemoryStream("abc"u8.ToArray())));
        long first = options.Callbacks.Read!(buffer);
        long end = options.Callbacks.Read!(buffer);

        Assert.AreEqual(3, first);
        Assert.AreEqual(0, end);
        Assert.AreEqual("abc", Encoding.ASCII.GetString(buffer, 0, 3));
    }

    [TestMethod]
    public void SetOption_SecondWriteDataStream_ReplacesTheFirst()
    {
        var options = new EasyOptions();
        var first = new MemoryStream();
        var second = new MemoryStream();

        Set(ref options, CurlOption.Writedata, first);
        Set(ref options, CurlOption.Writedata, second);
        options.Callbacks.Write!("x"u8);

        Assert.AreEqual(0, first.Length);
        Assert.AreEqual(1, second.Length);
    }

    [TestMethod]
    [DataRow(CurlOption.Writedata)]
    [DataRow(CurlOption.Readdata)]
    [DataRow(CurlOption.Headerdata)]
    public void SetOption_DataStreamAfterTheCallersOwnFunction_KeepsTheFunction(CurlOption option)
    {
        var options = new EasyOptions();
        WriteCallback write = data => data.Length;
        ReadCallback read = _ => 0;
        HeaderCallback header = line => line.Length;
        Set(ref options, CurlOption.Writefunction, write);
        Set(ref options, CurlOption.Readfunction, read);
        Set(ref options, CurlOption.Headerfunction, header);

        Assert.AreEqual(CurlCode.Ok, Set(ref options, option, new MemoryStream()));

        Assert.AreSame(write, options.Callbacks.Write);
        Assert.AreSame(read, options.Callbacks.Read);
        Assert.AreSame(header, options.Callbacks.Header);
    }

    [TestMethod]
    [DataRow(CurlOption.Writedata)]
    [DataRow(CurlOption.Readdata)]
    [DataRow(CurlOption.Headerdata)]
    public void SetOption_DataGivenAnyOtherValue_ReturnsOkAndChangesNothing(CurlOption option)
    {
        var options = new EasyOptions();
        var before = options;

        Assert.AreEqual(CurlCode.Ok, Set(ref options, option, new object()));
        Assert.AreEqual(CurlCode.Ok, Set(ref options, option, null));
        Assert.AreSame(before, options);
    }

    [TestMethod]
    public void Write_StreamFails_ReturnsZeroSoTheTransferFailsWithWriteError()
    {
        var callbacks = new StreamDataCallbacks(new FailingStream());

        Assert.AreEqual(0, callbacks.Write("x"u8));
    }

    private static CurlCode Set(ref EasyOptions options, CurlOption option, object? value) =>
        EasyOptionDispatch.Default.SetOption(ref options, option, value);

    private sealed class FailingStream : MemoryStream
    {
        public override void Write(ReadOnlySpan<byte> buffer) => throw new IOException("disk full");
    }
}
