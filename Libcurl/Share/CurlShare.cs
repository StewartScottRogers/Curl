using Curl.Api.Codes;
using Curl.Api.EngineContracts.Share;

namespace Libcurl.Share;

/// <summary>
/// A share handle (ADR-0506): <c>curl_share_init</c>'s result, created by
/// <see cref="Global.CurlLibrary.ShareInit"/> over the engine's <see cref="IShareHandle"/>.
/// <see cref="SetOption"/> is <c>curl_share_setopt</c> and <see cref="Cleanup"/> is
/// <c>curl_share_cleanup</c>. Setting <c>CURLOPT_SHARE</c> to it on a <see cref="Easy.CurlEasy"/>
/// attaches the easy handle, and setting <c>CURLOPT_SHARE</c> to <see langword="null"/> detaches it.
/// </summary>
/// <remarks>
/// The lock callbacks are the seam's own <see cref="ShareLockCallback"/> and
/// <see cref="ShareUnlockCallback"/>, handed to the engine as given; the engine calls them with its
/// transfer, so a callback that needs state carries it in its closure rather than in
/// <see cref="UserData"/> (ADR-0581).
/// </remarks>
public sealed class CurlShare
{
    private readonly IShareHandle handle;
    private int cleanedUp;

    /// <summary>Wraps the engine's share <paramref name="handle"/>.</summary>
    /// <param name="handle">The engine's share handle; <see cref="Cleanup"/> frees it.</param>
    internal CurlShare(IShareHandle handle)
    {
        this.handle = handle;
    }

    /// <summary>Whether <see cref="Cleanup"/> (<c>curl_share_cleanup</c>) has freed the handle.</summary>
    public bool IsCleanedUp => Volatile.Read(ref cleanedUp) != 0;

    /// <summary>The value <c>CURLSHOPT_USERDATA</c> last set, or <see langword="null"/>.</summary>
    public object? UserData { get; private set; }

    /// <summary>
    /// The engine's handle, which <c>CURLOPT_SHARE</c> attaches; <see langword="null"/> once the handle
    /// is cleaned up, so setting <c>CURLOPT_SHARE</c> to a freed share detaches, as libcurl's
    /// <c>GOOD_SHARE_HANDLE</c> check does.
    /// </summary>
    internal IShareHandle? EngineHandle => IsCleanedUp ? null : handle;

    /// <summary><c>curl_share_setopt</c>: sets <paramref name="option"/> to <paramref name="value"/>.</summary>
    /// <param name="option">The option to set.</param>
    /// <param name="value">
    /// A <see cref="CurlLockData"/> (or its number) for <see cref="CurlShareOption.Share"/> and
    /// <see cref="CurlShareOption.Unshare"/>; a <see cref="ShareLockCallback"/> or <see cref="ShareUnlockCallback"/>
    /// (or <see langword="null"/>) for the lock functions; anything for <see cref="CurlShareOption.UserData"/>.
    /// </param>
    /// <returns>
    /// <see cref="CurlShareCode.Invalid"/> once cleaned up; <see cref="CurlShareCode.BadOption"/> for an
    /// unknown option, a value of the wrong type, or a lock data kind that cannot be shared
    /// (<see cref="CurlLockData.None"/>, <see cref="CurlLockData.Share"/> or one <c>curl.h</c> lacks), all
    /// without calling the engine; else the engine's answer.
    /// </returns>
    public CurlShareCode SetOption(CurlShareOption option, object? value)
    {
        if (IsCleanedUp)
        {
            return CurlShareCode.Invalid;
        }

        return option switch
        {
            CurlShareOption.Share or CurlShareOption.Unshare => SetLockData(option, value),
            CurlShareOption.LockFunction => SetLockFunction(value),
            CurlShareOption.UnlockFunction => SetUnlockFunction(value),
            CurlShareOption.UserData => SetUserData(value),
            _ => CurlShareCode.BadOption,
        };
    }

    /// <summary>
    /// <c>curl_share_cleanup</c>: asks the engine to free the handle. On <see cref="CurlShareCode.Ok"/>
    /// the handle is cleaned up; on any other answer (<see cref="CurlShareCode.InUse"/> while an easy
    /// handle is attached) it stays usable.
    /// </summary>
    /// <returns><see cref="CurlShareCode.Invalid"/> once cleaned up, else the engine's answer.</returns>
    public CurlShareCode Cleanup()
    {
        if (IsCleanedUp)
        {
            return CurlShareCode.Invalid;
        }

        var code = handle.Cleanup();
        if (code == CurlShareCode.Ok)
        {
            Volatile.Write(ref cleanedUp, 1);
        }

        return code;
    }

    private static bool TryReadLockData(object? value, out CurlLockData data)
    {
        data = value switch
        {
            CurlLockData kind => kind,
            int number => (CurlLockData)number,
            long number => (CurlLockData)number,
            _ => CurlLockData.None,
        };
        return data is >= CurlLockData.Cookie and <= CurlLockData.Hsts;
    }

    private CurlShareCode SetLockData(CurlShareOption option, object? value)
    {
        if (!TryReadLockData(value, out var data))
        {
            return CurlShareCode.BadOption;
        }

        return option == CurlShareOption.Share ? handle.Share(data) : handle.Unshare(data);
    }

    private CurlShareCode SetLockFunction(object? value) => value is null or ShareLockCallback
        ? handle.SetLockCallback((ShareLockCallback?)value)
        : CurlShareCode.BadOption;

    private CurlShareCode SetUnlockFunction(object? value) => value is null or ShareUnlockCallback
        ? handle.SetUnlockCallback((ShareUnlockCallback?)value)
        : CurlShareCode.BadOption;

    private CurlShareCode SetUserData(object? value)
    {
        UserData = value;
        return CurlShareCode.Ok;
    }
}
