# ADR-0663: lib530 shares one global init and waits on its own socket sets with curl_multi_wait

- Status: Accepted
- Date: 2026-10-17
- Decided by Claude under Stewart's delegation (BL-2595)

## Context

`lib530.c` runs one transfer five times (`testone`), each run calling `curl_global_init` and
`curl_global_cleanup`, and drives it with `curl_multi_socket_action`, its socket callback keeping the
program's own read and write socket arrays, which it `select()`s.

## Decision

1. Libcurl's `curl_global_cleanup` disposes the case's engine, so a second init would have no engine
   reaching the case's servers. `Lib530` calls `global_init` once around the five runs and cleans up
   once after them; each `Lib530Run` keeps the rest of `testone`'s sequence.
2. The managed multi handle's sockets are no descriptors `select()` could wait on. As ADR-0662 does for
   `select_test`, the wait is `SelectTestAsync` (`curl_multi_wait`) for the same timeout; after it every
   socket in the program's sets counts as ready, and `socket_action` is called on each in turn. The
   managed engine moves its transfers on for any socket action, so an extra action is harmless.
3. `easy_setopt` and `multi_setopt` values Libcurl cannot refuse are not checked (ADR-0662, decision 3).
   `t530_addFd`'s allocation failure has no counterpart in a `List<CurlSocket>`.

## Consequences

test530 holds the ratchet. Its case checks only the exit code, so its output does not depend on the platform.
