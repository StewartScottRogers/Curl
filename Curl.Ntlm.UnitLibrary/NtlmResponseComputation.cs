using System.Buffers.Binary;
using System.Security.Cryptography;
using Curl.Cryptography;

namespace Curl.Ntlm;

/// <summary>
/// Computes the responses to a server challenge and their session keys, per MS-NLMP
/// sections 3.3.1 (NTLMv1, with and without extended session security), 3.3.2 (NTLMv2) and
/// 3.4.5.1 (<c>KXKEY</c>), with strings hashed as curl 8.21.0 hashes them
/// (<see cref="NtlmOneWayFunctions" />). curl itself sends only two of these:
/// <see cref="ComputeV2" /> when the challenge carries
/// <see cref="NtlmNegotiateFlags.NegotiateExtendedSessionSecurity" />, and
/// <see cref="ComputeV1" /> otherwise (<c>Curl_auth_create_ntlm_type3_message</c>,
/// <c>lib/vauth/ntlm.c</c> lines 609 to 667); <see cref="NtlmChallengeAnswerer" /> makes
/// that choice.
/// </summary>
public static class NtlmResponseComputation
{
    /// <summary>The length of a server or client challenge.</summary>
    public const int ChallengeLength = 8;

    /// <summary>The length of an exported session key and of its encryption.</summary>
    public const int SessionKeyLength = 16;

    // The NTLMv2 blob before its timestamp: Responserversion, HiResponserversion and Z(6).
    private const int V2BlobHeaderLength = 8;

    // Header, timestamp, client challenge and Z(4): where the target information starts.
    private const int V2BlobTargetInformationOffset = 28;

    // The Z(4) after the target information.
    private const int V2BlobTrailerLength = 4;

    private const byte LmKeyPadding = 0xBD;

    /// <summary>
    /// NTLMv1 without extended session security: <c>DESL(LMOWFv1)</c> and
    /// <c>DESL(NTOWFv1)</c> of the server challenge, as curl sends them
    /// (<c>Curl_ntlm_core_lm_resp</c>). The key exchange key follows
    /// <paramref name="flags" />: <see cref="NtlmNegotiateFlags.NegotiateLmKey" /> first, then
    /// <see cref="NtlmNegotiateFlags.RequestNonNtSessionKey" />, else the session base key.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="serverChallenge" /> is not <see cref="ChallengeLength" /> bytes.</exception>
    public static NtlmResponses ComputeV1(string password, ReadOnlySpan<byte> serverChallenge, NtlmNegotiateFlags flags)
    {
        RequireLength(serverChallenge, ChallengeLength, nameof(serverChallenge));
        byte[] ntOwf = NtlmOneWayFunctions.ComputeNtOwfV1(password);
        byte[] lmOwf = NtlmOneWayFunctions.ComputeLmOwfV1(password);
        try
        {
            byte[] lmResponse = NtlmDes.Desl(lmOwf, serverChallenge);
            byte[] ntResponse = NtlmDes.Desl(ntOwf, serverChallenge);
            byte[] sessionBaseKey = ComputeMd4(ntOwf);
            byte[] keyExchangeKey = ComputeV1KeyExchangeKey(flags, sessionBaseKey, lmOwf, lmResponse);
            return new NtlmResponses(lmResponse, ntResponse, sessionBaseKey, keyExchangeKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(ntOwf);
            CryptographicOperations.ZeroMemory(lmOwf);
        }
    }

    /// <summary>
    /// NTLMv1 with extended session security (the "NTLM2 session response"): the client
    /// challenge and 16 zero bytes as the LM response, <c>DESL(NTOWFv1)</c> of the first
    /// eight bytes of <c>MD5(server challenge, client challenge)</c> as the NT response, and
    /// <c>HMAC_MD5(SessionBaseKey, server challenge, client challenge)</c> as the key
    /// exchange key. curl 8.21.0 never sends it; it is here for callers that must.
    /// </summary>
    /// <exception cref="ArgumentException">Either challenge is not <see cref="ChallengeLength" /> bytes.</exception>
    public static NtlmResponses ComputeV1WithExtendedSessionSecurity(string password, ReadOnlySpan<byte> serverChallenge, ReadOnlySpan<byte> clientChallenge)
    {
        RequireLength(serverChallenge, ChallengeLength, nameof(serverChallenge));
        RequireLength(clientChallenge, ChallengeLength, nameof(clientChallenge));
        byte[] ntOwf = NtlmOneWayFunctions.ComputeNtOwfV1(password);
        try
        {
            byte[] challenges = [.. serverChallenge, .. clientChallenge];
            byte[] lmResponse = new byte[NtlmDes.DeslLength];
            clientChallenge.CopyTo(lmResponse);
            byte[] ntResponse = NtlmDes.Desl(ntOwf, MD5.HashData(challenges).AsSpan(0, ChallengeLength));
            byte[] sessionBaseKey = ComputeMd4(ntOwf);
            return new NtlmResponses(lmResponse, ntResponse, sessionBaseKey, HMACMD5.HashData(sessionBaseKey, challenges));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(ntOwf);
        }
    }

    /// <summary>
    /// NTLMv2 (<c>Curl_ntlm_core_mk_ntlmv2_resp</c> and <c>Curl_ntlm_core_mk_lmv2_resp</c>,
    /// <c>lib/curl_ntlm_core.c</c>): the NT response is <c>NTProofStr</c> followed by the
    /// blob - version bytes, <paramref name="timestamp" /> as a FILETIME, the client
    /// challenge, four zero bytes, the target information verbatim and four zero bytes - and
    /// the LM response is <c>HMAC_MD5(NTOWFv2, server challenge, client challenge)</c>
    /// followed by the client challenge. Like curl it always sends the LMv2 response and
    /// never reads an <c>MsvAvTimestamp</c>. The session base key is
    /// <c>HMAC_MD5(NTOWFv2, NTProofStr)</c>, and is the key exchange key.
    /// </summary>
    /// <exception cref="ArgumentException">Either challenge is not <see cref="ChallengeLength" /> bytes.</exception>
    public static NtlmResponses ComputeV2(
        string user,
        string domain,
        string password,
        ReadOnlySpan<byte> serverChallenge,
        ReadOnlySpan<byte> clientChallenge,
        DateTimeOffset timestamp,
        ReadOnlySpan<byte> targetInformation)
    {
        RequireLength(serverChallenge, ChallengeLength, nameof(serverChallenge));
        RequireLength(clientChallenge, ChallengeLength, nameof(clientChallenge));
        byte[] ntOwfV1 = NtlmOneWayFunctions.ComputeNtOwfV1(password);
        byte[] ntOwfV2 = NtlmOneWayFunctions.ComputeNtOwfV2(user, domain, ntOwfV1);
        try
        {
            byte[] blob = WriteV2Blob(clientChallenge, timestamp, targetInformation);
            byte[] ntProof = HMACMD5.HashData(ntOwfV2, (byte[])[.. serverChallenge, .. blob]);
            byte[] lmResponse = [.. HMACMD5.HashData(ntOwfV2, (byte[])[.. serverChallenge, .. clientChallenge]), .. clientChallenge];
            byte[] sessionBaseKey = HMACMD5.HashData(ntOwfV2, ntProof);
            return new NtlmResponses(lmResponse, [.. ntProof, .. blob], sessionBaseKey, [.. sessionBaseKey]);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(ntOwfV1);
            CryptographicOperations.ZeroMemory(ntOwfV2);
        }
    }

    /// <summary>
    /// The <c>EncryptedRandomSessionKey</c> an AUTHENTICATE message carries under
    /// <see cref="NtlmNegotiateFlags.NegotiateKeyExchange" />:
    /// <c>RC4K(KeyExchangeKey, ExportedSessionKey)</c> (MS-NLMP section 3.1.5.1.2).
    /// </summary>
    /// <exception cref="ArgumentException">Either key is not <see cref="SessionKeyLength" /> bytes.</exception>
    public static byte[] EncryptSessionKey(ReadOnlySpan<byte> keyExchangeKey, ReadOnlySpan<byte> exportedSessionKey)
    {
        RequireLength(keyExchangeKey, SessionKeyLength, nameof(keyExchangeKey));
        RequireLength(exportedSessionKey, SessionKeyLength, nameof(exportedSessionKey));
        byte[] encrypted = new byte[SessionKeyLength];
        using Rc4 rc4 = new(keyExchangeKey);
        rc4.ApplyKeyStream(exportedSessionKey, encrypted);
        return encrypted;
    }

    // MS-NLMP 3.4.5.1 for NTLMv1 without extended session security.
    private static byte[] ComputeV1KeyExchangeKey(NtlmNegotiateFlags flags, byte[] sessionBaseKey, byte[] lmOwf, byte[] lmResponse)
    {
        byte[] key = new byte[SessionKeyLength];
        if (flags.HasFlag(NtlmNegotiateFlags.NegotiateLmKey))
        {
            ReadOnlySpan<byte> block = lmResponse.AsSpan(0, ChallengeLength);
            ReadOnlySpan<byte> secondKey = [lmOwf[7], LmKeyPadding, LmKeyPadding, LmKeyPadding, LmKeyPadding, LmKeyPadding, LmKeyPadding];
            NtlmDes.Encrypt(lmOwf.AsSpan(0, 7), block, key.AsSpan(0, 8));
            NtlmDes.Encrypt(secondKey, block, key.AsSpan(8, 8));
        }
        else if (flags.HasFlag(NtlmNegotiateFlags.RequestNonNtSessionKey))
        {
            lmOwf.AsSpan(0, 8).CopyTo(key);
        }
        else
        {
            sessionBaseKey.CopyTo(key, 0);
        }

        return key;
    }

    private static byte[] WriteV2Blob(ReadOnlySpan<byte> clientChallenge, DateTimeOffset timestamp, ReadOnlySpan<byte> targetInformation)
    {
        byte[] blob = new byte[V2BlobTargetInformationOffset + targetInformation.Length + V2BlobTrailerLength];
        blob[0] = 1;
        blob[1] = 1;
        BinaryPrimitives.WriteInt64LittleEndian(blob.AsSpan(V2BlobHeaderLength), timestamp.ToFileTime());
        clientChallenge.CopyTo(blob.AsSpan(V2BlobHeaderLength + 8));
        targetInformation.CopyTo(blob.AsSpan(V2BlobTargetInformationOffset));
        return blob;
    }

    private static byte[] ComputeMd4(byte[] data)
    {
        byte[] hash = new byte[Md4.HashSize];
        Md4.HashData(data, hash);
        return hash;
    }

    private static void RequireLength(ReadOnlySpan<byte> value, int length, string parameterName)
    {
        if (value.Length != length)
        {
            throw new ArgumentException($"NTLM needs {length} bytes here; this is {value.Length}.", parameterName);
        }
    }
}
