namespace Curl.Tls;

/// <summary>
/// The outcome of decoding TLS bytes: the decoded value, or the alert the bytes call for.
/// A codec returns this instead of throwing, so a malformed message from a peer is a
/// value the handshake answers with an alert, never an exception.
/// </summary>
/// <typeparam name="T">The type the bytes decode to.</typeparam>
public sealed class TlsDecodeResult<T>
{
    private readonly T? value;

    private TlsDecodeResult(T? value, TlsAlertDescription? alert)
    {
        this.value = value;
        Alert = alert;
    }

    /// <summary>Gets a value indicating whether the bytes decoded.</summary>
    public bool Succeeded => Alert is null;

    /// <summary>Gets the alert the bytes call for, or <see langword="null" /> when they decoded.</summary>
    public TlsAlertDescription? Alert { get; }

    /// <summary>Gets the decoded value.</summary>
    /// <exception cref="InvalidOperationException">The bytes did not decode.</exception>
    public T Value => Succeeded
        ? value!
        : throw new InvalidOperationException($"The bytes did not decode; they call for the {Alert} alert.");

    /// <summary>Returns a result holding <paramref name="decoded" />.</summary>
    /// <param name="decoded">The decoded value.</param>
    /// <returns>A successful result.</returns>
    public static TlsDecodeResult<T> Success(T decoded) => new(decoded, null);

    /// <summary>Returns a result that calls for <paramref name="alert" />.</summary>
    /// <param name="alert">The alert the bytes call for.</param>
    /// <returns>A failed result.</returns>
    public static TlsDecodeResult<T> Failure(TlsAlertDescription alert) => new(default, alert);
}
