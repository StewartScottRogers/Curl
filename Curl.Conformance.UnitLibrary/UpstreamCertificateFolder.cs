namespace Curl.Conformance;

/// <summary>
/// Prepares the folder upstream's cases read as <c>%CERTDIR</c>: the parent of a <c>certs</c>
/// folder that <see cref="UpstreamTestCertificateGenerator"/> fills from the release's
/// <c>tests/certs/*.prm</c> files (BL-1923, BL-2217). It exists so the upstream conformance
/// tests and the gap office's <c>Gap/Tools/Measure-UpstreamCases.cs</c> prepare the same folder
/// in one line, and pass what it returns to <see cref="UpstreamCaseRunner"/> as its
/// <c>certificateDirectory</c>.
/// </summary>
public static class UpstreamCertificateFolder
{
    /// <summary>The name of the folder under <c>%CERTDIR</c> that holds the files.</summary>
    public const string CertsFolderName = "certs";

    /// <summary>
    /// Generates every certificate file from <paramref name="certsSourceDirectory"/> into
    /// <c>certs</c> under <paramref name="outputDirectory"/>, overwriting what an earlier call
    /// wrote, and returns <paramref name="outputDirectory"/>. Generate once per process and reuse
    /// the folder for every case.
    /// </summary>
    /// <param name="certsSourceDirectory">The release's <c>tests/certs</c> folder, holding <c>test-ca.prm</c> and the other <c>test-*.prm</c> files.</param>
    /// <param name="outputDirectory">The folder to return as <c>%CERTDIR</c>; created when missing.</param>
    /// <param name="timeProvider">The clock the certificates' validity starts from.</param>
    public static string Prepare(string certsSourceDirectory, string outputDirectory, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(outputDirectory);
        new UpstreamTestCertificateGenerator(timeProvider)
            .Generate(certsSourceDirectory, Path.Combine(outputDirectory, CertsFolderName));
        return outputDirectory;
    }
}
