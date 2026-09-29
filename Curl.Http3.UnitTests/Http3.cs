namespace Curl.Http3;

/// <summary>
/// Shared helpers for the HTTP/3 framing tests: streams built from hex, and the connection
/// error a failure reports.
/// </summary>
internal static class Http3
{
    /// <summary>
    /// A readable stream holding the bytes written in hex, groups separated by spaces.
    /// </summary>
    public static MemoryStream StreamOf(string hex) => new(Qpack.FromHex(hex));

    /// <summary>
    /// Runs <paramref name="action" /> and returns the <see cref="Http3ErrorCode" /> it failed with.
    /// </summary>
    public static async Task<Http3ErrorCode> ErrorOfAsync(Func<Task> action) =>
        (await Assert.ThrowsExactlyAsync<Http3Exception>(action)).ErrorCode;

    /// <summary>
    /// Reads every frame the stream holds, as <see cref="Http3FrameReader" /> returns them.
    /// </summary>
    public static async Task<List<Http3Frame>> ReadAllFramesAsync(Stream stream)
    {
        Http3FrameReader reader = new(stream, 1024);
        List<Http3Frame> frames = [];
        while (await reader.ReadFrameAsync(CancellationToken.None) is { } frame)
        {
            frames.Add(frame);
        }

        return frames;
    }
}
