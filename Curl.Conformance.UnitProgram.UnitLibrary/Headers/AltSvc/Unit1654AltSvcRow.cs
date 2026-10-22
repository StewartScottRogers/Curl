using Curl.Core.AltSvc;

namespace Curl.Conformance.UnitProgram.Headers.AltSvc;

/// <summary>One <c>Curl_altsvc_parse</c> call of <c>unit1654.c</c>: the origin, the header and, when the C program checks it, the entry count after it.</summary>
/// <param name="Host">The origin's host, <c>Curl_peer_create</c>'s.</param>
/// <param name="Port">The origin's port.</param>
/// <param name="SourceAlpn">The HTTP version the header came over.</param>
/// <param name="Header">The <c>Alt-Svc</c> header value, as the C program passes it.</param>
/// <param name="ExpectedCount">The entry count the C program checks after the call; <see langword="null" /> where it checks none.</param>
public sealed record Unit1654AltSvcRow(string Host, int Port, AltSvcAlpn SourceAlpn, string Header, int? ExpectedCount);
