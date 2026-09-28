namespace Curl.Protocol.Ssh;

[TestClass]
public sealed class SystemSshRandomSourceTests
{
    [TestMethod]
    public void Fill_WritesBytesThatDifferBetweenCalls()
    {
        SystemSshRandomSource source = new();
        byte[] first = new byte[32];
        byte[] second = new byte[32];

        source.Fill(first);
        source.Fill(second);

        CollectionAssert.AreNotEqual(first, second);
    }
}
