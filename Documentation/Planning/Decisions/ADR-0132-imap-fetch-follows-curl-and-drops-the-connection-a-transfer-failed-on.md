# ADR-0132 — The IMAP fetch follows curl's SELECT and FETCH and drops the connection a transfer failed on

- **Status:** Accepted
- **Date:** 2026-09-28

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-555.

## Context

BL-555 fetches the message an IMAP URL names (`imap://host/INBOX;UID=1`, RFC 5092) with
`SELECT` and `FETCH`. Every case in the task was measured on curl 8.21.0 (Schannel build)
with `Record-CurlExchange.ps1 -Imap` and is pinned in
`Curl.Protocol.Imap.UnitTests\ImapProtocolHandlerFetchTests.cs`; the recordings are in
the task's Notes. A few behaviours cannot be driven through the recorder, and one has no
seam in the IMAP library yet, so they are decided here.

## Decision

1. **The measured behaviour is the specification.** The path is read as curl's
   `imap_parse_url_path` reads it (`ImapUrlPath`), the mailbox is quoted as `imap_atom`
   quotes it (`ImapQuoting`), and `SELECT`, `UID FETCH`/`FETCH`, the literal and every
   exit code and message follow the recordings. A failure in the command phase (exit 3,
   67, 78, 8) sends `LOGOUT` first, as measured.
2. **A failure inside the literal sends no `LOGOUT`.** The server closing inside the
   literal (exit 18, measured, where the recorder had already hung up) and the output
   failing (exit 23, not measurable with the recorder) end the transfer as curl's
   `multi.c` ends a failed transfer: the connection is closed as premature, so
   `imap_disconnect` sends no `LOGOUT`.
3. **The server closing after the literal, before the tagged completion, is exit 56**
   `response reading failed (errno: 0)`, as every other response the session waits for.
   The recorder always tags its last reply line, so this is read from `lib/pingpong.c`
   rather than measured.
4. **A read failing with an `IOException` inside the literal is treated as the server
   closing (exit 18).** `ImapControlChannel` already treats a failed read as end of
   stream for every response (BL-553); curl reports a reset socket as exit 56. Keeping one
   rule in the channel is simpler, and the difference only shows on a reset mid-literal.
5. **URLs that do not name a message** (no mailbox, no `UID` or `MAILINDEX`, a custom
   command, an upload) keep closing the open session with `LOGOUT` and a success until
   BL-556 and BL-557 implement them; a malformed path is exit 3 whatever it asks for.

## Consequences

The fetch matches curl byte for byte on every recorded case, including the output bytes
already written when a later step fails. A reset connection mid-literal reports exit 18
where curl reports 56; a task can split the channel's read failure from end of stream if
that difference ever matters.

## Alternatives considered

- **Send `LOGOUT` after every failure.** Rejected: curl does not after a transfer-phase
  failure, and after exit 18 the connection is gone anyway.
- **Read the literal with the line reader** (`ReadResponseAsync`), as `CAPABILITY`'s
  literals are read. Rejected: it caps a response at 65535 bytes and holds the message in
  memory; curl streams a `FETCH` body of any size to the output.
