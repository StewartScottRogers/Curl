using System.Text;

namespace Curl.Extension.DeltaDownload.UnitTests;

[TestClass]
public sealed class Md4Tests
{
    [TestMethod]
    [DataRow("", "31d6cfe0d16ae931b73c59d7e0c089c0")]
    [DataRow("a", "bde52cb31de33e46245e05fbdbd6fb24")]
    [DataRow("abc", "a448017aaf21d8525fc10ae87aa6729d")]
    [DataRow("message digest", "d9130a8164549fe818874806e1c7014b")]
    [DataRow("abcdefghijklmnopqrstuvwxyz", "d79e1c308aa5bbcdeea8ed63df412da9")]
    [DataRow("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789", "043f8582f241db351ce627e153e7f0e4")]
    [DataRow("12345678901234567890123456789012345678901234567890123456789012345678901234567890", "e33b4ddc9c38f2199c3e7b164fcc0536")]
    public void Hash_TheTestSuite_GivesItsDigests_Rfc1320AppendixA5(string message, string digest)
    {
        Assert.AreEqual(digest, Convert.ToHexStringLower(Md4.Hash(Encoding.ASCII.GetBytes(message))));
    }
}
