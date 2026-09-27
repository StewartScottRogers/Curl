# Curl.Protocol.Ftp.UnitLibrary

Phase 2.

FTP with a separate control and data channel. `FtpProtocolHandler` serves `ftp`
downloads and directory listings in passive mode (`EPSV`, then `PASV`), as ADR-0093
records, honouring `-r`, `-C` and `-I` (ADR-0093's BL-438 addendum), and uploads `-T` with
`STOR`, or `APPE` for `-C` (ADR-0093's BL-439 addendum). Still to come: active mode,
`ftps` and the FTP-only options.

**URL schemes:** `ftp` now; `ftps` intended.

This library may reference `Curl.Protocol.Abstractions.UnitLibrary` and nothing
else horizontal. Referencing another protocol library is a build break, and
`Curl.Protocol.Abstractions.UnitTests` fails if one appears.

Never construct a `Socket`, `SslStream` or `HttpClient` here. Take `IConnection`
so the tests in the matching `.UnitTests` project can drive this code from a
recorded byte stream with no network.

Measure curl before pinning a new command or message:
`Record-CurlExchange.ps1 -Ftp` serves one scripted FTP session, with `-FtpReply
'VERB=reply'` overrides and `-FtpData` for the file (served from the last `REST`
offset), and writes `transcript.txt`, with the bytes curl uploads on a `STOR` or `APPE`
data connection in `upload.bin`.
