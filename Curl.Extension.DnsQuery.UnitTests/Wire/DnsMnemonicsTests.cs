using Curl.Extension.DnsQuery.Wire;

namespace Curl.Extension.DnsQuery.UnitTests.Wire;

[TestClass]
public sealed class DnsMnemonicsTests
{
    [TestMethod]
    [DataRow((ushort)1, "A")]
    [DataRow((ushort)28, "AAAA")]
    [DataRow((ushort)257, "CAA")]
    [DataRow((ushort)65, "TYPE65")]
    public void TypeMnemonic_KnownAndUnknownTypes_GivesMnemonicOrRfc3597Form(ushort type, string expected) =>
        Assert.AreEqual(expected, DnsMnemonics.TypeMnemonic(type));

    [TestMethod]
    [DataRow((ushort)1, "IN")]
    [DataRow((ushort)3, "CH")]
    [DataRow((ushort)2, "CLASS2")]
    public void ClassMnemonic_KnownAndUnknownClasses_GivesMnemonicOrRfc3597Form(ushort recordClass, string expected) =>
        Assert.AreEqual(expected, DnsMnemonics.ClassMnemonic(recordClass));

    [TestMethod]
    [DataRow(0, "QUERY")]
    [DataRow(3, "OPCODE3")]
    public void OpcodeMnemonic_Rfc1035Section411Opcodes_GivesMnemonic(int opcode, string expected) =>
        Assert.AreEqual(expected, DnsMnemonics.OpcodeMnemonic(opcode));

    [TestMethod]
    [DataRow(3, "NXDOMAIN")]
    [DataRow(9, "RCODE9")]
    public void ResponseCodeMnemonic_Rfc1035Section411Codes_GivesMnemonic(int responseCode, string expected) =>
        Assert.AreEqual(expected, DnsMnemonics.ResponseCodeMnemonic(responseCode));

    [TestMethod]
    [DataRow("A", (ushort)1)]
    [DataRow("aaaa", (ushort)28)]
    [DataRow("TYPE65", (ushort)65)]
    [DataRow("type257", (ushort)257)]
    public void TryParseType_MnemonicOrTypeN_GivesType(string text, ushort expected)
    {
        Assert.IsTrue(DnsMnemonics.TryParseType(text, out ushort type));
        Assert.AreEqual(expected, type);
    }

    [TestMethod]
    [DataRow("BOGUS")]
    [DataRow("TYPE")]
    [DataRow("TYPE70000")]
    [DataRow("TYPE-1")]
    public void TryParseType_TextNamingNoType_Fails(string text) =>
        Assert.IsFalse(DnsMnemonics.TryParseType(text, out _));

    [TestMethod]
    [DataRow("IN", (ushort)1)]
    [DataRow("hs", (ushort)4)]
    [DataRow("CLASS2", (ushort)2)]
    public void TryParseClass_MnemonicOrClassN_GivesClass(string text, ushort expected)
    {
        Assert.IsTrue(DnsMnemonics.TryParseClass(text, out ushort recordClass));
        Assert.AreEqual(expected, recordClass);
    }

    [TestMethod]
    public void TryParseClass_TextNamingNoClass_Fails() =>
        Assert.IsFalse(DnsMnemonics.TryParseClass("TYPE1", out _));
}
