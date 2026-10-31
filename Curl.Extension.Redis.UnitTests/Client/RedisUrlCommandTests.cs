using System.Text;

namespace Curl.Extension.Redis.Client;

/// <summary>Checks reading the command from the URL path, with the redis URI scheme's <c>db-number</c> first.</summary>
[TestClass]
public sealed class RedisUrlCommandTests
{
    [TestMethod]
    [DataRow("/GET/key", null, "GET|key")]
    [DataRow("/2/GET/key", "2", "GET|key")]
    [DataRow("/SET/a%20b/x%2Fy", null, "SET|a b|x/y")]
    [DataRow("/PING", null, "PING")]
    [DataRow("/ECHO/", null, "ECHO|")]
    [DataRow("//PING", null, "|PING")]
    public void Parse_Path_GivesTheDatabaseAndTheArguments(string path, string? database, string arguments)
    {
        RedisUrlCommand command = RedisUrlCommand.Parse(path)!;

        Assert.AreEqual(database, command.Database);
        Assert.AreEqual(arguments, string.Join('|', command.Arguments.Select(Encoding.UTF8.GetString)));
        Assert.AreEqual(arguments.Split('|')[0], command.Name);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("/")]
    [DataRow("/0")]
    public void Parse_PathWithoutACommand_GivesNull(string path) =>
        Assert.IsNull(RedisUrlCommand.Parse(path));

    [TestMethod]
    public void ArgumentsWith_Body_AppendsItAsTheLastArgument()
    {
        RedisUrlCommand command = RedisUrlCommand.Parse("/SET/key")!;

        Assert.AreEqual(2, command.ArgumentsWith(null).Count);
        Assert.AreEqual("v", Encoding.UTF8.GetString(command.ArgumentsWith("v"u8.ToArray())[2]));
    }
}
