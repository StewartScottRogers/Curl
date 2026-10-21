using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// One stand-in that stands in front of, or beside, the runner's plain in-memory servers: a TLS front, the SSH, HTTP/2 or
/// HTTP/3 server. It adds its own variables, wraps the connector built so far, and prepares or reads back its own files,
/// so a new one is a new file plus one line in <see cref="UpstreamStandInFronts"/>.
/// </summary>
internal abstract class UpstreamStandInFront
{
    /// <summary>Gets where in the chain of servers this front wraps.</summary>
    public abstract UpstreamStandInStage Stage { get; }

    /// <summary>Adds the features the harness stands in for.</summary>
    /// <param name="platform">The platform Curl runs on.</param>
    /// <returns>The platform, with this front's features added when it stands in for one.</returns>
    public virtual UpstreamCurlPlatform WithFeatures(UpstreamCurlPlatform platform) => platform;

    /// <summary>Adds the <c>%NAME</c> variables this front gives a value.</summary>
    /// <param name="variables">The run's variables.</param>
    /// <param name="certificateDirectory">The certificate directory the caller names, or <see langword="null"/>.</param>
    public abstract void AddVariables(Dictionary<string, string> variables, string? certificateDirectory);

    /// <summary>Writes the files this front's server expects in the log directory before curl runs.</summary>
    /// <param name="logDirectory">The case's log directory.</param>
    public virtual void PrepareLogDirectory(string logDirectory)
    {
    }

    /// <summary>Reads the bytes this front's server received as an upload, for <c>&lt;verify&gt;&lt;upload&gt;</c>.</summary>
    /// <param name="logDirectory">The case's log directory.</param>
    /// <param name="testNumber">The case's number.</param>
    /// <returns>The upload, or nothing when this front takes none.</returns>
    public virtual byte[] UploadedBytes(string logDirectory, int testNumber) => [];

    /// <summary>Wraps the connector built so far at this front's <see cref="Stage"/>.</summary>
    /// <param name="inner">The connector to stand in front of.</param>
    /// <param name="context">The case.</param>
    /// <returns>The front over <paramref name="inner"/>, or <paramref name="inner"/> when the case does not use this front.</returns>
    public abstract IConnector Wrap(IConnector inner, UpstreamStandInContext context);

    /// <summary>Wraps the datagram connector built so far.</summary>
    /// <param name="inner">The connector to stand in front of.</param>
    /// <param name="context">The case.</param>
    /// <returns>The front over <paramref name="inner"/>, or <paramref name="inner"/> when the case does not use this front.</returns>
    public virtual IDatagramConnector WrapDatagram(IDatagramConnector inner, UpstreamStandInContext context) => inner;
}
