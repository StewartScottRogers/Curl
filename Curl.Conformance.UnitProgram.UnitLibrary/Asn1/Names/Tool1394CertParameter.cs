using Curl.Conformance.UnitProgram.Hashes;
using Curl.Networking;

namespace Curl.Conformance.UnitProgram.Asn1.Names;

/// <summary>
/// The port of curl 8.22.0's <c>tests/tunit/tool1394.c</c>: <c>parse_cert_parameter</c>, here
/// <see cref="ClientCertificateArgument.Split"/>, over the C program's table row for row; its
/// <c>NULL</c> passphrase is <c>Split</c>'s <see langword="null"/>. The <c>#ifdef _WIN32</c> drive-letter
/// rows run only where the Windows build's drive-letter reading is on (ADR-0723).
/// </summary>
/// <param name="split">Splits a <c>-E</c> value into certificate and passphrase; <see cref="ClientCertificateArgument.Split"/> unless a test injects another.</param>
/// <param name="recognisesDriveLetters">Whether this is the Windows build, which runs the drive-letter rows and keeps a drive letter's colon.</param>
public sealed class Tool1394CertParameter(Func<string, bool, (string Path, string? Passphrase)> split, bool recognisesDriveLetters) : IUnitProgram
{
    private static readonly (string Param, string Cert, string? Passphrase, bool WindowsOnly)[] Rows =
    [
        ("foo:bar:baz", "foo", "bar:baz", false),
        ("foo\\:bar:baz", "foo:bar", "baz", false),
        ("foo\\\\:bar:baz", "foo\\", "bar:baz", false),
        ("foo:bar\\:baz", "foo", "bar\\:baz", false),
        ("foo:bar\\\\:baz", "foo", "bar\\\\:baz", false),
        ("foo\\bar\\baz", "foo\\bar\\baz", null, false),
        ("foo\\\\bar\\\\baz", "foo\\bar\\baz", null, false),
        ("foo\\", "foo\\", null, false),
        ("foo\\\\", "foo\\", null, false),
        ("foo:bar\\", "foo", "bar\\", false),
        ("foo:bar\\\\", "foo", "bar\\\\", false),
        ("foo:bar:", "foo", "bar:", false),
        ("foo\\::bar\\:", "foo:", "bar\\:", false),
        ("pkcs11:foobar", "pkcs11:foobar", null, false),
        ("PKCS11:foobar", "PKCS11:foobar", null, false),
        ("PkCs11:foobar", "PkCs11:foobar", null, false),
        ("c:\\foo:bar:baz", "c:\\foo", "bar:baz", true),
        ("c:\\foo\\:bar:baz", "c:\\foo:bar", "baz", true),
        ("c:\\foo\\\\:bar:baz", "c:\\foo\\", "bar:baz", true),
        ("c:\\foo:bar\\:baz", "c:\\foo", "bar\\:baz", true),
        ("c:\\foo:bar\\\\:baz", "c:\\foo", "bar\\\\:baz", true),
        ("c:\\foo\\bar\\baz", "c:\\foo\\bar\\baz", null, true),
        ("c:\\foo\\\\bar\\\\baz", "c:\\foo\\bar\\baz", null, true),
        ("c:\\foo\\", "c:\\foo\\", null, true),
        ("c:\\foo\\\\", "c:\\foo\\", null, true),
        ("c:\\foo:bar\\", "c:\\foo", "bar\\", true),
        ("c:\\foo:bar\\\\", "c:\\foo", "bar\\\\", true),
        ("c:\\foo:bar:", "c:\\foo", "bar:", true),
        ("c:\\foo\\::bar\\:", "c:\\foo:", "bar\\:", true),
    ];

    /// <summary>Creates the port over <see cref="ClientCertificateArgument.Split"/>, reading drive letters on Windows only, as the platform's curl build does.</summary>
    public Tool1394CertParameter()
        : this(ClientCertificateArgument.Split, OperatingSystem.IsWindows())
    {
    }

    /// <inheritdoc />
    public string ProgramName => "tool1394";

    /// <inheritdoc />
    public Task<UnitProgramResult> RunAsync(UnitProgramContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        UnitChecks checks = new(context.StandardError, ProgramName);
        foreach (var row in Rows.Where(row => recognisesDriveLetters || !row.WindowsOnly))
        {
            (string path, string? passphrase) = split(row.Param, recognisesDriveLetters);
            checks.FailUnless(path == row.Cert, $"expected certname '{row.Cert}' but got '{path}' for -E param '{row.Param}'");
            checks.FailUnless(passphrase == row.Passphrase, $"expected passphrase '{row.Passphrase ?? "NULL"}' but got '{passphrase ?? "NULL"}' for -E param '{row.Param}'");
        }

        return Task.FromResult(checks.Result);
    }
}
