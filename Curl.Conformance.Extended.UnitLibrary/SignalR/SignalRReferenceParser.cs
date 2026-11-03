namespace Curl.Conformance.Extended.SignalR;

/// <summary>Reads one whole item from the start of the bytes a connection received, such as <see cref="SignalRReferenceFrame.TryRead" />.</summary>
/// <typeparam name="T">The item read.</typeparam>
/// <param name="pending">The bytes received and not yet read.</param>
/// <param name="item">The item, or <see langword="null" /> when it is not whole yet.</param>
/// <returns>The number of bytes the item took; 0 when it is not whole yet.</returns>
public delegate int SignalRReferenceParser<T>(ReadOnlySpan<byte> pending, out T? item)
    where T : class;
