# Curl.Protocol.Ftp.UnitLibrary

Phase 2.

FTP with a separate control and data channel. `FtpProtocolHandler` serves `ftp` and
`ftps` downloads and directory listings in passive mode (`EPSV`, then `PASV`), as ADR-0093
records, or in active mode under `-P` (`EPRT`, then `PORT`, and `--disable-eprt`), honouring
`-r`, `-C` and `-I` (ADR-0093's BL-438 addendum), and uploads `-T` with `STOR`, or `APPE`
for `-C` (ADR-0093's BL-439 addendum), and honours `--disable-epsv`,
`--no-ftp-skip-pasv-ip`, `--ftp-method`, `--ftp-create-dirs`, `-l` and `-Q`
(ADR-0093's BL-436 addendum). TLS: `ftps://` is TLS from the first byte, and `--ssl`,
`--ssl-reqd` and `--ftp-ssl-control` upgrade `ftp://` with `AUTH`, then `PBSZ` and `PROT`
(ADR-0102 and its BL-437 addendum). Still to come: a host or interface name in `-P`
(BL-463) and the other FTP-only options.

**URL schemes:** `ftp`, and `ftps` for a handler built with a listener and a TLS provider.

This library may reference `Curl.Protocol.Abstractions.UnitLibrary` and nothing
else horizontal. Referencing another protocol library is a build break, and
`Curl.Protocol.Abstractions.UnitTests` fails if one appears.

Never construct a `Socket`, `SslStream` or `HttpClient` here. Take `IConnection`,
`IConnectionListener` and `ITlsProvider` so the tests in the matching `.UnitTests` project
can drive this code from a recorded byte stream with no network.

Measure curl before pinning a new command or message:
`Record-CurlExchange.ps1 -Ftp` serves one scripted FTP session, with `-FtpReply
'VERB=reply'` overrides (several for one verb are answered in turn; `{DATAPORT_HI}` and
`{DATAPORT_LO}` stand for the data port in a `227`) and `-FtpData` for the file (served
from the last `REST` offset, and for `LIST` and `NLST`), and writes `transcript.txt`, with
the bytes curl uploads on a `STOR` or `APPE` data connection in `upload.bin`. It dials back
to the address `EPRT` or `PORT` names, answers `AUTH` by serving TLS (curl needs `-k`),
serves TLS data after `PROT P`, serves implicit FTPS with `-Tls`, and `-FtpIdleMilliseconds`
keeps the control connection open for a long wait.
