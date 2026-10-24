using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.EngineContracts.Share;
using Curl.Conformance.Differential.Libcurl.Multi;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Info;
using Libcurl.Multi;
using Libcurl.Options;
using Libcurl.Share;

namespace Curl.Conformance.Differential.IntegrationTests.LibcurlMulti;

/// <summary>Runs a multi or share case's calls through Curl's Libcurl, as the tester's <c>LibTest</c> multi and share modes did (ADR-0793).</summary>
internal sealed class CurlLibcurlMultiTarget(CurlLibrary library) : ILibcurlMultiTarget
{
    private readonly CurlMulti _multi = library.MultiInit();
    private readonly CurlShare _share = library.ShareInit();
    private readonly List<CurlEasy> _easies = [];
    private readonly List<MemoryStream> _bodies = [];
    private readonly HashSet<int> _freed = [];
    private bool _multiCleaned;

    public IReadOnlyList<int> AddEasy(IReadOnlyList<LibcurlMultiValue> options)
    {
        var easy = library.EasyInit();
        var body = new MemoryStream();
        _easies.Add(easy);
        _bodies.Add(body);
        WriteCallback collect = data =>
        {
            body.Write(data);
            return data.Length;
        };
        easy.SetOption(CurlOption.Writefunction, collect);
        return [.. options.Select(option => (int)easy.SetOption((CurlOption)option.Id, option.Kind == "share" ? _share : Value(option)))];
    }

    public int AddHandle(int? easy) => (int)_multi.AddHandle(easy is { } index ? _easies[index] : null);

    public int RemoveHandle(int? easy) => (int)_multi.RemoveHandle(easy is { } index ? _easies[index] : null);

    public int SetMultiOption(LibcurlMultiValue setting) => (int)_multi.SetOption((CurlMultiOption)setting.Id, Value(setting));

    public async ValueTask<(int Code, int Running)> PerformAsync()
    {
        var (code, running) = await _multi.PerformAsync();
        return ((int)code, running);
    }

    public async ValueTask<(int Code, int Ready)> PollAsync(int timeoutMilliseconds)
    {
        var (code, ready) = await _multi.PollAsync(Memory<CurlWaitSocket>.Empty, timeoutMilliseconds);
        return ((int)code, ready);
    }

    public async ValueTask<(int Code, int Ready)> WaitAsync(int timeoutMilliseconds)
    {
        var (code, ready) = await _multi.WaitAsync(Memory<CurlWaitSocket>.Empty, timeoutMilliseconds);
        return ((int)code, ready);
    }

    public IReadOnlyList<LibcurlMultiMessage> ReadMessages()
    {
        var messages = new List<LibcurlMultiMessage>();
        while (_multi.InfoRead(out var queue) is { } message)
        {
            messages.Add(new((int)message.Type, _easies.IndexOf(message.Easy), (int)message.Result, queue));
        }

        return messages;
    }

    public (byte[] Body, long ResponseCode) Body(int easy) => (_bodies[easy].ToArray(), ResponseCode(_easies[easy]));

    public int HandleCount() => _multi.GetHandles().Count;

    public void CleanupEasy(int easy)
    {
        _freed.Add(easy);
        _easies[easy].Dispose();
    }

    public async ValueTask CleanupMultiAsync()
    {
        _multiCleaned = true;
        await _multi.DisposeAsync();
    }

    public int SetShareOption(LibcurlMultiValue setting) => (int)_share.SetOption((CurlShareOption)setting.Id, setting.Kind == "null" ? null : Value(setting));

    public (int Code, long ResponseCode, long Connects) PerformEasy(int easy)
    {
        var code = _easies[easy].Perform();
        _easies[easy].GetInfo(CurlInfo.NumConnects, out var connects);
        return ((int)code, ResponseCode(_easies[easy]), Convert.ToInt64(connects, System.Globalization.CultureInfo.InvariantCulture));
    }

    public int CleanupShare() => (int)_share.Cleanup();

    public async ValueTask DisposeAsync()
    {
        if (!_multiCleaned)
        {
            await _multi.DisposeAsync();
        }

        for (var index = 0; index < _easies.Count; index++)
        {
            if (!_freed.Contains(index))
            {
                _easies[index].Dispose();
            }
        }

        if (!_share.IsCleanedUp)
        {
            _share.Cleanup();
        }
    }

    private static object? Value(LibcurlMultiValue value) => value.Kind == "l" ? value.Number : value.Text;

    private static long ResponseCode(CurlEasy easy)
    {
        easy.GetInfo(CurlInfo.ResponseCode, out var code);
        return Convert.ToInt64(code, System.Globalization.CultureInfo.InvariantCulture);
    }
}
