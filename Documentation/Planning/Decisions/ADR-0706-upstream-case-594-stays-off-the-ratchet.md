# ADR-0706: Upstream case 594 stays off the ratchet, as upstream disables it

- Status: Accepted
- Date: 2026-10-19
- Decided by Claude under Stewart's delegation
- Task: BL-2795

## Context

Upstream case 594 (lib591: active-mode `STOR` over a multi handle, `NODATACONN`, `EPRT` refused) expects
exit 28 and a protocol that ends with `STOR 594` then `QUIT`. BL-2795 asked for it on
`PassingUpstreamCases.txt`, with the runner jumping its long waits.

What was found:

- curl 8.22.0's `tests/data/DISABLED` lists `594` unconditionally, with no `%if` around it. upstream's
  `runtests.pl` never runs the case, so nothing upstream checks its `<verify>`.
- curl 8.22.0's source cannot give that `<verify>`. In `lib/ftp.c`, `ftp_done_status` leaves
  `ctl_valid` set only for `CURLE_OK` (not premature) and a list of transfer errors that does not hold
  `CURLE_OPERATION_TIMEDOUT`. Any other code clears it, and `ftp_quit` sends `QUIT` only while it is
  set. `lib/multi.c` ends a failed `DOING`/`DOING_MORE` with `multi_done(data, result, FALSE)` and keeps
  the handler's error. `curl_multi_cleanup`, which lib591 calls without `curl_multi_remove_handle`,
  ends a still-attached handle with `multi_done(data, CURLE_OK, TRUE)`, and that premature status
  clears `ctl_valid` too. So no path reports 28 and then sends `QUIT`.
- Real curl 8.22.0 (Schannel, the tool) was measured in 594's setup with `Record-CurlExchange.ps1 -Ftp
  -FtpReply 'EPRT=500 we dont like EPRT now','STOR=STALL'` and `-P - -T file`. It exits 12 after
  120128 ms with no `QUIT`. The accept timer (60 s by default) runs while `STOR` waits for its reply and
  ends with `Accept timeout occurred while waiting server connect`. Then `ftp_done`'s control-reply
  wait gives `* FTP response timeout` and `* control connection looks dead` after another 60 s.

## Decision

Case 594 is not added to `PassingUpstreamCases.txt` and the runner gets no special clock for it. Its
expected `QUIT` contradicts upstream's own source, and upstream does not run it. Faking a `QUIT`
after a timeout would make Curl differ from libcurl for every other caller. The case stays reported
`Inconclusive` with its first difference, like any unlisted case.

Curl's own difference that the measurement turned up is filed as its own task. Curl starts the
active-mode accept timer only once `STOR` is answered, and so ends with 28 after 60 s where curl
ends with 12 after 120 s. The task's work is the accept timer running during the transfer command's
wait, then the 60 s control-reply wait.

## Consequences

- BL-2795's first acceptance criterion cannot be met as written, so the task is Blocked for Stewart
  to close or reword.
- If a later curl release removes 594 from `DISABLED`, the gap office's release watch shows the
  change and the case is looked at again.
