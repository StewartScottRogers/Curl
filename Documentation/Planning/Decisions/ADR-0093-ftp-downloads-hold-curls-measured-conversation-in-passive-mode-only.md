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

`curl ftp://host/path/file` downloads as curl does, with curl's exit codes. Uploads, `ftps`,
active mode, `--disable-epsv`, `--ftp-method`, `-l`, `-Q`, `-r`, `-C` and `-I` are not
implemented; the handler ignores `Range`, `ResumeFrom`, `Upload` and `NoBody` today and
downloads the whole file, which must be fixed before it is registered. Meanwhile `Curl.Console` still routes non-proxied `ftp://` to
`ForwardedFtpProtocolHandler` until the handler is registered there (separate task).

## Alternatives considered

- **Try `PASV` first.** curl 8.21.0 sends `EPSV` first on IPv4 too; sending `PASV` would change
  the bytes a server sees.
- **Leave directory listings to a later task.** A trailing `/` would then need an invented
  message; `LIST` is measured and costs two commands.
