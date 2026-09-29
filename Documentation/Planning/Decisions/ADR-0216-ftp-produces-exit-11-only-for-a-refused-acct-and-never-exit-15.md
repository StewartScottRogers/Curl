# ADR-0216 — FTP produces exit 11 only for a refused `ACCT`, and never exit 15

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-662.

## Context

The conformance audit of 2026-09-28 (row 45) noted that the FTP handler never ends with exit 11
(`CURLE_FTP_WEIRD_PASS_REPLY`) or exit 15 (`CURLE_FTP_CANT_GET_HOST`). BL-662 measured curl 8.21.0
(mingw, Schannel) with `Record-CurlExchange.ps1 -Ftp -FtpReply`; the stderr and exit codes are in
BL-662's Notes. In short:

- `PASS` answered with any `2xx` (`202`, `231`) logs in; `332` without `--ftp-account` is exit 67
  `ACCT requested but none available`; `421` is exit 28 `Timeout was reached`; everything else
  (`150`, `350`, `530`) is exit 67 `Access denied: <code>`. No `PASS` reply gives exit 11.
- Exit 11 comes only after `ACCT`: with `--ftp-account`, a `332` to `PASS` sends `ACCT <account>`,
  and any reply but `230` (`202` and `530` measured) is exit 11 `ACCT rejected by server: <code>`.
- A `227` naming an unreachable address under `--no-ftp-skip-pasv-ip` is the ordinary connect
  failure (exit 28 after the operating system's SYN timeout for `10.255.255.1`, exit 7 for a
  refused port), and with `--ftp-skip-pasv-ip` (the default) the address is not used at all.
- curl 8.21.0's binary no longer holds the text `cannot resolve new host`: the data connection's
  host goes through the ordinary connect and resolver, so nothing in its FTP code returns exit 15.
  Only the `curl_easy_strerror` text for code 15 remains.

## Decision

1. **Exit 11 belongs to `ACCT`.** The handler already answers every measured `PASS` reply as curl
   does, and BL-662 pins them. Sending `ACCT` needs `--ftp-account` in `ITransferContext`, which is
   BL-635's work; BL-635 also pins `ACCT rejected by server: <code>` as exit 11.
2. **No exit 15.** The data connection's failures stay the connector's own exit and message (6 for
   a name that cannot be resolved, 7 for a refusal, 28 for a timeout), as in curl 8.21.0. Mapping any
   of them to 15 would make Curl differ from the curl it replaces.

## Consequences

`CurlExitCode.FtpCantGetHost` stays unused by the FTP handler, like curl's own `CURLE_FTP_CANT_GET_HOST`.
curl's message for a failed data connect names the control host and then `via <data host>:<port>`;
the connector's message names the data host alone. That difference is a follow-up task.

## Alternatives considered

- **Exit 11 for a `2xx` other than `230`, or for `332`.** Rejected: measured curl logs in on any
  `2xx` and gives exit 67 for `332`.
- **Exit 15 when the data host cannot be resolved.** Rejected: curl 8.21.0 gives the resolver's exit
  6; exit 15 was the behaviour of older curl releases, not of the one Curl matches.
