# ADR-0093 — FTP downloads hold curl's measured conversation, in passive mode only

- **Status:** Accepted
- **Date:** 2026-09-27
- **Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions").

## Context

`Curl.Protocol.Ftp.UnitLibrary` had no handler (BL-431). curl's FTP support is large:
passive and active modes, uploads, `ftps`, `--ftp-method`, `-Q`, `-l`, ranges and resume.
The first handler needs a scope that is a true drop-in for the common case, and a
measured answer for every failure it can meet.

`Record-CurlExchange.ps1` gained an `-Ftp` mode: a scripted control server that answers
each command from a reply table and serves a passive data connection. With it, curl
8.21.0 (mingw, Schannel) was recorded for 30 cases on 2026-09-27; the command sequences
and messages are pinned in `FtpProtocolHandlerTests`.

## Decision

- `FtpProtocolHandler` serves `ftp` only. It sends curl's conversation: `USER` (then `PASS`
  on a `331`), `PWD`, one `CWD` per non-empty path directory, `EPSV` (then `PASV` when
  `EPSV` is not answered `229`), `TYPE I`, `SIZE`, `RETR`, `QUIT`. The login defaults to
  `anonymous` / `ftp@example.com`, curl's defaults.
- A path ending in `/` sends `TYPE A` and `LIST` in place of `TYPE I`, `SIZE` and `RETR`,
  because curl does and the difference is two commands; a `450` to `LIST` is an empty
  listing with exit 0, as measured.
- Passive mode only. The data connection goes to the control connection's host and the
  offered port; a `227` reply's address is ignored, as curl's default
  `--ftp-skip-pasv-ip` does.
- Every failure returns the exit code and message curl printed, and sends `QUIT` exactly
  where curl did: after a refused `CWD`, `TYPE`, `SIZE`, `RETR`/`LIST`, an unreadable `229`
  or a refused `PASV`, and a non-226/250 end of transfer; not after a refused login, a bad
  greeting, a control character in the path, an unreadable `227`, or a data connection
  that closed short of `SIZE`'s count.
- A reply ends at the first line of three digits and a space; a bare `331` line does not end
  one. A `230` greeting skips `USER` and `PASS`. A `421` to any command is exit 28
  `Timeout was reached` with no `QUIT`, and `control connection looks dead` when it ends the
  data transfer. A reply line of 65536 bytes or more, its CRLF included, is exit 100
  `A value or data field grew larger than allowed`; 65535 is read. All measured.
- The user name and password are sent as given, CR and LF included: curl 8.21.0 sent
  `-u "a<CR><LF>DELE x:pw"` as two command lines, and a drop-in replacement does the same.
  Only the user controls `-u`; a URL's user information with `%0D` is refused with exit 3
  before any handler runs. Each character is sent as one Latin-1 byte, which is what the
  Windows build sends for `-u jörg` (`6A F6 72 67`).
- Two outcomes were not measured and follow the nearest measured rule: a data connect that
  fails returns the connector's result without `QUIT`, and a data read that fails is exit 56
  `Failure when receiving data from the peer`, the text the gopher handler uses.

## Consequences

`curl ftp://host/path/file` downloads as curl does, with curl's exit codes. `ftps`,
active mode, `--disable-epsv`, `--ftp-method`, `-l` and `-Q` are not implemented. `-r`, `-C`
and `-I` are honoured since BL-438, and `-T` uploads since BL-439 (see the addenda below). Meanwhile `Curl.Console` still routes non-proxied `ftp://` to
`ForwardedFtpProtocolHandler` until the handler is registered there (separate task).

## Alternatives considered

- **Try `PASV` first.** curl 8.21.0 sends `EPSV` first on IPv4 too; sending `PASV` would change
  the bytes a server sees.
- **Leave directory listings to a later task.** A trailing `/` would then need an invented
  message; `LIST` is measured and costs two commands.

## Addendum (BL-438, 2026-09-27): `-r`, `-C` and `-I`

Decided by Claude under Stewart's delegation. Measured with curl 8.21.0 (Git for Windows'
mingw64 build) on 2026-09-27, with `Record-CurlExchange.ps1 -Ftp -FtpData 0123456789`
against `ftp://127.0.0.1:port/dir/f.txt`. The recorder now answers `REST` with `350`,
serves the data from the last `REST` offset, answers `MDTM` with `213 20260927123456`, and
tolerates curl closing the data connection early. Every case below is pinned in
`FtpProtocolHandlerRangeTests`.

| Case | Commands after `SIZE` (`213 10`) | Output | Exit |
| --- | --- | --- | --- |
| `-r 0-4` | `RETR`, `ABOR`, `QUIT` | `01234` | 0 |
| `-r 3-4` | `REST 3`, `RETR`, `ABOR`, `QUIT` | `34` | 0 |
| `-r 5-` | `REST 5`, `RETR`, `QUIT` | `56789` | 0 |
| `-r -3` | `REST 7`, `RETR`, `ABOR`, `QUIT` | `789` | 0 |
| `-r -10` | `REST 0`, `RETR`, `ABOR`, `QUIT` | whole file | 0 |
| `-r -3`, `SIZE` refused | `REST -3`, `RETR`, `ABOR`, `QUIT` | first 3 bytes served | 0 |
| `-r 0-20` | `RETR`, `ABOR`, `QUIT` | whole file | 0 |
| `-r 10-12` | `ABOR`, `QUIT` | nothing | 0 |
| `-r 10-`, `-C 10` | `QUIT` | nothing | 0 |
| `-r -20` | `ABOR`, `QUIT` | nothing | 36 `Offset (-20) was beyond file size (10)` |
| `-C 20` | `QUIT` | nothing | 36 `Offset (20) was beyond file size (10)` |
| `-C 5` | `REST 5`, `RETR`, `QUIT` | `56789` | 0 |
| `-C 5`, `SIZE` refused | `REST 5`, `RETR`, `QUIT` | `56789` | 0 |
| `-C -`, `-o` holding 5 bytes | `REST 5`, `RETR`, `QUIT` | appended `56789` | 0 |
| `-C -`, `-o` absent | `RETR`, `QUIT` | whole file | 0 |
| `-C 5` or `-r 5-`, `REST` answered `502` | nothing more, no `QUIT` | nothing | 31 `Could not use REST` |
| `-r 3-4`, `451` after the data | `REST 3`, `RETR`, `ABOR`, `QUIT` | `34` | 0 |
| `-r 5-` or `-C 5`, `451` after the data | `REST 5`, `RETR`, `QUIT` | `56789` | 18 `server did not report OK, got 451` |
| `-r 0-14`, `SIZE` 20, 10 bytes served | `RETR`, no `ABOR`, no `QUIT` | 10 bytes | 18 `end of response with 5 bytes missing` |
| `-r 0-30`, `SIZE` 20, 10 bytes served | as above | 10 bytes | 18 `end of response with 10 bytes missing` |
| `-C 5` or `-r 2-`, `SIZE` 20, short | `REST`, `RETR`, no `QUIT` | what arrived | 18 `transfer closed with N bytes remaining to read` |

So curl keeps two numbers: an offset, sent with `REST` only when the requested one is not
zero (after a negative suffix offset is resolved against `SIZE`), and a byte limit, set by
`-r first-last` (`last - first + 1`) and `-r -n` (`n`) but not by `-r first-` or `-C`. A
limit stops the read at the limit, sends `ABOR` whose reply is read and not checked, and
reports a short transfer as `end of response with N bytes missing`, where N is the
smaller of the limit and the bytes left by `SIZE`, less what arrived. Without a limit the
BL-431 rules apply unchanged, with `SIZE` less the offset as the expected count.
`FtpDownloadWindow` holds the two numbers; a `-r` wins over `-C` when a hand-built context
carries both, as curl's range handling overwrites the resume offset. The `-C -` offset is
resolved by `Curl.Console` from the output file, so the handler sees `-C 5` or `-C 0`.

`-I` opens no data connection. On a file it sends `MDTM`, `TYPE I`, `SIZE` and `REST 0`
after the `CWD`s, then `QUIT`, and writes to the header output (curl's stdout for `-I`):

```
Last-Modified: Sun, 27 Sep 2026 12:34:56 GMT\r\n   for a 213 MDTM with a YYYYMMDDHHMMSS time
Content-Length: 10\r\n                             for a 213 SIZE
Accept-ranges: bytes\r\n                           for a 350 REST 0
```

each line left out when its reply is anything else (`MDTM` `550` or `213 garbage`, `SIZE`
`502`, `REST` `502`), exit 0. A `550` to `SIZE` is exit 78 and a refused `TYPE` exit 17,
each after `QUIT` and after the `Last-Modified` line already written. On a directory URL
curl sends `QUIT` straight after the `CWD`s and writes nothing, exit 0.

Not measured, and decided by the nearest measured rule: a `-r` on a directory listing is
ignored (`LIST` has no `REST`); an `MDTM` time that is 14 digits but no real date (month
13) writes no `Last-Modified` line, where curl's own date parser may differ; a header line
the header output refuses is exit 23 `client returned ERROR on write of N bytes`, the
`file://` text, after `QUIT`; a failure sending `ABOR` or reading its reply is ignored, as
it is for `QUIT`.

## Addendum (BL-439, 2026-09-27): `-T` uploads

Decided by Claude under Stewart's delegation. Measured with curl 8.21.0 (Git for Windows'
mingw64 build) on 2026-09-27, uploading the twelve bytes `hello world\n` with
`Record-CurlExchange.ps1 -Ftp`. The recorder now answers `STOR` and `APPE` with `150`,
accepts the passive data connection, records every byte curl sends on it (to `upload.bin`
and the transcript) until curl closes it, then answers `226` (or the `STORDONE` override).
Every case below is pinned in `FtpProtocolHandlerUploadTests`.

The conversation is the download's up to `TYPE I` - login, `PWD`, one `CWD` per directory,
`EPSV` (then `PASV`), `TYPE I` - and shares its code (`FtpSession`) and its failure rules;
then `STOR <file>`, the upload written to the data connection, the data connection closed
(which tells the server the file has ended), the end-of-transfer reply, and `QUIT`. A
directory URL (`-T up.txt ftp://host/dir/`) reaches the handler with the file name already
appended by `Curl.Console` (`UploadUrl`), so it stores `up.txt` in `dir`.

| Case | Commands after `TYPE I` | Bytes on the data connection | Exit |
| --- | --- | --- | --- |
| `-T up.txt .../dir/sub/file.txt` | `STOR file.txt`, `QUIT` (after `CWD dir`, `CWD sub`) | all 12 | 0 |
| `-T empty.txt .../dir/e.txt` | `STOR e.txt`, `QUIT` | none | 0 |
| `STOR` answered `553` (or `550`) | `STOR`, `QUIT` | none | 25 `Failed FTP upload: 553` |
| `CWD` answered `550` | nothing after `CWD`, then `QUIT`; no `MKD` | none | 9 `Server denied you to change to the given directory` |
| `451` after the data | `STOR`, `QUIT` | all 12 | 18 `server did not report OK, got 451` |
| `250` after the data | `STOR`, `QUIT` | all 12 | 0 |
| `-C 5` | `APPE`, `QUIT` (no `SIZE`) | the last 7 | 0 |
| `-C 12`, `-C 20` | `QUIT` | none | 0 (curl notes "File already completely uploaded") |
| `-C 5 -T empty.txt` | `APPE`, `QUIT` | none | 0 |
| `-C 5 -T -`, `-C 20 -T -` | `APPE`, `QUIT` | all 12: nothing skipped from standard input | 0 |
| `-C -`, `SIZE` `213 3` | `SIZE`, `APPE`, `QUIT` | the last 9 | 0 |
| `-C -`, `SIZE` `213 0` or `550` | `SIZE`, `STOR`, `QUIT` | all 12 | 0 |
| `-C -`, `SIZE` `213 12` or `213 20` | `SIZE`, `QUIT` | none | 0 |
| `-C - -T -`, `SIZE` `213 20` | `SIZE`, `APPE`, `QUIT` | all 12 | 0 |
| `-T - .../dir/` (no file name) | nothing after `PWD`, no `QUIT` | none | 3 `Uploading to a URL without a filename` |

So a non-zero offset (`-C N`, or the `SIZE` count for `-C -`, which `Curl.Console` passes
as `ResumeUploadFromUnknownOffset`) sends `APPE` instead of `STOR`, having skipped that
many bytes of a source that can seek; when the offset reaches the end of a non-empty
source that can seek, curl sends `QUIT` straight after `TYPE I` and succeeds with nothing
sent. An empty source and a source that cannot seek skip nothing. `FtpUploadOffset` holds
that rule. Any reply of 400 or more to `STOR` or `APPE` is exit 25; any lower one lets the
upload go ahead, as curl's own check is `>= 400`.

Not measured, and decided by the nearest measured rule: a failed read of the upload source
ends the upload as its end does, as the HTTP handler treats a failed read (curl's read
callback); a failed write to the data connection is exit 55 `Failure when sending data to
the peer`, with no `QUIT`, as a failed receive ends a download; `-I` with `-T` uploads, as
the upload is checked first. `--crlf` (`ConvertLineEndings`) and `-a`/`--append` are not
honoured on FTP uploads yet.