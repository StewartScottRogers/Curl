using System.Globalization;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Share;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Info;
using Libcurl.Misc.DatesAndLists;
using Libcurl.Options;
using Libcurl.Share;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib506.c</c>: a share handle sharing cookies and DNS, whose lock and unlock callbacks write each lock to
/// <c>stdout</c> with a running count per kind (<c>lock:   cookie [Pigs in space]: 0</c>); cookies injected,
/// cleared and flushed through <c>CURLOPT_COOKIELIST</c>; two transfers of <c>URL0001</c> and <c>URL0002</c>
/// over the share; a third that writes the jar the second argument names; and a fourth handle that reloads it and
/// lists the cookies <c>CURLINFO_COOKIELIST</c> gives, then tries a <c>curl_share_cleanup</c> while the handle
/// still uses the share. Every step is announced on <c>stdout</c> as upstream announces it. The
/// <c>easy_setopt</c> answers are not checked, as Libcurl cannot refuse these values, and neither is
/// <c>CURLINFO_COOKIELIST</c>'s, which Libcurl always reads.
/// </summary>
public sealed class Lib506 : ILibTest
{
    private const string HostHeader = "Host: www.host.foo.com";

    /// <inheritdoc/>
    public string ProgramName => "lib506";

    /// <inheritdoc/>
    public async Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        await using StreamWriter output = LibTestRun.StandardOutputWriter(context);
        await output.WriteLineAsync("GLOBAL_INIT").ConfigureAwait(false);
        return await LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, output, cancellationToken)).ConfigureAwait(false);
    }

    /// <summary>The program's body after <c>curl_global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="output">The program's <c>stdout</c>.</param>
    /// <param name="cancellationToken">Stops the transfers.</param>
    /// <returns>A task that completes when the program reaches <c>curl_global_cleanup</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, TextWriter output, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(output);
        TextWriter error = context.StandardError;
        output.WriteLine("SHARE_INIT");
        if (!steps.ShareInit(library, out CurlShare? share))
        {
            return;
        }

        if (!SetUpShare(share, new LockRecorder(output, error)))
        {
            error.Write("curl_share_setopt() failed\n");
            share.Cleanup();
            steps.Result = LibTestErrorCodes.MajorBad;
            return;
        }

        if (await InjectCookiesAsync(steps, library, share, output).ConfigureAwait(false)
            && await FireAllAsync(context, steps, library, share, output, cancellationToken).ConfigureAwait(false))
        {
            output.WriteLine("SHARE_CLEANUP");
            CurlShareCode code = share.Cleanup();
            if (code != CurlShareCode.Ok)
            {
                error.Write($"curl_share_cleanup failed, code errno {(int)code}\n");
            }

            output.WriteLine("GLOBAL_CLEANUP");
        }
    }

    // Each share option announced, stopping at the first refused.
    private static bool SetUpShare(CurlShare share, LockRecorder recorder)
    {
        (string Announcement, CurlShareOption Option, object Value)[] options =
        [
            ("CURLSHOPT_LOCKFUNC", CurlShareOption.LockFunction, (ShareLockCallback)recorder.Lock),
            ("CURLSHOPT_UNLOCKFUNC", CurlShareOption.UnlockFunction, (ShareUnlockCallback)recorder.Unlock),
            ("CURLSHOPT_USERDATA", CurlShareOption.UserData, recorder),
            ("CURL_LOCK_DATA_COOKIE", CurlShareOption.Share, CurlLockData.Cookie),
            ("CURL_LOCK_DATA_DNS", CurlShareOption.Share, CurlLockData.Dns),
        ];
        return options.All(option =>
        {
            recorder.Output.WriteLine(option.Announcement);
            return share.SetOption(option.Option, option.Value) == CurlShareCode.Ok;
        });
    }

    // The initial cookie manipulation on a handle of its own; a failed curl_easy_init cleans up the share.
    private static async ValueTask<bool> InjectCookiesAsync(LibTestSteps steps, CurlLibrary library, CurlShare share, TextWriter output)
    {
        if (!steps.EasyInitOrMajorBad(library, out CurlEasy? easy))
        {
            share.Cleanup();
            return false;
        }

        await using (easy.ConfigureAwait(false))
        {
            SetAnnounced(steps, easy, output, "CURLOPT_SHARE", CurlOption.Share, share);
            SetAnnounced(steps, easy, output, "CURLOPT_COOKIELIST injected_and_clobbered", CurlOption.Cookielist, "Set-Cookie: injected_and_clobbered=yes; domain=host.foo.com; expires=Sat Feb 2 11:56:27 GMT 2030");
            SetAnnounced(steps, easy, output, "CURLOPT_COOKIELIST ALL", CurlOption.Cookielist, "ALL");
            SetAnnounced(steps, easy, output, "CURLOPT_COOKIELIST session", CurlOption.Cookielist, "Set-Cookie: session=elephants");
            SetAnnounced(steps, easy, output, "CURLOPT_COOKIELIST injected", CurlOption.Cookielist, "Set-Cookie: injected=yes; domain=host.foo.com; expires=Sat Feb 2 11:56:27 GMT 2030");
            SetAnnounced(steps, easy, output, "CURLOPT_COOKIELIST SESS", CurlOption.Cookielist, "SESS");
            output.WriteLine("CLEANUP");
        }

        return true;
    }

    // The two "threads", the jar-writing transfer and the reload; false when a curl_easy_init failed.
    private static async ValueTask<bool> FireAllAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, CurlShare share, TextWriter output, CancellationToken cancellationToken)
    {
        for (int run = 1; run <= 2; run++)
        {
            output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"*** run {run}"));
            await FireAsync(context, library, share, LibTestUtilities.SubUrl(context.Url, run), output, cancellationToken).ConfigureAwait(false);
        }

        output.WriteLine("*** run 3");
        string url = LibTestUtilities.SubUrl(context.Url, 3);
        return await WriteJarAsync(context, steps, library, share, url, output, cancellationToken).ConfigureAwait(false)
            && await ReloadJarAsync(context, steps, library, share, url, output).ConfigureAwait(false);
    }

    // t506_test_fire: one transfer over the share with an empty cookie file; a failure is written to stderr.
    private static async ValueTask FireAsync(LibTestContext context, CurlLibrary library, CurlShare share, string url, TextWriter output, CancellationToken cancellationToken)
    {
        await using CurlEasy easy = library.EasyInit();
        CurlSlist headers = CurlSlist.Append(null, HostHeader);
        easy.SetOption(CurlOption.Writedata, context.StandardOutput);
        easy.SetOption(CurlOption.Verbose, 1L);
        easy.SetOption(CurlOption.Httpheader, headers);
        easy.SetOption(CurlOption.Url, url);
        easy.SetOption(CurlOption.Cookiefile, string.Empty);
        output.WriteLine("CURLOPT_SHARE");
        easy.SetOption(CurlOption.Share, share);
        output.WriteLine("PERFORM");
        CurlCode result = await easy.PerformAsync(cancellationToken).ConfigureAwait(false);
        if (result != CurlCode.Ok)
        {
            context.StandardError.Write(string.Create(CultureInfo.InvariantCulture, $"perform URL '{url}' repeat 0 failed, curlcode {(int)result}\n"));
        }

        output.WriteLine("CLEANUP");
    }

    private static async ValueTask<bool> WriteJarAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, CurlShare share, string url, TextWriter output, CancellationToken cancellationToken)
    {
        if (!steps.EasyInitOrMajorBad(library, out CurlEasy? easy))
        {
            share.Cleanup();
            return false;
        }

        await using (easy.ConfigureAwait(false))
        {
            steps.EasySetOption(easy, CurlOption.Writedata, context.StandardOutput);
            steps.EasySetOption(easy, CurlOption.Httpheader, CurlSlist.Append(null, HostHeader));
            steps.EasySetOption(easy, CurlOption.Url, url);
            SetAnnounced(steps, easy, output, "CURLOPT_SHARE", CurlOption.Share, share);
            SetAnnounced(steps, easy, output, "CURLOPT_COOKIEJAR", CurlOption.Cookiejar, context.Argument2);
            SetAnnounced(steps, easy, output, "CURLOPT_COOKIELIST FLUSH", CurlOption.Cookielist, "FLUSH");
            output.WriteLine("PERFORM");
            await easy.PerformAsync(cancellationToken).ConfigureAwait(false);
            output.WriteLine("CLEANUP");
        }

        return true;
    }

    // Loads the jar on a fresh handle and lists its cookies, then tries to free the share it still uses.
    private static async ValueTask<bool> ReloadJarAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, CurlShare share, string url, TextWriter output)
    {
        if (!steps.EasyInitOrMajorBad(library, out CurlEasy? easy))
        {
            share.Cleanup();
            return false;
        }

        await using (easy.ConfigureAwait(false))
        {
            steps.EasySetOption(easy, CurlOption.Writedata, context.StandardOutput);
            steps.EasySetOption(easy, CurlOption.Httpheader, CurlSlist.Append(null, HostHeader));
            steps.EasySetOption(easy, CurlOption.Url, url);
            SetAnnounced(steps, easy, output, "CURLOPT_SHARE", CurlOption.Share, share);
            SetAnnounced(steps, easy, output, "CURLOPT_COOKIELIST ALL", CurlOption.Cookielist, "ALL");
            SetAnnounced(steps, easy, output, "CURLOPT_COOKIEJAR", CurlOption.Cookiefile, context.Argument2);
            SetAnnounced(steps, easy, output, "CURLOPT_COOKIELIST RELOAD", CurlOption.Cookielist, "RELOAD");
            steps.EasyGetInfo(easy, CurlInfo.Cookielist, out object? cookies);
            output.WriteLine("loaded cookies:");
            ListCookiesAndTryShareCleanup(context, steps, share, (IReadOnlyList<string>)cookies!, output);
            output.WriteLine("CLEANUP");
        }

        return true;
    }

    private static void ListCookiesAndTryShareCleanup(LibTestContext context, LibTestSteps steps, CurlShare share, IReadOnlyList<string> cookies, TextWriter output)
    {
        if (cookies.Count == 0)
        {
            context.StandardError.Write($"  reloading cookies from '{context.Argument2}' failed\n");
            steps.Result = LibTestErrorCodes.MajorBad;
            return;
        }

        output.WriteLine("-----------------");
        foreach (string cookie in cookies)
        {
            output.WriteLine($"  {cookie}");
        }

        output.WriteLine("-----------------");
        output.WriteLine("try SHARE_CLEANUP...");
        if (share.Cleanup() == CurlShareCode.Ok)
        {
            context.StandardError.Write("curl_share_cleanup succeed but error expected\n");
        }
        else
        {
            output.WriteLine("SHARE_CLEANUP failed, correct");
        }
    }

    private static void SetAnnounced(LibTestSteps steps, CurlEasy easy, TextWriter output, string announcement, CurlOption option, object? value)
    {
        output.WriteLine(announcement);
        steps.EasySetOption(easy, option, value);
    }

    // t506_test_lock and t506_test_unlock with their t506_userdata: a lock flag and a running count per kind.
    private sealed class LockRecorder(TextWriter output, TextWriter error)
    {
        private const string Text = "Pigs in space";
        private readonly int[] locks = new int[3];
        private readonly int[] counters = new int[3];

        public TextWriter Output => output;

        public void Lock(IEasyTransfer? transfer, CurlLockData data, CurlLockAccess access)
        {
            if (IndexOf(data, "lock") is not { } index)
            {
                return;
            }

            if (locks[index] != 0)
            {
                output.WriteLine($"lock: double locked {NameOf(index)}");
                return;
            }

            locks[index]++;
            output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"lock:   {NameOf(index),-6} [{Text}]: {counters[index]++}"));
        }

        public void Unlock(IEasyTransfer? transfer, CurlLockData data)
        {
            if (IndexOf(data, "unlock") is not { } index)
            {
                return;
            }

            if (locks[index] == 0)
            {
                output.WriteLine($"unlock: double unlocked {NameOf(index)}");
                return;
            }

            locks[index]--;
            output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"unlock: {NameOf(index),-6} [{Text}]: {counters[index]++}"));
        }

        private static string NameOf(int index) => index switch
        {
            0 => "share",
            1 => "dns",
            _ => "cookie",
        };

        private int? IndexOf(CurlLockData data, string function)
        {
            switch (data)
            {
                case CurlLockData.Share:
                    return 0;
                case CurlLockData.Dns:
                    return 1;
                case CurlLockData.Cookie:
                    return 2;
                default:
                    error.Write(string.Create(CultureInfo.InvariantCulture, $"{function}: no such data: {(int)data}\n"));
                    return null;
            }
        }
    }
}
