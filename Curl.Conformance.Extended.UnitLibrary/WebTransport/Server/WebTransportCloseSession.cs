namespace Curl.Conformance.Extended.WebTransport.Server;

/// <summary>The WT_CLOSE_SESSION capsule a case has the server send: an application error code and a message.</summary>
/// <param name="Code">The 32-bit application error code.</param>
/// <param name="Message">The error message, sent as UTF-8.</param>
public sealed record WebTransportCloseSession(uint Code, string Message);
