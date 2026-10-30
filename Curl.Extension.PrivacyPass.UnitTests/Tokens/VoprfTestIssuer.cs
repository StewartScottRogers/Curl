using System.Net;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Curl.Extension.PrivacyPass.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.PrivacyPass.Tokens;

/// <summary>
/// The issuer side of RFC 9578 §5 for the tests: the RFC 9497 Appendix A.4.2 VOPRF(P-384, SHA-384) key, a type
/// <c>0x0001</c> challenge, BlindEvaluate with GenerateProof (RFC 9497 §3.3.2, §2.2.1) and the PRF output the client
/// must reach.
/// </summary>
internal static class VoprfTestIssuer
{
    public static BigInteger SecretKey { get; } = Scalar("051646b9e6e7a71ae27c1e1d0b87b4381db6d3595eeeb1adb41579adbf992f4278f9016eafc944edaa2b43183581779d");

    public static byte[] EncodedPublicKey { get; } = Convert.FromHexString("031d689686c611991b55f1a1d8f4305ccd6cb719446f660a30db61b7aa87b46acf59b7c0d4a9077b3da21c25dd482229a0");

    public static P384Point PublicKey { get; } = P384Group.Decode(EncodedPublicKey)!;

    // token_type 1, issuer_name "issuer.example", no redemption context, origin_info "origin.example".
    public static byte[] Challenge { get; } =
        [0x00, 0x01, 0x00, 0x0E, .. "issuer.example"u8, 0x00, 0x00, 0x0E, .. "origin.example"u8];

    public static PrivateTokenChallengeHeader Header { get; } = new(PrivateTokenChallenge.Decode(Challenge)!, EncodedPublicKey);

    public static BigInteger Scalar(string hex) => new(Convert.FromHexString(hex), isUnsigned: true, isBigEndian: true);

    /// <summary>Gives <c>k · blinded</c> and the proof of it drawn with <paramref name="proofRandomScalar"/>.</summary>
    public static (byte[] Evaluated, byte[] Proof) Evaluate(P384Point[] blinded, BigInteger proofRandomScalar)
    {
        var evaluated = blinded.Select(element => P384Group.Multiply(SecretKey, element)).ToArray();
        var (m, z) = VoprfP384Sha384.ComputeComposites(PublicKey, blinded, evaluated);
        var t2 = P384Group.Multiply(proofRandomScalar, P384Group.Generator);
        var t3 = P384Group.Multiply(proofRandomScalar, m);
        var c = VoprfP384Sha384.ChallengeScalar(PublicKey, m, z, t2, t3);
        var s = P384Group.Reduce(proofRandomScalar - (c * SecretKey), P384Group.Order);
        return (P384Group.Encode(evaluated[0]), [.. P384Group.EncodeScalar(c), .. P384Group.EncodeScalar(s)]);
    }

    /// <summary>Gives the PRF output for <paramref name="input"/> under the issuer's key (RFC 9497 §3.3.1 Evaluate).</summary>
    public static byte[] Output(byte[] input)
    {
        var element = P384Group.Encode(P384Group.Multiply(SecretKey, P384HashToCurve.HashToCurve(input, VoprfP384Sha384.HashToGroupTag)));
        return SHA384.HashData([(byte)(input.Length >> 8), (byte)input.Length, .. input, 0, (byte)element.Length, .. element, .. "Finalize"u8]);
    }
}

/// <summary>A fake issuer connection that answers the type <c>0x0001</c> <c>TokenRequest</c> written to it with <see cref="VoprfTestIssuer.Evaluate"/> of its blinded element.</summary>
/// <param name="corruptProof">Whether to flip the last bit of the proof's challenge so it does not verify.</param>
internal sealed class EvaluatingIssuerConnection(bool corruptProof = false) : IConnection
{
    private readonly MemoryStream written = new();
    private byte[]? response;
    private int position;

    public byte[] TokenRequest => written.ToArray()[^(P384Group.ElementLength + 3)..];

    public bool IsSecure => true;

    public EndPoint? RemoteEndPoint => null;

    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        response ??= Answer();
        var count = Math.Min(buffer.Length, response.Length - position);
        response.AsMemory(position, count).CopyTo(buffer);
        position += count;
        return ValueTask.FromResult(count);
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        written.Write(buffer.Span);
        return ValueTask.CompletedTask;
    }

    public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private byte[] Answer()
    {
        var blinded = P384Group.Decode(written.ToArray()[^P384Group.ElementLength..])!;
        var (evaluated, proof) = VoprfTestIssuer.Evaluate([blinded], 7);
        if (corruptProof)
        {
            proof[P384Group.ScalarLength - 1] ^= 1;
        }

        byte[] body = [.. evaluated, .. proof];
        return [.. Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: application/private-token-response\r\nContent-Length: {body.Length}\r\n\r\n"), .. body];
    }
}
