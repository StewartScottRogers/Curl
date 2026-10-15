using Curl.Api.Callbacks;

namespace Curl.Api.Options.Http;

[TestClass]
public sealed class EasyMimeBodyTests
{
    private static readonly EasyMimePart DefaultPart = new();

    [TestMethod]
    public void New_EasyMimeBody_HasNoParts()
    {
        Assert.IsEmpty(new EasyMimeBody().Parts);
    }

    [TestMethod]
    public void New_EasyMimePart_IsAnEmptyPartWithNothingSet()
    {
        Assert.IsNull(DefaultPart.Name);
        Assert.IsNull(DefaultPart.FileName);
        Assert.IsNull(DefaultPart.Type);
        Assert.IsNull(DefaultPart.Encoder);
        Assert.IsEmpty(DefaultPart.Headers);
        Assert.IsNull(DefaultPart.SubParts);
        Assert.IsNull(DefaultPart.Data);
    }

    [TestMethod]
    public void With_EachPartProperty_ChangesOnlyThatProperty()
    {
        AssertChangesOnly(DefaultPart with { Name = "field" }, p => p with { Name = null });
        AssertChangesOnly(DefaultPart with { FileName = "a.txt" }, p => p with { FileName = null });
        AssertChangesOnly(DefaultPart with { Type = "text/plain" }, p => p with { Type = null });
        AssertChangesOnly(DefaultPart with { Encoder = "base64" }, p => p with { Encoder = null });
        AssertChangesOnly(DefaultPart with { Headers = ["X-A: 1"] }, p => p with { Headers = DefaultPart.Headers });
        AssertChangesOnly(DefaultPart with { SubParts = new EasyMimeBody() }, p => p with { SubParts = null });
        AssertChangesOnly(DefaultPart with { Data = new EasyMimeFile("a.txt") }, p => p with { Data = null });
    }

    [TestMethod]
    public void EasyMimeBody_Parts_KeepTheirOrder()
    {
        EasyMimePart first = new() { Name = "first" };
        EasyMimePart second = new() { Name = "second" };

        EasyMimeBody body = new() { Parts = [first, second] };

        Assert.AreSame(first, body.Parts[0]);
        Assert.AreSame(second, body.Parts[1]);
    }

    [TestMethod]
    public void EasyMimeBytes_Bytes_HoldsTheGivenContent()
    {
        byte[] content = [1, 2, 3];

        EasyMimeData data = new EasyMimeBytes(content);

        CollectionAssert.AreEqual(content, ((EasyMimeBytes)data).Bytes.ToArray());
    }

    [TestMethod]
    public void EasyMimeFile_Path_HoldsTheGivenPath()
    {
        EasyMimeData data = new EasyMimeFile("dir/a.txt");

        Assert.AreEqual("dir/a.txt", ((EasyMimeFile)data).Path);
    }

    [TestMethod]
    public void EasyMimeStandardInput_IsDataOfItsOwnKind()
    {
        EasyMimeData data = new EasyMimeStandardInput();

        Assert.AreEqual(new EasyMimeStandardInput(), data);
        Assert.AreNotEqual<EasyMimeData>(new EasyMimeFile("-"), data);
    }

    [TestMethod]
    public void EasyMimeCallback_HoldsTheSizeAndBothCallbacks()
    {
        ReadCallback read = _ => 0;
        SeekCallback seek = (_, _) => SeekResult.Ok;

        EasyMimeCallback data = new(-1, read, seek);

        Assert.AreEqual(-1L, data.Size);
        Assert.AreSame(read, data.Read);
        Assert.AreSame(seek, data.Seek);
    }

    [TestMethod]
    public void EasyStreamDependency_HoldsTheParentAndExclusivity()
    {
        object parent = new();

        EasyStreamDependency dependency = new(parent, true);

        Assert.AreSame(parent, dependency.Parent);
        Assert.IsTrue(dependency.Exclusive);
    }

    private static void AssertChangesOnly(EasyMimePart changed, Func<EasyMimePart, EasyMimePart> restore)
    {
        Assert.AreNotEqual(DefaultPart, changed);
        Assert.AreEqual(DefaultPart, restore(changed));
    }
}
