using System.Security.Cryptography;

namespace Curl.Extended.Protocol.Kafka.Wire.KafkaSasl;

/// <summary>The SASL mechanisms the Kafka client speaks and how it picks one from what a broker offers (ADR-1317).</summary>
internal static class KafkaSaslMechanisms
{
    /// <summary>RFC 4616 PLAIN, the mechanism the client tries first.</summary>
    internal const string Plain = "PLAIN";

    /// <summary>RFC 7677 SCRAM-SHA-256.</summary>
    internal const string ScramSha256 = "SCRAM-SHA-256";

    /// <summary>SCRAM with SHA-512, the Kafka mechanism of KIP-84.</summary>
    internal const string ScramSha512 = "SCRAM-SHA-512";

    /// <summary>Gives the strongest SCRAM mechanism among <paramref name="offered"/> (SHA-512 before SHA-256), or <see langword="null"/> when it offers neither.</summary>
    internal static string? StrongestScram(IReadOnlyList<string> offered) =>
        offered.Contains(ScramSha512, StringComparer.Ordinal) ? ScramSha512
        : offered.Contains(ScramSha256, StringComparer.Ordinal) ? ScramSha256
        : null;

    /// <summary>Gives the hash a SCRAM <paramref name="mechanism"/> is built on.</summary>
    internal static HashAlgorithmName HashOf(string mechanism) =>
        mechanism == ScramSha512 ? HashAlgorithmName.SHA512 : HashAlgorithmName.SHA256;
}
