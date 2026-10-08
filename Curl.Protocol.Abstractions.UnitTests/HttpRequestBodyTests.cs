using Curl.Testing;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins <see cref="BytesBody" /> and <see cref="StreamBody" />: each carries its content
/// and a <see cref="HttpRequestBody.ContentType" /> that is never null (ADR-0014).
/// </summary>
[TestClass]
public sealed class HttpRequestBodyTests
{
    private const string FormType = "application/x-www-form-urlencoded";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void BytesBody_Constructor_RoundTripsEveryValue()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        ReadOnlyMemory<byte> content = new byte[] { 0x61, 0x3D, 0x31 };
        diagnostics.Bytes("content", content.Span);
        diagnostics.Arrange("content type", FormType);

        var body = new BytesBody(content, FormType);

        diagnostics.Bytes("body content", body.Content.Span);
        diagnostics.Act("content type", body.ContentType);
        diagnostics.Diff("content", content.ToArray(), body.Content.ToArray());
        diagnostics.Diff("content type", FormType, body.ContentType);
        Assert.IsTrue(content.Span.SequenceEqual(body.Content.Span));
        Assert.AreEqual(FormType, body.ContentType);
        Assert.IsInstanceOfType<HttpRequestBody>(body);
    }

    [TestMethod]
    public void BytesBody_WithNullContentType_ThrowsArgumentNullException()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        string? contentType = null;
        diagnostics.Arrange("content type", contentType);

        var exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => new BytesBody(ReadOnlyMemory<byte>.Empty, contentType!));

        diagnostics.Act("exception message", exception.Message);
        diagnostics.Diff("param name", "ContentType", exception.ParamName ?? string.Empty);
        Assert.AreEqual("ContentType", exception.ParamName);
    }

    [TestMethod]
    public void BytesBody_WithEmptyContentType_Accepts()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("content type", string.Empty);

        var body = new BytesBody(ReadOnlyMemory<byte>.Empty, string.Empty);

        diagnostics.Act("content type", body.ContentType);
        diagnostics.Diff("content type", string.Empty, body.ContentType);
        Assert.AreEqual(string.Empty, body.ContentType);
    }

    [TestMethod]
    public void BytesBody_Equals_ForSameMemoryAndType_ReturnsTrue()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        ReadOnlyMemory<byte> content = new byte[] { 0x78 };
        var first = new BytesBody(content, FormType);
        var second = new BytesBody(content, FormType);
        diagnostics.Bytes("content", content.Span);
        diagnostics.Arrange("content type", FormType);

        bool equal = first.Equals(second);

        diagnostics.Act("equal", equal);
        diagnostics.Act("hash codes", $"{first.GetHashCode() == second.GetHashCode()}");
        diagnostics.Assert("equal", true, equal);
        Assert.AreEqual(first, second);
        Assert.AreEqual(first.GetHashCode(), second.GetHashCode());
        Assert.AreNotEqual(first, new BytesBody(content, "text/plain"));
    }

    [TestMethod]
    public void BytesBody_WithChangingContent_KeepsContentType()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        var empty = new BytesBody(ReadOnlyMemory<byte>.Empty, FormType);
        ReadOnlyMemory<byte> content = new byte[] { 0x62 };
        diagnostics.Arrange("content type", FormType);
        diagnostics.Bytes("new content", content.Span);

        var filled = empty with { Content = content };

        diagnostics.Bytes("filled content", filled.Content.Span);
        diagnostics.Act("content type", filled.ContentType);
        diagnostics.Diff("content", content.ToArray(), filled.Content.ToArray());
        diagnostics.Diff("content type", FormType, filled.ContentType);
        Assert.IsTrue(content.Span.SequenceEqual(filled.Content.Span));
        Assert.AreEqual(FormType, filled.ContentType);
    }

    [TestMethod]
    public void StreamBody_WithChangingContent_KeepsLengthAndType()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        using var first = new MemoryStream();
        using var second = new MemoryStream();
        var body = new StreamBody(first, 3, FormType);
        diagnostics.Arrange("length", 3);
        diagnostics.Arrange("content type", FormType);

        var replaced = body with { Content = second };

        diagnostics.Act("length", replaced.Length);
        diagnostics.Act("content type", replaced.ContentType);
        diagnostics.Assert("length", 3L, replaced.Length);
        Assert.AreSame(second, replaced.Content);
        Assert.AreEqual(3L, replaced.Length);
        Assert.AreEqual(FormType, replaced.ContentType);
    }

    [TestMethod]
    public void StreamBody_Constructor_RoundTripsEveryValue()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        using var content = new MemoryStream();
        const string multipart = "multipart/form-data; boundary=------------------------abc";
        diagnostics.Arrange("length", 42);
        diagnostics.Arrange("content type", multipart);

        var body = new StreamBody(content, 42, multipart);

        diagnostics.Act("length", body.Length);
        diagnostics.Act("content type", body.ContentType);
        diagnostics.Assert("length", 42L, body.Length);
        diagnostics.Diff("content type", multipart, body.ContentType);
        Assert.AreSame(content, body.Content);
        Assert.AreEqual(42L, body.Length);
        Assert.AreEqual(multipart, body.ContentType);
        Assert.IsInstanceOfType<HttpRequestBody>(body);
    }

    [TestMethod]
    public void StreamBody_WithUnknownLength_KeepsNull()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        using var content = new MemoryStream();
        diagnostics.Arrange("length", null);

        var body = new StreamBody(content, null, FormType);

        diagnostics.Act("length", body.Length);
        diagnostics.Assert("length", null, body.Length);
        Assert.IsNull(body.Length);
    }

    [TestMethod]
    public void StreamBody_WithNullContentType_ThrowsArgumentNullException()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        using var content = new MemoryStream();
        string? contentType = null;
        diagnostics.Arrange("content type", contentType);

        var exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => new StreamBody(content, null, contentType!));

        diagnostics.Act("exception message", exception.Message);
        diagnostics.Diff("param name", "ContentType", exception.ParamName ?? string.Empty);
        Assert.AreEqual("ContentType", exception.ParamName);
    }

    [TestMethod]
    public void StreamBody_WithChangingLength_KeepsContentAndType()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        using var content = new MemoryStream();
        var sized = new StreamBody(content, 5, FormType);
        diagnostics.Arrange("length", 5);
        diagnostics.Arrange("content type", FormType);

        var chunked = sized with { Length = null };

        diagnostics.Act("length", chunked.Length);
        diagnostics.Act("content type", chunked.ContentType);
        diagnostics.Act("equals sized", chunked.Equals(sized));
        diagnostics.Assert("length", null, chunked.Length);
        Assert.AreSame(content, chunked.Content);
        Assert.IsNull(chunked.Length);
        Assert.AreEqual(FormType, chunked.ContentType);
        Assert.AreNotEqual(sized, chunked);
    }
}
