namespace Curl.Conformance.Differential.Libcurl.Mime;

/// <summary>One of the tester's <c>mimetest.py</c> cases: a MIME structure posted with <c>CURLOPT_MIMEPOST</c> to the loopback server's <c>/echo</c>.</summary>
/// <param name="Name">The case's name.</param>
/// <param name="Parts">The structure's parts.</param>
public sealed record LibcurlMimeCase(string Name, IReadOnlyList<LibcurlMimePart> Parts);
