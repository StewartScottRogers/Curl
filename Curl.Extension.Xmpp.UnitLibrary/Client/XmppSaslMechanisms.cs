using System.Net;

namespace Curl.Extension.Xmpp.Client;

/// <summary>
/// The SASL mechanisms every mechanism file has added from its <c>[ModuleInitializer]</c>: no central switch, so a new
/// mechanism is one new file.
/// </summary>
internal static class XmppSaslMechanisms
{
    private static readonly List<IXmppSaslMechanism> Registered = [];

    /// <summary>Gives the mechanisms that log in with <paramref name="credentials"/>, or without any when it is <see langword="null"/>, most preferred first.</summary>
    internal static IReadOnlyList<IXmppSaslMechanism> For(NetworkCredential? credentials) =>
        [.. Registered.Where(mechanism => mechanism.NeedsCredentials == credentials is not null).OrderByDescending(mechanism => mechanism.Preference)];

    /// <summary>Adds <paramref name="mechanism"/>.</summary>
    internal static void Register(IXmppSaslMechanism mechanism) => Registered.Add(mechanism);
}
