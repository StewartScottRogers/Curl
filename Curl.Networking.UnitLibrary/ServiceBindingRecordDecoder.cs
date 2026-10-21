using System.Buffers.Binary;
using System.Net;
using System.Text;

namespace Curl.Networking;

/// <summary>
/// Decodes one HTTPS record's data (RFC 9460 section 2.2): the 2-byte SvcPriority, the
/// uncompressed TargetName, then SvcParams as key, length and value while 4 or more bytes remain
/// (fewer trailing bytes are ignored), the way curl 8.22.0's <c>doh_resp_decode_httpsrr</c> reads
/// the first HTTPS record of a DoH answer (ADR-0312, upstream unit1658). The keys curl reads are
/// kept: <c>alpn</c> (1), <c>no-default-alpn</c> (2), <c>port</c> (3), <c>ipv4hint</c> (4),
/// <c>ech</c> (5) and <c>ipv6hint</c> (6); any other key, <c>mandatory</c> (0) included, is
/// skipped. Keys must rise strictly, as RFC 9460 requires and curl checks. A malformed record is
/// refused with a <see cref="ServiceBindingFailure" /> rather than read past its end.
/// </summary>
public static class ServiceBindingRecordDecoder
{
    private const int MaximumLabelLength = 63;
    private const int ParameterHeaderLength = 4;
    private const ushort AlpnKey = 1;
    private const ushort NoDefaultAlpnKey = 2;
    private const ushort PortKey = 3;
    private const ushort IPv4HintKey = 4;
    private const ushort EchKey = 5;
    private const ushort IPv6HintKey = 6;

    /// <summary>Decodes <paramref name="recordData" />, the RDATA of one HTTPS or SVCB record.</summary>
    /// <param name="recordData">The record data, as <see cref="DnsAnswer.HttpsRecordData" /> holds it.</param>
    /// <returns>The record, or the failure that stopped the decode.</returns>
    public static ServiceBindingDecoding Decode(ReadOnlySpan<byte> recordData)
    {
        if (recordData.Length < 3)
        {
            return new ServiceBindingDecoding(ServiceBindingFailure.Truncated, null);
        }

        var position = 2;
        var failure = ReadTargetName(recordData, ref position, out var targetName);
        var parameters = new ParameterSet();
        var lowestNextKey = 0;
        while (failure == ServiceBindingFailure.None && recordData.Length - position >= ParameterHeaderLength)
        {
            failure = ReadParameter(recordData, ref position, ref lowestNextKey, parameters);
        }

        return failure == ServiceBindingFailure.None
            ? new ServiceBindingDecoding(failure, parameters.ToRecord(BinaryPrimitives.ReadUInt16BigEndian(recordData), targetName))
            : new ServiceBindingDecoding(failure, null);
    }

    private static ServiceBindingFailure ReadTargetName(ReadOnlySpan<byte> data, ref int position, out string targetName)
    {
        var labels = new List<string>();
        targetName = ".";
        var length = -1;
        while (length != 0)
        {
            if (position >= data.Length)
            {
                return ServiceBindingFailure.BadTargetName;
            }

            length = data[position++];
            if (length > MaximumLabelLength || data.Length - position < length)
            {
                return ServiceBindingFailure.BadTargetName;
            }

            labels.Add(Encoding.Latin1.GetString(data.Slice(position, length)));
            position += length;
        }

        return JoinTargetName(labels, out targetName);
    }

    /// <summary>Joins the labels, the root's empty one last, into the dotted target name and refuses one curl's junk scan refuses.</summary>
    private static ServiceBindingFailure JoinTargetName(List<string> labels, out string targetName)
    {
        targetName = labels.Count > 1 ? string.Join('.', labels[..^1]) : ".";
        return HasControlCharacter(targetName) ? ServiceBindingFailure.TargetNameHasControlCharacter : ServiceBindingFailure.None;
    }

    /// <summary>
    /// Whether <paramref name="targetName" />, up to its first NUL (where curl's C string ends),
    /// holds a byte curl's <c>Curl_junkscan</c> refuses: a space, a control character or DEL.
    /// </summary>
    private static bool HasControlCharacter(string targetName)
    {
        var scanned = targetName.AsSpan();
        var end = scanned.IndexOf('\0');
        if (end >= 0)
        {
            scanned = scanned[..end];
        }

        return scanned.ContainsAnyInRange('\u0001', ' ') || scanned.Contains('\u007F');
    }

    private static ServiceBindingFailure ReadParameter(ReadOnlySpan<byte> data, ref int position, ref int lowestNextKey, ParameterSet parameters)
    {
        var key = BinaryPrimitives.ReadUInt16BigEndian(data[position..]);
        var length = BinaryPrimitives.ReadUInt16BigEndian(data[(position + 2)..]);
        position += ParameterHeaderLength;
        if (key < lowestNextKey)
        {
            return ServiceBindingFailure.ParametersOutOfOrder;
        }

        if (data.Length - position < length)
        {
            return ServiceBindingFailure.ParameterOverrun;
        }

        lowestNextKey = key + 1;
        var value = data.Slice(position, length);
        position += length;
        return parameters.Store(key, value);
    }

    /// <summary>The SvcParams read so far, filled in by <see cref="Store" /> one parameter at a time.</summary>
    private sealed class ParameterSet
    {
        private readonly List<string> _applicationProtocols = [];
        private readonly List<IPAddress> _ipv4Hints = [];
        private readonly List<IPAddress> _ipv6Hints = [];
        private bool _noDefaultApplicationProtocol;
        private ushort? _port;
        private byte[] _echConfigList = [];

        public ServiceBindingFailure Store(ushort key, ReadOnlySpan<byte> value) => key switch
        {
            AlpnKey => StoreApplicationProtocols(value),
            NoDefaultAlpnKey => StoreNoDefaultApplicationProtocol(value),
            PortKey => StorePort(value),
            IPv4HintKey => StoreHints(value, 4, _ipv4Hints),
            EchKey => StoreEchConfigList(value),
            IPv6HintKey => StoreHints(value, 16, _ipv6Hints),
            _ => ServiceBindingFailure.None,
        };

        public ServiceBindingRecord ToRecord(ushort priority, string targetName) => new(priority, targetName)
        {
            ApplicationProtocols = _applicationProtocols,
            NoDefaultApplicationProtocol = _noDefaultApplicationProtocol,
            Port = _port,
            IPv4Hints = _ipv4Hints,
            IPv6Hints = _ipv6Hints,
            EchConfigList = _echConfigList,
        };

        private static ServiceBindingFailure StoreHints(ReadOnlySpan<byte> value, int addressLength, List<IPAddress> hints)
        {
            if (value.IsEmpty || value.Length % addressLength != 0)
            {
                return ServiceBindingFailure.BadParameterValue;
            }

            for (var offset = 0; offset < value.Length; offset += addressLength)
            {
                hints.Add(new IPAddress(value.Slice(offset, addressLength)));
            }

            return ServiceBindingFailure.None;
        }

        private ServiceBindingFailure StoreApplicationProtocols(ReadOnlySpan<byte> value)
        {
            // curl accepts an empty list and an empty identifier, and refuses only one that overruns the value.
            var failure = ServiceBindingFailure.None;
            var offset = 0;
            while (failure == ServiceBindingFailure.None && offset < value.Length)
            {
                int length = value[offset++];
                if (value.Length - offset < length)
                {
                    failure = ServiceBindingFailure.BadApplicationProtocolList;
                }
                else
                {
                    _applicationProtocols.Add(Encoding.Latin1.GetString(value.Slice(offset, length)));
                    offset += length;
                }
            }

            return failure;
        }

        private ServiceBindingFailure StoreNoDefaultApplicationProtocol(ReadOnlySpan<byte> value)
        {
            _noDefaultApplicationProtocol = true;
            return value.IsEmpty ? ServiceBindingFailure.None : ServiceBindingFailure.BadParameterValue;
        }

        private ServiceBindingFailure StorePort(ReadOnlySpan<byte> value)
        {
            if (value.Length != 2)
            {
                return ServiceBindingFailure.BadParameterValue;
            }

            _port = BinaryPrimitives.ReadUInt16BigEndian(value);
            return ServiceBindingFailure.None;
        }

        private ServiceBindingFailure StoreEchConfigList(ReadOnlySpan<byte> value)
        {
            if (value.IsEmpty)
            {
                return ServiceBindingFailure.BadParameterValue;
            }

            _echConfigList = value.ToArray();
            return ServiceBindingFailure.None;
        }
    }
}
