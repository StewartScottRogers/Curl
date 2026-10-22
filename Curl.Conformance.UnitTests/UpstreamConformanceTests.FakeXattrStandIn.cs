using System.Text;
using Curl.Console;

namespace Curl.Conformance;

public sealed partial class UpstreamConformanceTests
{
    // The --xattr cases (644, 687, 688) rely on Debug for CURL_FAKE_XATTR: upstream's debug build then sets no
    // extended attribute but prints each one to standard output as "<name> => <value>" (src/tool_xattr.c at
    // curl-8_22_0), which their <stdout> compares. The harness reports Debug to a case setting it (DebugStandInRules),
    // and RunCurlAsync gives the curl tool this writer in place of the file system's (BL-2833). The real tool never
    // reads CURL_FAKE_XATTR.
    internal const string FakeExtendedAttributesVariableName = "CURL_FAKE_XATTR";

    // The writer RunCurlAsync gives curl: the fake one under the case's CURL_FAKE_XATTR, else none, so --xattr sets nothing.
    internal static IExtendedAttributeWriter? ExtendedAttributeWriterFor(UpstreamCurlInvocation invocation) =>
        invocation.EnvironmentVariables.ContainsKey(FakeExtendedAttributesVariableName)
            ? new FakeExtendedAttributeWriter(invocation.StandardOutput)
            : null;

    // Writes each attribute as the debug build's printf("%s => %s\n") does, and reports every one as set.
    internal sealed class FakeExtendedAttributeWriter(Stream standardOutput) : IExtendedAttributeWriter
    {
        public bool TryWrite(string path, string name, string value, out string errorText)
        {
            standardOutput.Write(Encoding.UTF8.GetBytes($"{name} => {value}\n"));
            errorText = string.Empty;
            return true;
        }
    }
}
