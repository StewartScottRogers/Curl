using Curl.Testing;

namespace Curl.Protocol.Ssh.Negotiation;

/// <summary>
/// Writes what an SSH negotiation test arranges and gets - a <c>KEXINIT</c>'s name-lists, a
/// preset's lists and the algorithms agreed - as <c>ARRANGE</c>, <c>ACT</c> and <c>DIFF</c>
/// lines through the shared <see cref="TestDiagnostics" /> helper (BL-1624).
/// </summary>
internal static class SshNegotiationDiagnostics
{
    /// <summary>Writes each name-list of a <c>KEXINIT</c> as an <c>ARRANGE</c> line.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="label">Whose <c>KEXINIT</c> it is.</param>
    /// <param name="kexInit">The <c>KEXINIT</c>.</param>
    public static void ArrangeKexInit(this TestDiagnostics diagnostics, string label, SshKexInit kexInit)
    {
        foreach ((string name, string value) in Lists(kexInit))
        {
            diagnostics.Arrange($"{label} {name}", value);
        }
    }

    /// <summary>Writes each name-list of a <c>KEXINIT</c> as an <c>ACT</c> line.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="label">Whose <c>KEXINIT</c> it is.</param>
    /// <param name="kexInit">The <c>KEXINIT</c>.</param>
    public static void ActKexInit(this TestDiagnostics diagnostics, string label, SshKexInit kexInit)
    {
        foreach ((string name, string value) in Lists(kexInit))
        {
            diagnostics.Act($"{label} {name}", value);
        }
    }

    /// <summary>Writes the algorithms a negotiation agreed, or that it agreed nothing.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="algorithms">The agreed algorithms, or <see langword="null" />.</param>
    public static void ActAlgorithms(this TestDiagnostics diagnostics, SshNegotiatedAlgorithms? algorithms) =>
        diagnostics.Act("negotiated", algorithms?.ToString() ?? "(nothing agreed)");

    /// <summary>Writes the <c>DIFF</c> line between an expected name-list and the actual one.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="label">Which list.</param>
    /// <param name="expected">The expected names, comma-separated.</param>
    /// <param name="actual">The actual names.</param>
    public static void DiffList(this TestDiagnostics diagnostics, string label, string expected, IEnumerable<string> actual) =>
        diagnostics.Diff(label, expected, string.Join(',', actual));

    /// <summary>Joins a name-list as it goes on the wire.</summary>
    /// <param name="names">The names.</param>
    /// <returns>The comma-separated names, or <c>(empty)</c>.</returns>
    public static string Join(IEnumerable<string> names)
    {
        string joined = string.Join(',', names);
        return joined.Length == 0 ? "(empty)" : joined;
    }

    private static IEnumerable<(string Name, string Value)> Lists(SshKexInit kexInit) =>
    [
        ("cookie", Convert.ToHexString(kexInit.Cookie)),
        ("kex", Join(kexInit.KeyExchange)),
        ("host key", Join(kexInit.ServerHostKey)),
        ("cipher c2s", Join(kexInit.CipherClientToServer)),
        ("cipher s2c", Join(kexInit.CipherServerToClient)),
        ("mac c2s", Join(kexInit.MacClientToServer)),
        ("mac s2c", Join(kexInit.MacServerToClient)),
        ("compression c2s", Join(kexInit.CompressionClientToServer)),
        ("compression s2c", Join(kexInit.CompressionServerToClient)),
        ("languages", $"{Join(kexInit.LanguagesClientToServer)} / {Join(kexInit.LanguagesServerToClient)}"),
        ("first kex packet follows", kexInit.FirstKexPacketFollows ? "true" : "false"),
    ];
}
