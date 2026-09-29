# Curl.Protocol.Smb.UnitLibrary

Phase 5.

SMB/CIFS file access, speaking curl 8.21.0's SMBv1 (`NT LM 0.12`) on every platform
(ADR-0200).

**URL schemes:** `smb`, `smbs`

`SmbProtocolHandler` so far opens the session (BL-595): `SmbUrlPath` splits the share
from the URL's path, `SmbSessionEstablisher` sends `SmbNegotiateRequest`, reads
`SmbNegotiateResponse`'s challenge and answers it with `SmbSessionSetupRequest`, whose
NTLMv1 LM and NT responses come from `Curl.Ntlm`'s `NtlmResponseComputation.ComputeV1`.
`SmbMessageReader` frames every reply as curl's `smb_recv_message` does. Connecting to
the share and reading or writing the file are BL-596 and BL-597.

This library may reference `Curl.Protocol.Abstractions.UnitLibrary` and, of the
hand-built libraries ADR-0120 lists, `Curl.Ntlm.UnitLibrary`; nothing else horizontal.
Referencing another protocol library is a build break, and
`Curl.Protocol.Abstractions.UnitTests` fails if one appears.

Never construct a `Socket`, `SslStream` or `HttpClient` here. Take `IConnection`
so the tests in the matching `.UnitTests` project can drive this code from a
recorded byte stream with no network. `Curl.Protocol.Smb.UnitTests`'
`SmbRecordedExchange` holds the bytes measured from Linux curl on 2026-09-29.
