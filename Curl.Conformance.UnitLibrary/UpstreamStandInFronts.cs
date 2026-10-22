using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>The runner's registration table of stand-in fronts, in the order each wraps within its stage.</summary>
internal sealed class UpstreamStandInFronts
{
    private readonly IReadOnlyList<UpstreamStandInFront> fronts;

    private UpstreamStandInFronts(IReadOnlyList<UpstreamStandInFront> fronts) => this.fronts = fronts;

    /// <summary>Builds the table: add a new front's line here.</summary>
    /// <param name="sshServer">The SSH stand-in, or <see langword="null"/> for none.</param>
    /// <param name="http2Server">The HTTP/2 stand-in, or <see langword="null"/> for none.</param>
    /// <param name="http3Server">The HTTP/3 stand-in, or <see langword="null"/> for none.</param>
    /// <returns>The table.</returns>
    public static UpstreamStandInFronts Create(UpstreamSshServer? sshServer, UpstreamHttp2Server? http2Server, UpstreamHttp3Server? http3Server)
    {
        List<UpstreamStandInFront> fronts =
        [
            new UpstreamHttpsFront(),
            new UpstreamHttpsMtlsFront(),
            new UpstreamFtpsFront(),
            new UpstreamMailTlsFront(),
            new UpstreamMqttsFront(),
            new UpstreamGophersFront(),
            new UpstreamSmbFront(),
            new UpstreamHttp2Front(http2Server),
            new UpstreamHttpsProxyFront(),
        ];
        if (sshServer is not null)
        {
            fronts.Insert(fronts.Count - 2, new UpstreamSshFront(sshServer));
        }

        if (http3Server is not null)
        {
            fronts.Insert(fronts.Count - 2, new UpstreamHttp3Front(http3Server));
        }

        // Outside every other front, so it splits the writes curl makes, whichever stand-in they reach.
        fronts.Add(new UpstreamSmallSendsFront());
        return new UpstreamStandInFronts([.. fronts]);
    }

    /// <summary>Adds every front's features to the platform.</summary>
    /// <param name="platform">The platform Curl runs on.</param>
    /// <returns>The platform with the features the fronts stand in for.</returns>
    public UpstreamCurlPlatform WithFeatures(UpstreamCurlPlatform platform) =>
        fronts.Aggregate(platform, (current, front) => front.WithFeatures(current));

    /// <summary>Adds every front's variables.</summary>
    /// <param name="variables">The run's variables.</param>
    /// <param name="certificateDirectory">The certificate directory the caller names, or <see langword="null"/>.</param>
    public void AddVariables(Dictionary<string, string> variables, string? certificateDirectory)
    {
        foreach (UpstreamStandInFront front in fronts)
        {
            front.AddVariables(variables, certificateDirectory);
        }
    }

    /// <summary>Lets every front write its files into the log directory.</summary>
    /// <param name="logDirectory">The case's log directory.</param>
    public void PrepareLogDirectory(string logDirectory)
    {
        foreach (UpstreamStandInFront front in fronts)
        {
            front.PrepareLogDirectory(logDirectory);
        }
    }

    /// <summary>Gathers the uploads the fronts' servers took.</summary>
    /// <param name="logDirectory">The case's log directory.</param>
    /// <param name="testNumber">The case's number.</param>
    /// <returns>The uploaded bytes.</returns>
    public IEnumerable<byte> UploadedBytes(string logDirectory, int testNumber) =>
        fronts.SelectMany(front => front.UploadedBytes(logDirectory, testNumber));

    /// <summary>Wraps <paramref name="inner"/> in the fronts of one stage, in table order.</summary>
    /// <param name="stage">The stage whose fronts apply.</param>
    /// <param name="inner">The connector built so far.</param>
    /// <param name="context">The case.</param>
    /// <returns>The connector with the stage's fronts around it.</returns>
    public IConnector Wrap(UpstreamStandInStage stage, IConnector inner, UpstreamStandInContext context) =>
        fronts.Where(front => front.Stage == stage).Aggregate(inner, (current, front) => front.Wrap(current, context));

    /// <summary>Wraps the datagram connector in every front's datagram stand-in.</summary>
    /// <param name="inner">The connector built so far.</param>
    /// <param name="context">The case.</param>
    /// <returns>The connector with the fronts around it.</returns>
    public IDatagramConnector WrapDatagram(IDatagramConnector inner, UpstreamStandInContext context) =>
        fronts.Aggregate(inner, (current, front) => front.WrapDatagram(current, context));
}
