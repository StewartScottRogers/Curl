# ADR-0954: A `-Z` run's first batch is set up before any of its transfers starts

- **Status:** Accepted
- **Date:** 2026-10-28
- **Task:** BL-3699 (CGR-170). Amends ADR-0127.
- Decided by Claude under Stewart's delegation.

## Context

curl 8.22.0 sets up `--parallel-max` plus one transfers (glob, `-T` file) before its first
`curl_multi_perform`. A setup failure among them makes it abandon the batch: nothing is sent, and each
transfer already set up is reported with the failure's code and `curl_easy_strerror` text. Curl started
each transfer as soon as a slot was free, so it performed the others and reported them as they ended.
Measured on curl 8.22.0 (Schannel) with `file://` URLs and a closed port (BL-3699 Notes):

- `--parallel-max 1`: a bad glob at the second URL abandons the first; at the third it does not.
  `--parallel-max 2`: the third URL abandons, the fourth does not; `--parallel-max 3` and the default 50
  likewise (`max + 1`). `--parallel-immediate` changes nothing.
- A URL that is not a well-formed glob past the first batch prints the glob lines, is then taken as
  written (`http://[::1` fails `URL rejected: Bad IPv6 address`, `file:///...?{a` is read), the run carries
  on, and the run exits with the glob's 3 even after an earlier exit 7.
- A `-T` file that cannot be opened past the first batch prints `cannot open`, reports its transfer with
  exit 26 and the generic text whatever failed before, starts nothing more, and the run exits 26.

## Decision

`ParallelRun` holds the first `--parallel-max + 1` transfers unstarted (`HoldInFirstBatch`), opening each
one's `-T` file as it is held. When the batch is complete, or the URLs run out, they start in order
(`StartHeldAsync`). A setup failure while the batch is held (bad glob, `-T` file, any other run-ending
setup refusal) abandons it (`AbandonFirstBatchAsync`): each held transfer is reported in command-line order
with the failure's code and `CurlEasyErrorText`. A setup failure's code is the run's exit code, whatever
failed before it (`SetupFailure`). Past the first batch a bad URL glob is taken as written, and a `-T` file
that cannot be opened reports exit 26 under `-Z` rather than the previous transfer's code (BL-440, which
stays for serial runs).

## Consequences

The first batch's `-T` files are opened twice: once to check them while held, once to send. Serial runs
are unchanged.
