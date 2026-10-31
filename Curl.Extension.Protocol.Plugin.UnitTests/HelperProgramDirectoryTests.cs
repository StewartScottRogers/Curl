namespace Curl.Extension.Protocol.Plugin;

/// <summary>Checks the listing of one <c>PATH</c> directory's helper files, in a temporary directory it deletes.</summary>
[TestClass]
public sealed class HelperProgramDirectoryTests
{
    [TestMethod]
    public void ListHelperFiles_GivesOnlyTheCurlProtoFiles()
    {
        string directory = Directory.CreateTempSubdirectory("bl4160-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(directory, "curl-proto-demo"), string.Empty);
            File.WriteAllText(Path.Combine(directory, "other"), string.Empty);

            string[] files = [.. HelperProgramDirectory.ListHelperFiles(directory)];

            CollectionAssert.AreEqual(new[] { Path.Combine(directory, "curl-proto-demo") }, files);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void ListHelperFiles_MissingDirectory_GivesNone() =>
        Assert.IsEmpty(HelperProgramDirectory.ListHelperFiles(Path.Combine(Path.GetTempPath(), "bl4160-missing-" + Guid.NewGuid().ToString("N"))));
}
