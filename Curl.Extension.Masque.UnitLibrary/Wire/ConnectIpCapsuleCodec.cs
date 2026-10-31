using System.Buffers;
using System.Net;
using Curl.HttpDatagram;

namespace Curl.Extension.Masque.Wire;

/// <summary>
/// Encodes and decodes the CONNECT-IP capsules of RFC 9484 section 4.7: <c>ADDRESS_ASSIGN</c>,
/// <c>ADDRESS_REQUEST</c> and <c>ROUTE_ADVERTISEMENT</c>. Decoding refuses a malformed capsule
/// with a <see cref="MasqueException" />.
/// </summary>
internal static class ConnectIpCapsuleCodec
{
    /// <summary>The <c>ADDRESS_ASSIGN</c> capsule type.</summary>
    public const ulong AddressAssignType = 0x01;

    /// <summary>The <c>ADDRESS_REQUEST</c> capsule type.</summary>
    public const ulong AddressRequestType = 0x02;

    /// <summary>The <c>ROUTE_ADVERTISEMENT</c> capsule type.</summary>
    public const ulong RouteAdvertisementType = 0x03;

    /// <summary>Encodes an <c>ADDRESS_ASSIGN</c> capsule.</summary>
    /// <param name="addresses">The assigned addresses; none withdraws every earlier assignment.</param>
    /// <returns>The capsule.</returns>
    public static Capsule EncodeAddressAssign(IEnumerable<ConnectIpAddress> addresses) => new(AddressAssignType, EncodeAddresses(addresses));

    /// <summary>Encodes an <c>ADDRESS_REQUEST</c> capsule.</summary>
    /// <param name="addresses">The requested addresses, each with a nonzero request ID.</param>
    /// <returns>The capsule.</returns>
    public static Capsule EncodeAddressRequest(IEnumerable<ConnectIpAddress> addresses) => new(AddressRequestType, EncodeAddresses(addresses));

    /// <summary>Encodes a <c>ROUTE_ADVERTISEMENT</c> capsule.</summary>
    /// <param name="routes">The address ranges, in the order section 4.7.3 requires.</param>
    /// <returns>The capsule.</returns>
    public static Capsule EncodeRouteAdvertisement(IEnumerable<ConnectIpRoute> routes)
    {
        ArrayBufferWriter<byte> writer = new();
        foreach (var route in routes)
        {
            var start = route.Start.GetAddressBytes();
            writer.Write([IpVersion(start)]);
            writer.Write(start);
            writer.Write(route.End.GetAddressBytes());
            writer.Write([route.IpProtocol]);
        }

        return new(RouteAdvertisementType, writer.WrittenMemory.ToArray());
    }

    /// <summary>Decodes an <c>ADDRESS_ASSIGN</c> capsule.</summary>
    /// <param name="capsule">The capsule.</param>
    /// <returns>The assigned addresses.</returns>
    /// <exception cref="MasqueException">The capsule is of another type or malformed.</exception>
    public static IReadOnlyList<ConnectIpAddress> DecodeAddressAssign(Capsule capsule) =>
        DecodeAddresses(capsule, AddressAssignType, "ADDRESS_ASSIGN");

    /// <summary>Decodes an <c>ADDRESS_REQUEST</c> capsule, which must hold at least one address and no request ID 0.</summary>
    /// <param name="capsule">The capsule.</param>
    /// <returns>The requested addresses.</returns>
    /// <exception cref="MasqueException">The capsule is of another type or malformed.</exception>
    public static IReadOnlyList<ConnectIpAddress> DecodeAddressRequest(Capsule capsule)
    {
        var addresses = DecodeAddresses(capsule, AddressRequestType, "ADDRESS_REQUEST");
        if (addresses.Count == 0 || addresses.Any(address => address.RequestId == 0))
        {
            throw CapsuleValueReader.MalformedCapsule("ADDRESS_REQUEST", "holds no address or a request ID of 0");
        }

        return addresses;
    }

    /// <summary>
    /// Decodes a <c>ROUTE_ADVERTISEMENT</c> capsule, whose ranges must each start no later than
    /// they end and be sorted by IP version, then IP protocol, then start, without overlapping.
    /// </summary>
    /// <param name="capsule">The capsule.</param>
    /// <returns>The address ranges.</returns>
    /// <exception cref="MasqueException">The capsule is of another type or malformed.</exception>
    public static IReadOnlyList<ConnectIpRoute> DecodeRouteAdvertisement(Capsule capsule)
    {
        ArgumentNullException.ThrowIfNull(capsule);
        CapsuleValueReader reader = new(capsule.Value.Span, "ROUTE_ADVERTISEMENT");
        CheckType(capsule, RouteAdvertisementType, "ROUTE_ADVERTISEMENT");
        List<ConnectIpRoute> routes = [];
        while (!reader.AtEnd)
        {
            var length = reader.ReadIpVersion();
            ConnectIpRoute route = new(reader.ReadAddress(length), reader.ReadAddress(length), reader.ReadByte());
            if (Compare(route.Start, route.End) > 0 || (routes.Count > 0 && !FollowsInOrder(routes[^1], route)))
            {
                throw reader.Malformed("holds a range that ends before it starts, or ranges out of order or overlapping");
            }

            routes.Add(route);
        }

        return routes;
    }

    private static byte[] EncodeAddresses(IEnumerable<ConnectIpAddress> addresses)
    {
        ArrayBufferWriter<byte> writer = new();
        foreach (var address in addresses)
        {
            var bytes = address.Address.GetAddressBytes();
            var requestId = writer.GetSpan(HttpDatagramVariableLengthInteger.GetEncodedLength(address.RequestId));
            writer.Advance(HttpDatagramVariableLengthInteger.Write(address.RequestId, requestId));
            writer.Write([IpVersion(bytes)]);
            writer.Write(bytes);
            writer.Write([address.PrefixLength]);
        }

        return writer.WrittenMemory.ToArray();
    }

    private static List<ConnectIpAddress> DecodeAddresses(Capsule capsule, ulong type, string capsuleName)
    {
        ArgumentNullException.ThrowIfNull(capsule);
        CapsuleValueReader reader = new(capsule.Value.Span, capsuleName);
        CheckType(capsule, type, capsuleName);
        List<ConnectIpAddress> addresses = [];
        while (!reader.AtEnd)
        {
            var requestId = reader.ReadVariableLengthInteger();
            var length = reader.ReadIpVersion();
            ConnectIpAddress address = new(requestId, reader.ReadAddress(length), reader.ReadByte());
            if (address.PrefixLength > length * 8)
            {
                throw reader.Malformed($"gives prefix length {address.PrefixLength} to {address.Address}");
            }

            addresses.Add(address);
        }

        return addresses;
    }

    private static void CheckType(Capsule capsule, ulong type, string capsuleName)
    {
        if (capsule.Type != type)
        {
            throw CapsuleValueReader.MalformedCapsule(capsuleName, $"has capsule type {capsule.Type}");
        }
    }

    private static byte IpVersion(byte[] address) => address.Length == 4 ? (byte)4 : (byte)6;

    // Section 4.7.3: by IP version, then IP protocol, then a start after the previous end.
    private static bool FollowsInOrder(ConnectIpRoute previous, ConnectIpRoute route)
    {
        var order = previous.Start.GetAddressBytes().Length.CompareTo(route.Start.GetAddressBytes().Length);
        order = order != 0 ? order : previous.IpProtocol.CompareTo(route.IpProtocol);
        return order < 0 || (order == 0 && Compare(previous.End, route.Start) < 0);
    }

    private static int Compare(IPAddress left, IPAddress right) =>
        left.GetAddressBytes().AsSpan().SequenceCompareTo(right.GetAddressBytes());
}
