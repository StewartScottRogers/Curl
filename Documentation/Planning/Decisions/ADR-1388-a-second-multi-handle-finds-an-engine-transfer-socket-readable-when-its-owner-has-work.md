# ADR-1388: A second multi handle finds an engine transfer socket readable when its owner has work (BL-5943)

Status: Accepted. Decided by Claude under Stewart's delegation. Amends ADR-0955 and ADR-0982.

## Context
CI run 45585553449 failed upstream test2407 on Windows: curl did not finish within 20 seconds,
twice. The next run on the same code passed. `lib2405` polls the sockets one multi handle's
`curl_multi_waitfds` gave on a second multi handle for 500 ms after each `curl_multi_perform`.

An in-memory connection's socket is an engine number (ADR-0955), and a wait never found an event on
another handle's number, so every turn slept the full 500 ms. A queued callback (ADR-0901) runs
only inside a perform, which waits 20 ms for the next one, so on a loaded runner each callback of
the case's four transfers could cost a turn: some fifty callbacks, more than 20 seconds. In real
libcurl the descriptor is readable once data arrives and the poll returns at once.

## Decision
A multi handle's wait or poll answers `PollIn` for an engine socket number of another handle's
transfer while that handle has work for the transfer's next perform: a callback queued, or the
transfer's perform finished and not yet reported. Numbers of the handle's own transfers are not
marked, as its ready count already includes them. The wait looks again every 10 ms, as it does for
any extra socket.

Raising the harness's 20 second limit, or the 20 ms follow-up wait, was rejected: both stay races
against the runner's load, and an application polling `waitfds` sockets would still sleep its whole
timeout where libcurl wakes.

## Consequences
Case 2407 run alone on the development machine went from 2.8 s to 0.8 s. A connect-only socket and
an operating system socket are answered as before.
