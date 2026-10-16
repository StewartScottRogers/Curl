# ADR-0571 — `Libcurl`'s protocol option setters check values as curl 8.22.0's `setopt.c` does

- **Status:** Accepted
- **Date:** 2026-10-15

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2407.

## Context

BL-2407 adds a setter row (ADR-0560) for every `CURLOPT_*` that `EasyProtocolOptions` holds: FTP,
SSH, SMTP, TFTP, TELNET, RTSP and `WS_OPTIONS`. The record's shape is fixed by `Curl.Api.UnitLibrary`.
Upstream answers out-of-range values in different ways, option by option.

## Decision

1. The rows live in `Libcurl/Options/Protocols`. `EasyOptionSetters.AddProtocolSetters` calls
   `EasyProtocolOptionSetters.AddTo`, with one named static method per row and explicit delegate
   creation, as ADR-0565 does for HTTP, so the coverage-based complexity measure stays low.
2. These answer `BadFunctionArgument`, as `setopt.c` does: `FTP_CREATE_MISSING_DIRS` outside 0..2,
   `FTPSSLAUTH` and `FTP_SSL_CCC` outside 0..2, `FTP_FILEMETHOD` outside 0..3, and
   `SSH_HOST_PUBLIC_KEY_MD5` given a string that is not 32 characters long.
3. These are accepted and adjusted, not refused, as upstream does: `TFTP_BLKSIZE` below 8 becomes 512
   and above 65464 becomes 65464; an unknown `RTSP_REQUEST` becomes `None` (upstream's switch
   default); `WS_OPTIONS` keeps only `CURLWS_RAW_MODE` and `CURLWS_NOAUTOPONG`; `SSH_AUTH_TYPES` keeps
   the low 32 bits unchecked (upstream's `(int)arg`), so `CURLSSH_AUTH_ANY` (-1) is `Any`.
4. `RTSP_STREAM_URI` given `null` restores `*`, the record's non-null default and what libcurl sends
   when no URI is set. Every other string option takes `null` as "unset". The list options (`QUOTE`,
   `PREQUOTE`, `POSTQUOTE`, `MAIL_RCPT`, `TELNETOPTIONS`) store a copy, and `null` empties them.
   `RTSP_CLIENT_CSEQ` and `RTSP_SERVER_CSEQ` take any value, unchecked.

## Consequences

- `FTPPORT` sets only `ActiveAddress`. Upstream also keeps a separate "use `PORT`" flag. The record
  says that `null` means passive, so that flag is not needed.
