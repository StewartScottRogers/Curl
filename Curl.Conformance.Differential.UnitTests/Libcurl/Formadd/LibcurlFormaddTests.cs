using System.Text;
using Curl.Conformance.Differential.Libcurl.Formadd;

namespace Curl.Conformance.Differential.UnitTests.Libcurl.Formadd;

[TestClass]
public sealed class LibcurlFormaddTests
{
    [TestMethod]
    public void Add_WritesTheCallsCode() => Assert.AreEqual("add=6", LibcurlFormaddLogWriter.Add(6));

    [TestMethod]
    public void Get_WritesTheCallsCode() => Assert.AreEqual("get=26", LibcurlFormaddLogWriter.Get(26));

    [TestMethod]
    public void NoForm_SaysNothingWasRead() => StringAssert.EndsWith(LibcurlFormaddLogWriter.NoForm, "=none");

    [TestMethod]
    public void Join_PutsTheFormGetOutputBeforeTheWireLog() =>
        Assert.AreEqual("formget:\nab\nwire:\ncd", Encoding.ASCII.GetString(LibcurlFormaddLogWriter.Join("ab"u8.ToArray(), "cd"u8.ToArray())));

    [TestMethod]
    public void All_NamesEveryCaseOnce()
    {
        var names = LibcurlFormaddCases.All.Select(c => c.Name).ToList();
        Assert.HasCount(names.Count, names.Distinct());
        Assert.AreEqual("copyname-copycontents", names[0]);
    }

    [TestMethod]
    public void All_PassesEveryCurlformOption()
    {
        var options = LibcurlFormaddCases.All.SelectMany(c => c.Calls).SelectMany(call => call).SelectMany(a => a.Array.Prepend(a)).Select(a => a.Option).ToHashSet();
        for (var option = LibcurlFormaddCases.Nothing; option <= LibcurlFormaddCases.ContentLen; option++)
        {
            Assert.Contains(option, options);
        }
    }

    [TestMethod]
    public void All_SendsEveryCaseButTheStream()
    {
        var held = LibcurlFormaddCases.All.Where(c => !c.Sends).Select(c => c.Name).ToList();
        CollectionAssert.AreEqual(new[] { "stream" }, held);
    }

    [TestMethod]
    public void All_KeepsEachValuesKind()
    {
        var binary = LibcurlFormaddCases.All.Single(c => c.Name == "contentslength-binary").Calls[0];
        Assert.AreEqual(new LibcurlFormaddArgument(LibcurlFormaddCases.PtrContents, LibcurlFormaddValueKind.Bytes, "6162006364", 0, [], []).ToString(), binary[1].ToString());
        Assert.AreEqual(LibcurlFormaddValueKind.Length, binary[2].Kind);
        Assert.AreEqual(5, binary[2].Number);
        var header = LibcurlFormaddCases.All.Single(c => c.Name == "contentheader").Calls[0][2];
        CollectionAssert.AreEqual(new[] { "X-A: 1", "X-B: two" }, header.Lines.ToArray());
        var file = LibcurlFormaddCases.All.Single(c => c.Name == "file").Calls[0][1];
        Assert.AreEqual(LibcurlFormaddValueKind.File, file.Kind);
        Assert.AreEqual("formfile.txt", file.Text);
        var nested = LibcurlFormaddCases.All.Single(c => c.Name == "error-illegal-array").Calls[0][1];
        Assert.AreEqual(LibcurlFormaddValueKind.Array, nested.Array[0].Kind);
        Assert.AreEqual(LibcurlFormaddValueKind.Pointer, LibcurlFormaddCases.All.Single(c => c.Name == "stream").Calls[0][1].Kind);
        Assert.IsNull(LibcurlFormaddCases.All.Single(c => c.Name == "error-null-name").Calls[0][0].Text);
        Assert.IsEmpty(LibcurlFormaddCases.All.Single(c => c.Name == "error-incomplete-empty").Calls[0]);
        Assert.HasCount(20, LibcurlFormaddCases.All.Single(c => c.Name == "many-fields").Calls);
    }

    [TestMethod]
    public void Join_RefusesNull()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => LibcurlFormaddLogWriter.Join(null!, []));
        Assert.ThrowsExactly<ArgumentNullException>(() => LibcurlFormaddLogWriter.Join([], null!));
    }
}
