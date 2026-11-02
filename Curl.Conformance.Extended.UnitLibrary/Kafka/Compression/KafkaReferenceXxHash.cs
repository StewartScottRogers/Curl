using System.Buffers.Binary;
using System.Numerics;

namespace Curl.Conformance.Extended.Kafka.Compression;

/// <summary>
/// XXH32 and XXH64 with seed 0 (Cyan4973/xxHash doc/xxhash_spec.md), which the LZ4 frame's header, block and content
/// checksums and zstd's content checksum (RFC 8878 section 3.1.1, the low 32 bits of XXH64) are made with.
/// </summary>
public static class KafkaReferenceXxHash
{
    private const uint Prime32First = 2654435761U;
    private const uint Prime32Second = 2246822519U;
    private const uint Prime32Third = 3266489917U;
    private const uint Prime32Fourth = 668265263U;
    private const uint Prime32Fifth = 374761393U;
    private const ulong Prime64First = 11400714785074694791UL;
    private const ulong Prime64Second = 14029467366897019727UL;
    private const ulong Prime64Third = 1609587929392839161UL;
    private const ulong Prime64Fourth = 9650029242287828579UL;
    private const ulong Prime64Fifth = 2870177450012600261UL;

    /// <summary>The XXH32 of <paramref name="input"/>.</summary>
    /// <param name="input">The bytes.</param>
    /// <returns>The hash.</returns>
    public static uint Hash32(ReadOnlySpan<byte> input)
    {
        int position = 0;
        uint hash = Prime32Fifth;
        if (input.Length >= 16)
        {
            uint[] lanes = [unchecked(Prime32First + Prime32Second), Prime32Second, 0, unchecked(0U - Prime32First)];
            for (; position <= input.Length - 16; position += 16)
            {
                for (int lane = 0; lane < 4; lane++)
                {
                    lanes[lane] = Round32(lanes[lane], BinaryPrimitives.ReadUInt32LittleEndian(input[(position + (lane * 4))..]));
                }
            }

            hash = BitOperations.RotateLeft(lanes[0], 1) + BitOperations.RotateLeft(lanes[1], 7) + BitOperations.RotateLeft(lanes[2], 12) + BitOperations.RotateLeft(lanes[3], 18);
        }

        hash += (uint)input.Length;
        for (; position + 4 <= input.Length; position += 4)
        {
            hash = BitOperations.RotateLeft(hash + (BinaryPrimitives.ReadUInt32LittleEndian(input[position..]) * Prime32Third), 17) * Prime32Fourth;
        }

        for (; position < input.Length; position++)
        {
            hash = BitOperations.RotateLeft(hash + (input[position] * Prime32Fifth), 11) * Prime32First;
        }

        hash = (hash ^ (hash >> 15)) * Prime32Second;
        hash = (hash ^ (hash >> 13)) * Prime32Third;
        return hash ^ (hash >> 16);
    }

    /// <summary>The XXH64 of <paramref name="input"/>.</summary>
    /// <param name="input">The bytes.</param>
    /// <returns>The hash.</returns>
    public static ulong Hash64(ReadOnlySpan<byte> input)
    {
        int position = input.Length - (input.Length % 32);
        ulong hash = (input.Length >= 32 ? Stripes64(input[..position]) : Prime64Fifth) + (ulong)input.Length;
        for (; position + 8 <= input.Length; position += 8)
        {
            hash = (BitOperations.RotateLeft(hash ^ Round64(0, BinaryPrimitives.ReadUInt64LittleEndian(input[position..])), 27) * Prime64First) + Prime64Fourth;
        }

        for (; position + 4 <= input.Length; position += 4)
        {
            hash = (BitOperations.RotateLeft(hash ^ (BinaryPrimitives.ReadUInt32LittleEndian(input[position..]) * Prime64First), 23) * Prime64Second) + Prime64Third;
        }

        for (; position < input.Length; position++)
        {
            hash = BitOperations.RotateLeft(hash ^ (input[position] * Prime64Fifth), 11) * Prime64First;
        }

        hash = (hash ^ (hash >> 33)) * Prime64Second;
        hash = (hash ^ (hash >> 29)) * Prime64Third;
        return hash ^ (hash >> 32);
    }

    // The four lanes over whole 32-byte stripes, converged and merged.
    private static ulong Stripes64(ReadOnlySpan<byte> stripes)
    {
        ulong[] lanes = [unchecked(Prime64First + Prime64Second), Prime64Second, 0, unchecked(0UL - Prime64First)];
        for (int position = 0; position < stripes.Length; position += 8)
        {
            int lane = (position / 8) % 4;
            lanes[lane] = Round64(lanes[lane], BinaryPrimitives.ReadUInt64LittleEndian(stripes[position..]));
        }

        ulong hash = BitOperations.RotateLeft(lanes[0], 1) + BitOperations.RotateLeft(lanes[1], 7) + BitOperations.RotateLeft(lanes[2], 12) + BitOperations.RotateLeft(lanes[3], 18);
        foreach (ulong lane in lanes)
        {
            hash = ((hash ^ Round64(0, lane)) * Prime64First) + Prime64Fourth;
        }

        return hash;
    }

    private static uint Round32(uint accumulator, uint lane) => BitOperations.RotateLeft(accumulator + (lane * Prime32Second), 13) * Prime32First;

    private static ulong Round64(ulong accumulator, ulong lane) => BitOperations.RotateLeft(accumulator + (lane * Prime64Second), 31) * Prime64First;
}
