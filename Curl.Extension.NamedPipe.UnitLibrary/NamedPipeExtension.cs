using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Connection;
using Curl.Extension.Abstractions.Description;

namespace Curl.Extension.NamedPipe;

/// <summary>The <c>--npipe</c> extension's option, description and connection hook activator (ADR-1021).</summary>
public static class NamedPipeExtension
{
    /// <summary>Gets <c>--npipe &lt;\\.\pipe\name&gt;</c>.</summary>
    public static ExtensionOption Option { get; } = new("npipe", ExtensionOptionArgument.Value, @"Connect through this Windows named pipe, such as \\.\pipe\name");

    /// <summary>Gets what <c>curl --extensions-help</c> says about the extension.</summary>
    public static ExtensionDescription Description { get; } = new(
        "npipe",
        "Runs every transfer over a Windows named pipe, the Windows counterpart of --unix-socket.",
        [],
        [Option],
        new Uri("https://learn.microsoft.com/windows/win32/ipc/pipe-names"));

    /// <summary>Gives the connection hook for <paramref name="values" />, opening pipes with <see cref="SystemNamedPipeOpener" />.</summary>
    /// <param name="values">The option group's extension options.</param>
    /// <returns>The hook, or <see langword="null" /> without <c>--npipe</c>.</returns>
    /// <exception cref="ArgumentException">The <c>--npipe</c> value is not a pipe name.</exception>
    public static IConnectionHook? Activate(ExtensionOptionValues values) => Activate(values, new SystemNamedPipeOpener());

    /// <summary>Gives the connection hook for <paramref name="values" />.</summary>
    /// <param name="values">The option group's extension options.</param>
    /// <param name="opener">Opens the pipe.</param>
    /// <returns>The hook, or <see langword="null" /> without <c>--npipe</c>.</returns>
    /// <exception cref="ArgumentException">The <c>--npipe</c> value is not a pipe name.</exception>
    internal static IConnectionHook? Activate(ExtensionOptionValues values, INamedPipeOpener opener)
    {
        ArgumentNullException.ThrowIfNull(values);
        string? value = values.ValueOf(Option);
        if (value is null)
        {
            return null;
        }

        return NamedPipeName.TryParse(value, out NamedPipeName? name)
            ? new NamedPipeConnectionHook(name!, opener)
            : throw new ArgumentException($@"--npipe needs a named pipe such as \\.\pipe\name, not '{value}'", nameof(values));
    }
}
