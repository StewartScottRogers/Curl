using System.Globalization;

namespace Curl.Original.Protocol.Smb;

/// <summary>
/// What a download has received and expects, kept so a <c>-m</c> timeout can say how far the
/// transfer got in the <c>-v</c> line curl's <c>failf</c> writes (BL-5329).
/// </summary>
internal sealed class SmbReceiveCount
{
    /// <summary>Gets or sets the body bytes delivered so far.</summary>
    public long Received { get; set; }

    /// <summary>Gets or sets the file size the open reported, or <see langword="null" /> before the open.</summary>
    public long? Expected { get; set; }

    /// <summary>Words the timeout as curl does: <c>Operation timed out after N milliseconds with M bytes received</c>, or <c>with M out of T bytes received</c> once the size is known.</summary>
    /// <param name="milliseconds">The time since the operation started.</param>
    /// <returns>The timeout's text.</returns>
    public string TimedOutMessage(long milliseconds) =>
        Expected is { } total
            ? string.Create(CultureInfo.InvariantCulture, $"Operation timed out after {milliseconds} milliseconds with {Received} out of {total} bytes received")
            : string.Create(CultureInfo.InvariantCulture, $"Operation timed out after {milliseconds} milliseconds with {Received} bytes received");
}
