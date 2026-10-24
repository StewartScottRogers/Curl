using Curl.Api.Codes;
using Curl.Api.EngineContracts.Url;
using Curl.Conformance.Differential.Libcurl.Scenarios;
using Libcurl.Global;
using Libcurl.Misc.DatesAndLists;
using Libcurl.Misc.ErrorText;
using Libcurl.Misc.Strings;
using Libcurl.Misc.Version;
using Libcurl.Url;

namespace Curl.Conformance.Differential.IntegrationTests.LibcurlScenarios;

/// <summary>Replays <c>scen.json</c> scenarios against Curl's own Libcurl, through its managed API as <c>LibTest</c> did (ADR-0791).</summary>
internal sealed class ManagedLibcurlScenarioTarget(CurlLibrary library) : ILibcurlScenarioTarget
{
    public string Version() => CurlVersion.Text(library);

    public string EasyErrorText(int code) => CurlErrorText.Easy((CurlCode)code);

    public string MultiErrorText(int code) => CurlErrorText.Multi((CurlMultiCode)code);

    public string ShareErrorText(int code) => CurlErrorText.Share((CurlShareCode)code);

    public string UrlErrorText(int code) => CurlUrlHandle.ErrorText((CurlUrlCode)code);

    public ILibcurlScenarioUrl CreateUrl() => new ManagedUrl(library.UrlInit());

    public string Escape(byte[] bytes) => CurlStrings.Escape(bytes);

    public byte[]? Unescape(byte[] bytes) => CurlStrings.Unescape(bytes);

    public long ParseDate(string text) => CurlDate.Parse(text);

    private sealed class ManagedUrl(CurlUrlHandle handle) : ILibcurlScenarioUrl
    {
        public int Set(int part, string? value, int flags) => (int)handle.Set((CurlUrlPart)part, value, (CurlUrlFlags)flags);

        public int Get(int part, int flags, out byte[]? value) =>
            (int)handle.GetBytes((CurlUrlPart)part, (CurlUrlFlags)flags, out value);

        public void Dispose() => handle.Dispose();
    }
}
