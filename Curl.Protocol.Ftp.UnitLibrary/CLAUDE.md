# Curl.Protocol.Ftp.UnitLibrary

Phase 2.

FTP with a separate control and data channel. `FtpProtocolHandler` serves `ftp` and
`ftps` downloads and directory listings in passive mode (`EPSV`, then `PASV`), as ADR-0323
records, or in active mode under `-P` (`EPRT`, then `PORT`, and `--disable-eprt`; a `-P`
address that is not local is bound once more on the control connection's address and still
announced, with curl's `-v` line, ADR-0107; a host name is resolved through the injected
`IDnsResolver` and its first address used, ADR-0108, after the injected
`INetworkInterfaceLookup` finds no interface of that name, ADR-0110), honouring
`-r`, `-C` and `-I` (ADR-0323's BL-438 addendum), `--max-filesize` through `SIZE`, or
part-way when the size is unknown (BL-638), `-z` and `-R` through `MDTM`
(`FtpTimeCondition`, `FtpModificationTime`, ADR-0323's BL-637 addendum), and uploads `-T` with `STOR`, or `APPE`
for `-C` or `-a` (ADR-0323's BL-439 addendum; `-a`, BL-633), converting LF to CRLF under `--crlf`, and for an ASCII upload off Windows
(`FtpUploadLineEndings`, BL-1957),
sends `TYPE A` under `-B` or a `;type=a` URL suffix (`FtpTypeCode`; an ASCII download sends no
`SIZE` or `REST`, BL-633), and honours `--disable-epsv`,
`--no-ftp-skip-pasv-ip`, `--ftp-method`, `--ftp-create-dirs`, `-l` and `-Q`
(ADR-0323's BL-436 addendum), and `--ftp-account` (`ACCT` after a `332` to `USER` or
`PASS`; anything but `230` is exit 11), `--ftp-alternative-to-user` (sent once after a
refused `USER` or `PASS`) and `--ftp-pret` (`PRET RETR <file>`, `PRET LIST`/`NLST` or
`PRET STOR <file>` before the first of `EPSV`/`PASV`, never in active mode; anything but
`200` is exit 84 with no `QUIT`), as curl 8.21.0 was measured to (BL-635). TLS: `ftps://` is TLS from the first byte, and `--ssl`,
`--ssl-reqd` and `--ftp-ssl-control` upgrade `ftp://` with `AUTH`, then `PBSZ` and `PROT`
(ADR-0102 and its BL-437 addendum); under `--ftp-ssl-ccc` `CCC` follows `PROT`, a 5xx keeps
TLS, and any other reply clears it through `IConnection.ClearTlsAsync` (plain text after it on
the OpenSSL build, exit 81 with no `QUIT` on the Schannel build, ADR-0280, BL-636). Time limits (BL-512): the handler reports
`ReportTransferStarted` once the control connection is up, so the runner's `-m` watchdog
(ADR-0117) ends any later stall with curl's `Operation timed out` message, and
`FtpConnectPhaseLimit` holds the greeting, login, `PBSZ`, `PROT` and `PWD` to
`--connect-timeout` (300 s when not given), as curl holds its states before `DO`. `-v` and
`--trace` (BL-930, BL-931): every control command and reply line but `QUIT`'s is reported
as a header, curl's `* ` lines about the data connection and the transfer as info lines, and
every data-connection byte as data. `-D` (BL-1131, ADR-0329): the same reply lines,
`QUIT`'s excepted, are written byte for byte to `ITransferContext.DumpHeaderOutput`, which
`-i` never prints; a refused write is exit 23. Curl's own diagnostic log (`--log-level`, ADR-0222,
BL-924): `FtpDiagnosticLog` writes component `ftp` from `ITransferContext.DiagnosticLog` -
the failure that ends a session as `error` with its `CurlExitCode`, the EPSV-to-PASV and
EPRT-to-PORT fallbacks, a skipped `227` address, a refused `AUTH` or `PROT P` and an
ignored `*` quote as `warning`, login, TLS, directory reached, data connection and
transfer start and end as `info`, and each command and reply as `verbose`, the argument
of `PASS` and `ACCT` never; both connect targets carry the log on. Still to
come: the other FTP-only options.

**URL schemes:** `ftp`, and `ftps` for a handler built with a listener and a TLS provider.

This library may reference `Curl.Protocol.Abstractions.UnitLibrary` and nothing
else horizontal. Referencing another protocol library is a build break, and
`Curl.Protocol.Abstractions.UnitTests` fails if one appears.

Never construct a `Socket`, `SslStream` or `HttpClient` here. Take `IConnection`,
`IConnectionListener`, `ITlsProvider`, `IDnsResolver` and `INetworkInterfaceLookup` so the tests in the matching `.UnitTests` project
can drive this code from a recorded byte stream with no network.

Measure curl before pinning a new command or message:
`Record-CurlExchange.ps1 -Ftp` serves one scripted FTP session, with `-FtpReply
'VERB=reply'` overrides (several for one verb are answered in turn; `{DATAPORT_HI}` and
`{DATAPORT_LO}` stand for the data port in a `227`) and `-FtpData` for the file (served
from the last `REST` offset, and for `LIST` and `NLST`), and writes `transcript.txt`, with
the bytes curl uploads on a `STOR` or `APPE` data connection in `upload.bin`. It dials back
to the address `EPRT` or `PORT` names, answers `AUTH` by serving TLS (curl needs `-k`),
serves TLS data after `PROT P`, clears TLS after a `CCC` reply below 500, serves implicit FTPS with `-Tls`, and `-FtpIdleMilliseconds`
keeps the control connection open for a long wait. The reply `STALL` (e.g. `USER=STALL`,
`GREETING=STALL`) answers nothing, and `-FtpDataHoldMilliseconds` holds a download's data
connection open after its bytes, to measure a stall (BL-512). `-ListenAddress` with `-Curl wsl.exe`
measures the Linux (OpenSSL) build from WSL, which cannot reach Windows' loopback (BL-474).
