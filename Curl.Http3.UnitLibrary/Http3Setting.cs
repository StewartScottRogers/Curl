namespace Curl.Http3;

/// <summary>
/// One parameter of a <c>SETTINGS</c> frame (RFC 9114 section 7.2.4.1).
/// </summary>
/// <param name="Identifier">The setting's identifier; <see cref="Http3SettingIdentifier" /> names the ones curl uses.</param>
/// <param name="Value">The setting's value.</param>
public readonly record struct Http3Setting(long Identifier, long Value);
