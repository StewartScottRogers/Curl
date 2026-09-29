namespace Curl.Kerberos;

/// <summary>What a Wrap token held.</summary>
/// <param name="Message">The message.</param>
/// <param name="Encrypted">Whether the token encrypted it, or only protected its integrity.</param>
public sealed record KerberosGssUnwrapped(byte[] Message, bool Encrypted);
