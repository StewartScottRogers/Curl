using System.Reflection;

namespace Curl.Mcp.Protocol;

[TestClass]
public sealed class ProtocolLibraryDependencyTests
{
    [TestMethod]
    public void ProtocolLibrary_ReferencesNoCurlAssemblyAndNoPackage()
    {
        Assembly library = Assembly.Load("Curl.Mcp.Protocol.UnitLibrary");

        string[] references = library.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => !name.StartsWith("System", StringComparison.Ordinal) && name != "netstandard" && name != "mscorlib")
            .ToArray();

        CollectionAssert.AreEqual(Array.Empty<string>(), references);
    }
}
