# ADR-0065 — HTTP progress reports only the delivered body and never goes back on a retry

- **Status:** Accepted
- **Date:** 2026-09-27
- **Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions").

## Context

ADR-0045 gave every handler an `ITransferProgress` sink whose byte counts are running totals
that never decrease, with "started" reported at most once, and left one question to the first
handler that rewinds while reporting: what "so far" means when a request is sent again. The
HTTP handler (BL-185) is that handler. Within one `ExecuteAsync` call it can:

- open a second connection, when a 401 it answers closes the first;
- send a request body twice, for a 401 it answers (a `-d` body) or a 417 resend without
  `Expect`;
- read a body it discards: the 401 or 417 a retry answers, or a 3xx under `-L`.

Each exchange has its own `HttpRequestBodyWriter` and `HttpResponseBodyReader`, whose counts
start at zero.

## Decision

- **Started** is reported once the first connection is established, before the request is
  sent, and never again in the same call. A connect failure reports nothing, so the console
  draws no meter for it, as curl does not.
- **Download** counts are the body bytes the transfer's output accepts, with the
  Content-Length as the expected total, or `null` for a chunked or read-to-close body. The
  count is reported once when the body starts (zero) and after each write. A body that is read
  and discarded is not reported at all, so only the body the user receives moves the counter.
- **Upload** counts are the request body bytes sent, framing excluded, with the body's length
  as the expected total (`null` for a stream of unknown length), reported before the first
  byte and after each piece.
- One `HttpTransferProgress` per call sits between the handler and the sink and drops any count
  below one it has already passed on. A body sent again therefore holds the counter where the
  first send left it until the resend passes it, and ends at the same total.

## Consequences

Good:

- The sink's contract holds with no change to it, and the console needs no HTTP knowledge.
- A retried upload shows the body's size once, not twice, matching `%{size_upload}`, which
  counts the last exchange's body.

Costs and caveats:

- While a body is resent, the upload counter pauses rather than showing the second send.
- Each redirect hop under `-L` is a new call with its own counts; how the console joins hops
  is the console's decision (BL-310 carries the sink across hops).
- The expected download total is the Content-Length alone, not offset by `-C`.

## Alternatives considered

- **Report every exchange's bytes cumulatively.** Rejected: the counter would end at twice the
  body for a retried upload and include discarded 401 bodies, which the user never sees.
- **Report nothing until the final exchange is known.** Rejected: whether a response is
  retried is known only after the body is sent, so the upload would go unreported live.
- **Report discarded bodies too, under the clamp.** Rejected: a discarded body larger than the
  delivered one would freeze the download counter above the real total.
