namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins <see cref="BytesBody" /> and <see cref="StreamBody" />: each carries its content
/// and a <see cref="HttpRequestBody.ContentType" /> that is never null (ADR-0014).
/// </summary>
[TestClass]
public sealed class HttpRequestBodyTests
{
    private const string FormType = "application/x-www-form-urlencoded";

    [TestMethod]
    public void BytesBody_Constructor_RoundTripsEveryValue()
    {
        ReadOnlyMemory<byte> content = new byte[] { 0x61, 0x3D, 0x31 };

        var body = new BytesBody(content, FormType);

        Assert.IsTrue(content.Span.SequenceEqual(body.Content.Span));
        Assert.AreEqual(FormType, body.ContentType);
        Assert.IsInstanceOfType<HttpRequestBody>(body);
    }

    [TestMethod]
    public void BytesBody_WithNullContentType_ThrowsArgumentNullException()
    {
        string? contentType = null;

        var exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => new BytesBody(ReadOnlyMemory<byte>.Empty, contentType!));

        Assert.AreEqual("ContentType", exception.ParamName);
    }

    [TestMethod]
    public void BytesBody_WithEmptyContentType_Accepts()
    {
        var body = new BytesBody(ReadOnlyMemory<byte>.Empty, string.Empty);

        Assert.AreEqual(string.Empty, body.ContentType);
    }

    [TestMethod]
    public void BytesBody_Equals_ForSameMemoryAndType_ReturnsTrue()
    {
        ReadOnlyMemory<byte> content = new byte[] { 0x78 };
        var first = new BytesBody(content, FormType);
        var second = new BytesBody(content, FormType);

        Assert.AreEqual(first, second);
        Assert.AreEqual(first.GetHashCode(), second.GetHashCode());
        Assert.AreNotEqual(first, new BytesBody(content, "text/plain"));
    }

    [TestMethod]
    public void BytesBody_WithChangingContent_KeepsContentType()
    {
        var empty = new BytesBody(ReadOnlyMemory<byte>.Empty, FormType);
        ReadOnlyMemory<byte> content = new byte[] { 0x62 };

        var filled = empty with { Content = content };

        Assert.IsTrue(content.Span.SequenceEqual(filled.Content.Span));
        Assert.AreEqual(FormType, filled.ContentType);
    }

    [TestMethod]
    public void StreamBody_WithChangingContent_KeepsLengthAndType()
    {
        using var first = new MemoryStream();
        using var second = new MemoryStream();
        var body = new StreamBody(first, 3, FormType);

        var replaced = body with { Content = second };

        Assert.AreSame(second, replaced.Content);
        Assert.AreEqual(3L, replaced.Length);
        Assert.AreEqual(FormType, replaced.ContentType);
    }

    [TestMethod]
    public void StreamBody_Constructor_RoundTripsEveryValue()
    {
        using var content = new MemoryStream();
        const string multipart = "multipart/form-data; boundary=------------------------abc";

        var body = new StreamBody(content, 42, multipart);

        Assert.AreSame(content, body.Content);
        Assert.AreEqual(42L, body.Length);
        Assert.AreEqual(multipart, body.ContentType);
        Assert.IsInstanceOfType<HttpRequestBody>(body);
    }

    [TestMethod]
    public void StreamBody_WithUnknownLength_KeepsNull()
    {
        using var content = new MemoryStream();

        var body = new StreamBody(content, null, FormType);

        Assert.IsNull(body.Length);
    }

    [TestMethod]
    public void StreamBody_WithNullContentType_ThrowsArgumentNullException()
    {
        using var content = new MemoryStream();
        string? contentType = null;

        var exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => new StreamBody(content, null, contentType!));

        Assert.AreEqual("ContentType", exception.ParamName);
    }

    [TestMethod]
    public void StreamBody_WithChangingLength_KeepsContentAndType()
    {
        using var content = new MemoryStream();
        var sized = new StreamBody(content, 5, FormType);

        var chunked = sized with { Length = null };

        Assert.AreSame(content, chunked.Content);
        Assert.IsNull(chunked.Length);
        Assert.AreEqual(FormType, chunked.ContentType);
        Assert.AreNotEqual(sized, chunked);
    }
}
