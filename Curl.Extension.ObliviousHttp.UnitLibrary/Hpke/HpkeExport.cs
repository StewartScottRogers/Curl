using Curl.Cryptography;

namespace Curl.Extension.ObliviousHttp.Hpke;

/// <summary><c>Context.Export(exporter_context, L)</c> (RFC 9180 section 5.3) into a new array, for both sides.</summary>
internal static class HpkeExport
{
    /// <summary>A secret of <paramref name="length" /> bytes bound to <paramref name="exporterContext" />.</summary>
    /// <exception cref="ArgumentException"><paramref name="length" /> is not between 1 and 8160.</exception>
    internal static byte[] Of(HpkeContext context, ReadOnlySpan<byte> exporterContext, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        byte[] exportedValue = new byte[length];
        context.Export(exporterContext, exportedValue);
        return exportedValue;
    }
}
