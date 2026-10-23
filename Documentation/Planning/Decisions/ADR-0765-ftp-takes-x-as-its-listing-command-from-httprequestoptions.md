# ADR-0765 — FTP takes -X as its listing command from HttpRequestOptions.CustomMethod

- Status: Accepted
- Date: 2026-10-22
- Task: BL-3165
- Decided by Claude under Stewart's delegation.

## Context

curl's `-X`/`--request` (`CURLOPT_CUSTOMREQUEST`) replaces `LIST` or `NLST` in an FTP
directory listing: upstream's `ftp_state_list` (and the `PRET` before it) sends the custom
request instead of the listing verb. Curl always sent `LIST`, or `NLST` under `-l`.
curl 8.21.0 (Schannel), measured with `Record-CurlExchange.ps1 -Ftp`, sends:

- `-X NLST ftp://host/`: `TYPE A` then `NLST`; `-X 'LIST -a'`: `LIST -a`.
- `-l -X 'LIST -a'`: `LIST -a` (the custom request wins over `-l`).
- `--ftp-method nocwd -X NLST ftp://host/sub/`: `NLST sub`; with the default method,
  `CWD sub` then `NLST`.
- `--ftp-pret -X NLST`: `PRET NLST`.
- `-X NLST ftp://host/file.txt`: `TYPE I`, `SIZE file.txt`, `RETR file.txt`; a file
  download ignores `-X`.

## Decision

The FTP handler reads `-X` from `ITransferContext.Http.CustomMethod`, which the engine
already fills from `-X` for every scheme, and uses it as the listing verb when it is set
(`FtpSession.ListVerb`). It is not a new member of a shared contract: the FTP handler already
reads `HttpRequestOptions.IgnoreContentLength` the same way (BL-1982), and the value is the
one option curl itself shares across protocols. A file download keeps `RETR`.

## Consequences

- No contract in `Curl.Protocol.Abstractions.UnitLibrary` changes; only the doc comments of
  `ITransferContext.Http` and `HttpRequestOptions.CustomMethod` now name the FTP use.
- `FtpProtocolHandlerListingRequestTests` pins each measured command line.
