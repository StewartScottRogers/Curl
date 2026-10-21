using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

// A case setting CURL_SMALLREQSEND or CURL_SMALLSENDS (363, 1294, 1295, 5028) has upstream's debug build split what
// curl sends: the first send of a request after that many bytes, or every send. Neither the curl tool nor Libcurl reads
// them, so the harness splits the writes of every connection the case makes, outside every other stand-in; the bytes
// the servers record are unchanged (BL-2832, ADR-0721).
internal sealed class UpstreamSmallSendsFront : UpstreamStandInFront
{
    public const string FirstWriteVariableName = "CURL_SMALLREQSEND";

    public const string EveryWriteVariableName = "CURL_SMALLSENDS";

    public override UpstreamStandInStage Stage => UpstreamStandInStage.Outermost;

    public override void AddVariables(Dictionary<string, string> variables, string? certificateDirectory)
    {
    }

    public override IConnector Wrap(IConnector inner, UpstreamStandInContext context)
    {
        Dictionary<string, string> environment = UpstreamCaseRunner.EnvironmentVariables(context.TestCase);
        int? firstWriteLimit = Limit(environment, FirstWriteVariableName);
        int? everyWriteLimit = Limit(environment, EveryWriteVariableName);
        return firstWriteLimit is null && everyWriteLimit is null ? inner : new SmallSendsConnector(inner, firstWriteLimit, everyWriteLimit);
    }

    // A positive whole number of bytes, as the debug build reads one; anything else sets no limit.
    private static int? Limit(Dictionary<string, string> environment, string name) =>
        environment.GetValueOrDefault(name) is { } value && int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int limit) && limit > 0
            ? limit
            : null;
}
