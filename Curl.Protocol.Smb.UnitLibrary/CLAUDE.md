# Curl.Protocol.Smb.UnitLibrary

Phase 5.

SMB/CIFS file access, speaking curl 8.21.0's SMBv1 (`NT LM 0.12`) on every platform
(ADR-0200).

**URL schemes:** `smb`, `smbs`

`SmbProtocolHandler` downloads a file. `SmbUrlPath` splits the share from the URL's
path; `SmbSessionEstablisher` (BL-595) sends `SmbNegotiateRequest`, reads
`SmbNegotiateResponse`'s challenge and answers it with `SmbSessionSetupRequest`, whose
NTLMv1 LM and NT responses come from `Curl.Ntlm`'s `NtlmResponseComputation.ComputeV1`;
then `SmbFileDownloader` (BL-596) sends `SmbTreeConnectRequest`, `SmbOpenRequest`
(reading `SmbOpenResponse`), `SmbReadRequest` until a short `SmbReadResponse`,
`SmbCloseRequest` and `SmbTreeDisconnectRequest`, each framed by
`SmbMessageHeader.Frame`. `SmbMessageReader` frames every reply as curl's
`smb_recv_message` does. Writing the file with `-T` is BL-597.

This library may reference `Curl.Protocol.Abstractions.UnitLibrary` and, of the
hand-built libraries ADR-0120 lists, `Curl.Ntlm.UnitLibrary`; nothing else horizontal.
Referencing another protocol library is a build break, and
`Curl.Protocol.Abstractions.UnitTests` fails if one appears.

Never construct a `Socket`, `SslStream` or `HttpClient` here. Take `IConnection`
so the tests in the matching `.UnitTests` project can drive this code from a
recorded byte stream with no network. `Curl.Protocol.Smb.UnitTests`'
`SmbRecordedExchange` holds the bytes measured from Linux curl on 2026-09-29, for the
session and for the download.
