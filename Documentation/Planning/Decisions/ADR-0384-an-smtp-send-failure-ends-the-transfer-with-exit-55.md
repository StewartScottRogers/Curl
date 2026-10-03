# ADR-0384: An SMTP send failure ends the transfer with exit 55

- Status: Accepted
- Date: 2026-10-02
- Task: BL-1243
- Decided by Claude under Stewart's delegation.

## Context

Until BL-1243, `SmtpControlChannel` swallowed an `IOException` from a write and left the next
read to find the connection closed, so a command or message that could not be sent ended with
exit 56 `response reading failed (errno: 0)`. Three tests pinned that (`ExecuteAsync_SendFails_FailsWithExit56WhenNoReplyFollows`,
`ExecuteAsync_CommandWriteFails_DoesNotReportTheCommand`, `ExecuteAsync_MessageWriteFails_DoesNotReportTheData`),
but BL-540's notes record that curl's exit for a send failure "was not measurable with the
recorder": the exit 56 was a placeholder, not a measurement. curl 8.21.0 sends a command through
`Curl_pp_sendf` (`lib/pingpong.c`) and the body through the transfer's own send; both return the
socket filter's error, so `smtp_statemachine` ends with `CURLE_SEND_ERROR` (55), with
`Send failure: Connection was reset` from `lib/cf-socket.c` for a reset and `CURLE_SEND_ERROR`'s
own text `Failed sending data to the peer` otherwise, as the DICT and RTSP handlers already do.

## Decision

1. A failed write of a command or of the message throws `SmtpSendFailedException`, which the
   session, the mail transaction and the command transfer turn into exit 55 with that text.
   Nothing more is sent or read, not even `QUIT`, so `-v` ends with `closing connection #N`.
2. `-v` writes the reset message before that line; the fallback text, which no `failf` writes,
   is not written.
3. A `QUIT` that cannot be sent once the transfer is over is ignored, as its reply is.
4. The three placeholder tests were updated to the new behaviour.

## Consequences

A broken SMTP connection fails as curl's does, with the exit code scripts check for. The POP3
handler swallows the same exception and is follow-up work for its own library.

## Alternatives considered

- Keep exit 56 from the placeholder: not what curl's code does on a failed send.
- Send `QUIT` after a send failure: the connection is already broken, and nothing written to it
  can arrive.
