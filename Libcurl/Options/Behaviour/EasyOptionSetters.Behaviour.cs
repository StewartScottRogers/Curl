using System.Text;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.Share;
using Curl.Api.Options;
using Curl.Api.Options.Behaviour;

namespace Libcurl.Options;

/// <summary>The <c>EasyBehaviourOptions</c> rows of <see cref="EasyOptionSetters"/> (ADR-0560).</summary>
internal static partial class EasyOptionSetters
{
    /// <summary>The largest permission <c>CURLOPT_NEW_FILE_PERMS</c> and <c>_DIRECTORY_PERMS</c> take: 0777.</summary>
    private const long MaximumPermissions = 0x1FF;

    static partial void AddBehaviourSetters(Dictionary<CurlOption, EasyOptionSetter> setters)
    {
        AddOutputFlagSetters(setters);
        AddTransferFlagSetters(setters);
        AddReferenceSetters(setters);
        AddPermissionSetters(setters);
    }

    private static void AddOutputFlagSetters(Dictionary<CurlOption, EasyOptionSetter> setters)
    {
        setters[CurlOption.Verbose] = Flag(static (b, on) => b with { Verbose = on });
        setters[CurlOption.Header] = Flag(static (b, on) => b with { IncludeHeader = on });
        setters[CurlOption.Noprogress] = Flag(static (b, on) => b with { NoProgress = on });
        setters[CurlOption.Nosignal] = Flag(static (b, on) => b with { NoSignal = on });
    }

    private static void AddTransferFlagSetters(Dictionary<CurlOption, EasyOptionSetter> setters)
    {
        setters[CurlOption.Wildcardmatch] = Flag(static (b, on) => b with { WildcardMatch = on });
        setters[CurlOption.Failonerror] = Flag(static (b, on) => b with { FailOnError = on });
        setters[CurlOption.KeepSendingOnError] = Flag(static (b, on) => b with { KeepSendingOnError = on });
        setters[CurlOption.QuickExit] = Flag(static (b, on) => b with { QuickExit = on });
    }

    private static void AddReferenceSetters(Dictionary<CurlOption, EasyOptionSetter> setters)
    {
        setters[CurlOption.Errorbuffer] = Reference<StringBuilder>(static (b, buffer) => b with { ErrorBuffer = buffer });
        setters[CurlOption.Stderr] = Reference<Stream>(static (b, stream) => b with { StandardError = stream });
        setters[CurlOption.Share] = Reference<IShareHandle>(static (b, share) => b with { Share = share });
        setters[CurlOption.Private] = static (ref EasyOptions options, object? value) =>
            SetBehaviour(ref options, options.Behaviour with { Private = value });
    }

    private static void AddPermissionSetters(Dictionary<CurlOption, EasyOptionSetter> setters)
    {
        setters[CurlOption.NewFilePerms] = Permissions(static (b, mode) => b with { NewFilePermissions = mode });
        setters[CurlOption.NewDirectoryPerms] = Permissions(static (b, mode) => b with { NewDirectoryPermissions = mode });
    }

    /// <summary>A <c>CURLOT_LONG</c> on/off row: any value but 0 switches it on, as in libcurl.</summary>
    /// <param name="set">Copies the behaviour options with the flag set.</param>
    /// <returns>The row.</returns>
    private static EasyOptionSetter Flag(Func<EasyBehaviourOptions, bool, EasyBehaviourOptions> set) =>
        (ref EasyOptions options, object? value) => SetBehaviour(ref options, set(options.Behaviour, Convert.ToInt64(value) != 0));

    /// <summary>A <c>CURLOT_OBJECTPOINT</c> row taking a <typeparamref name="T"/> or <see langword="null"/>.</summary>
    /// <typeparam name="T">The .NET type the field holds.</typeparam>
    /// <param name="set">Copies the behaviour options with the field set.</param>
    /// <returns>The row; it answers <see cref="CurlCode.BadFunctionArgument"/> for any other value.</returns>
    private static EasyOptionSetter Reference<T>(Func<EasyBehaviourOptions, T?, EasyBehaviourOptions> set)
        where T : class =>
        (ref EasyOptions options, object? value) => value is null or T
            ? SetBehaviour(ref options, set(options.Behaviour, (T?)value))
            : CurlCode.BadFunctionArgument;

    /// <summary>A permissions row: 0 to 0777, else <see cref="CurlCode.BadFunctionArgument"/>, as in libcurl.</summary>
    /// <param name="set">Copies the behaviour options with the permissions set.</param>
    /// <returns>The row.</returns>
    private static EasyOptionSetter Permissions(Func<EasyBehaviourOptions, UnixFileMode, EasyBehaviourOptions> set) =>
        (ref EasyOptions options, object? value) =>
        {
            var mode = Convert.ToInt64(value);
            return mode is < 0 or > MaximumPermissions
                ? CurlCode.BadFunctionArgument
                : SetBehaviour(ref options, set(options.Behaviour, (UnixFileMode)mode));
        };

    private static CurlCode SetBehaviour(ref EasyOptions options, EasyBehaviourOptions behaviour)
    {
        options = options with { Behaviour = behaviour };
        return CurlCode.Ok;
    }
}
