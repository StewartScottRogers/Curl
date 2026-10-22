using Curl.Conformance.UnitProgram.Asn1.Names;
using Curl.Networking;

namespace Curl.Conformance.UnitProgram.UnitTests.Asn1.Names;

[TestClass]
public sealed class Tool1394CertParameterTests
{
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task RunAsync_ClientCertificateArgumentSplit_EveryRowPassesAndNothingIsWritten(bool recognisesDriveLetters)
    {
        using StringWriter standardError = new();

        UnitProgramResult result = await RunAsync(new Tool1394CertParameter(ClientCertificateArgument.Split, recognisesDriveLetters), standardError);

        Assert.AreEqual(string.Empty, standardError.ToString());
        Assert.AreEqual(0, result.ExitCode);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task RunAsync_RegisteredPortOnWindows_RunsTheDriveLetterRows()
    {
        List<string> asked = [];
        Tool1394CertParameter program = new((value, driveLetters) => { asked.Add(value); return ClientCertificateArgument.Split(value, driveLetters); }, OperatingSystem.IsWindows());

        UnitProgramResult result = await RunAsync(program, new StringWriter());

        Assert.AreEqual(0, result.ExitCode);
        Assert.HasCount(29, asked);
        Assert.AreEqual("tool1394", new Tool1394CertParameter().ProgramName);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task RunAsync_RegisteredPortOffWindows_SkipsTheDriveLetterRows()
    {
        List<string> asked = [];
        Tool1394CertParameter program = new((value, driveLetters) => { asked.Add(value); return ClientCertificateArgument.Split(value, driveLetters); }, OperatingSystem.IsWindows());

        UnitProgramResult result = await RunAsync(program, new StringWriter());

        Assert.AreEqual(0, result.ExitCode);
        Assert.HasCount(16, asked);
    }

    [TestMethod]
    public async Task RunAsync_SplitThatDisagrees_WritesCertnameAndPassphraseLinesAndCountsBoth()
    {
        using StringWriter standardError = new();
        Tool1394CertParameter program = new((value, driveLetters) => value == "foo:bar:baz" ? ("x", null) : ClientCertificateArgument.Split(value, driveLetters), false);

        UnitProgramResult result = await RunAsync(program, standardError);

        string newLine = Environment.NewLine;
        Assert.AreEqual(2, result.ExitCode);
        Assert.AreEqual(
            "tool1394: expected certname 'foo' but got 'x' for -E param 'foo:bar:baz'" + newLine
            + "tool1394: expected passphrase 'bar:baz' but got 'NULL' for -E param 'foo:bar:baz'" + newLine,
            standardError.ToString());
    }

    [TestMethod]
    public async Task RunAsync_PassphraseWhereNoneIsExpected_WritesNullAsExpected()
    {
        using StringWriter standardError = new();
        Tool1394CertParameter program = new((value, driveLetters) => value == "foo\\" ? ("foo\\", "x") : ClientCertificateArgument.Split(value, driveLetters), false);

        UnitProgramResult result = await RunAsync(program, standardError);

        Assert.AreEqual(1, result.ExitCode);
        Assert.AreEqual("tool1394: expected passphrase 'NULL' but got 'x' for -E param 'foo\\'" + Environment.NewLine, standardError.ToString());
    }

    [TestMethod]
    public async Task RunAsync_NullContext_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => new Tool1394CertParameter().RunAsync(null!, CancellationToken.None));
    }

    private static Task<UnitProgramResult> RunAsync(Tool1394CertParameter program, StringWriter standardError) =>
        program.RunAsync(new UnitProgramContext(1394, []) { StandardError = standardError }, CancellationToken.None);
}
