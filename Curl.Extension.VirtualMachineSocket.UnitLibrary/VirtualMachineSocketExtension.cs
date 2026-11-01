using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Connection;
using Curl.Extension.Abstractions.Description;

namespace Curl.Extension.VirtualMachineSocket;

/// <summary>The <c>--vm-socket</c> extension's option, description and connection hook activator (ADR-1109).</summary>
public static class VirtualMachineSocketExtension
{
    /// <summary>Gets <c>--vm-socket &lt;cid:port | vm:service&gt;</c>.</summary>
    public static ExtensionOption Option { get; } = new("vm-socket", ExtensionOptionArgument.Value, "Connect through this vsock CID:port or Hyper-V socket VM:service GUID pair");

    /// <summary>Gets what <c>curl --extensions-help</c> says about the extension.</summary>
    public static ExtensionDescription Description { get; } = new(
        "vm-socket",
        "Runs every transfer over a vsock (CID:port) or Hyper-V socket (VM:service GUID), between a virtual machine and its host.",
        [],
        [Option],
        new Uri("https://man7.org/linux/man-pages/man7/vsock.7.html"));

    /// <summary>Gives the connection hook for <paramref name="values" />, opening sockets with <see cref="SystemVirtualMachineSocketOpener" />.</summary>
    /// <param name="values">The option group's extension options.</param>
    /// <returns>The hook, or <see langword="null" /> without <c>--vm-socket</c>.</returns>
    /// <exception cref="ArgumentException">The <c>--vm-socket</c> value is not an address.</exception>
    public static IConnectionHook? Activate(ExtensionOptionValues values) => Activate(values, new SystemVirtualMachineSocketOpener());

    /// <summary>Gives the connection hook for <paramref name="values" />.</summary>
    /// <param name="values">The option group's extension options.</param>
    /// <param name="opener">Opens the socket.</param>
    /// <returns>The hook, or <see langword="null" /> without <c>--vm-socket</c>.</returns>
    /// <exception cref="ArgumentException">The <c>--vm-socket</c> value is not an address.</exception>
    internal static IConnectionHook? Activate(ExtensionOptionValues values, IVirtualMachineSocketOpener opener)
    {
        ArgumentNullException.ThrowIfNull(values);
        string? value = values.ValueOf(Option);
        if (value is null)
        {
            return null;
        }

        return VirtualMachineSocketAddress.TryParse(value, out VirtualMachineSocketAddress? address)
            ? new VirtualMachineSocketConnectionHook(address!, opener)
            : throw new ArgumentException($"--vm-socket needs a vsock CID:port or a Hyper-V VM:service GUID pair, not '{value}'", nameof(values));
    }
}
