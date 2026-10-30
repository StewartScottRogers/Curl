using Curl.Extension.ObliviousHttp.Hpke;

namespace Curl.Extension.ObliviousHttp.Encapsulation;

/// <summary>The key configuration a client encapsulates to and the HPKE suite it chose from it.</summary>
/// <param name="Configuration">The gateway's key configuration.</param>
/// <param name="Suite">The configuration's KEM with one of its KDF and AEAD pairs.</param>
public sealed record KeyConfigurationChoice(KeyConfiguration Configuration, HpkeSuite Suite);
