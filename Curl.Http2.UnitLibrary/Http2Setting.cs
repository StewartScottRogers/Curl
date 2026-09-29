namespace Curl.Http2;

/// <summary>
/// One SETTINGS parameter as sent on the wire (RFC 9113 section 6.5.1).
/// </summary>
/// <param name="Identifier">The parameter.</param>
/// <param name="Value">Its 32-bit value.</param>
public readonly record struct Http2Setting(Http2SettingIdentifier Identifier, uint Value);
