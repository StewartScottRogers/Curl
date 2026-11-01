namespace Curl.Extended.Protocol.OciRegistry.Client;

/// <summary>Checks the path grammar of OCI Distribution Specification 1.1, "Pulling manifests" and "Pulling blobs".</summary>
[TestClass]
public sealed class OciPullReferenceTests
{
    [TestMethod]
    [DataRow("/library/alpine:3.20", "library/alpine", "3.20", false, "/v2/library/alpine/manifests/3.20")]
    [DataRow("/alpine", "alpine", "latest", false, "/v2/alpine/manifests/latest")]
    [DataRow("/a.b/c__d-e:v1_X", "a.b/c__d-e", "v1_X", false, "/v2/a.b/c__d-e/manifests/v1_X")]
    [DataRow("/library/alpine@sha256:abc0", "library/alpine", "sha256:abc0", true, "/v2/library/alpine/blobs/sha256:abc0")]
    public void Parse_ValidPath_GivesNameReferenceAndApiPath(string path, string repository, string reference, bool isBlob, string apiPath)
    {
        var parsed = OciPullReference.Parse(path);

        Assert.AreEqual(new OciPullReference(repository, reference, isBlob), parsed);
        Assert.AreEqual(apiPath, parsed!.ApiPath);
    }

    [TestMethod]
    [DataRow("/")]
    [DataRow("/Library/alpine")]
    [DataRow("/alpine:")]
    [DataRow("/alpine:-x")]
    [DataRow("/alpine@sha256")]
    [DataRow("/Alpine@sha256:abc")]
    [DataRow("/host:5000/alpine")]
    [DataRow("/a//b")]
    [DataRow("/a-")]
    [DataRow("/-a")]
    [DataRow("/a___b")]
    [DataRow("/a.-b")]
    [DataRow("/alpine:.x")]
    [DataRow("/alpine:a+b")]
    [DataRow("/alpine@:abc")]
    [DataRow("/alpine@sha256:")]
    [DataRow("/alpine@sha256:a/b")]
    [DataRow("/alpine@Sha256:abc")]
    [DataRow("/alpine@sha+:abc")]
    public void Parse_PathBreakingTheGrammar_GivesNull(string path) =>
        Assert.IsNull(OciPullReference.Parse(path));

    [TestMethod]
    public void Parse_TagOf128Characters_IsAcceptedAnd129IsNot()
    {
        Assert.IsNotNull(OciPullReference.Parse("/a:" + new string('x', 128)));
        Assert.IsNull(OciPullReference.Parse("/a:" + new string('x', 129)));
    }

    [TestMethod]
    [DataRow("/a--b@sha256+b64u.x_y-z:A=_-9")]
    [DataRow("/a__b/c.d:_T-1.x")]
    public void Parse_EveryGrammarSeparator_IsAccepted(string path) =>
        Assert.IsNotNull(OciPullReference.Parse(path));
}
