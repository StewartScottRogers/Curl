# ADR-0802: Upstream case 836 stays off the ratchet: curl 8.22.0 sends UID FETCH where it expects FETCH

- Status: Accepted
- Date: 2026-10-23
- Task: BL-3398
- Amends: ADR-0737
- Decided by Claude under Stewart's delegation.

## Context

ADR-0737 keeps upstream's disabled cases off the ratchet. BL-3398 asked whether case 836
(IMAP multiple connection authentication, `imap://HOST:PORT/836/;MAILINDEX=1 -u user.one:secret
-: imap://HOST:PORT/836/;UID=2 -u user.two:secret`) could join it. Through Curl it fails only
at its second connection's fetch: Curl sends `B004 UID FETCH 2 BODY[]` where the case's
`<protocol>` expects `B004 FETCH 2 BODY[]`.

The second connection was measured with `Record-CurlExchange.ps1 -Imap`, serving upstream's
greeting and capability list (no `AUTH=`, so the client logs in with LOGIN, as the case
expects), against the reference curl 8.22.0 (Git for Windows' ucrt64 build, Schannel, ADR-0018)
and against Curl, running `-s 'imap://127.0.0.1:PORT/836/;UID=2' -u user.two:secret` alone.
The recorder serves one session per run, so the tag letter is `A` rather than the case's `B`;
the tag letter per connection is already measured and matched by BL-1987. Both sent the same
100 bytes, byte for byte:

```
A001 CAPABILITY
A002 LOGIN user.two secret
A003 SELECT 836
A004 UID FETCH 2 BODY[]
A005 LOGOUT
```

and both exited 0. curl's documentation of the IMAP URL's `UID` part also makes `;UID=2` a
UID FETCH.

## Decision

Case 836 stays off `PassingUpstreamCases.txt`. Its expected `<protocol>` (`FETCH 2 BODY[]`)
does not match what curl 8.22.0 itself sends (`UID FETCH 2 BODY[]`), which is why upstream
lists it in `tests/data/DISABLED`. Curl already sends curl's bytes, so nothing in
`Curl.Original.Protocol.Imap.UnitLibrary` changes; changing it to pass the stale case would make Curl
differ from curl.

## Consequences

- The ratchet does not run 836. Should a later curl release update the case to `UID FETCH`,
  a task re-measures it and adds it.
- Curl keeps sending `UID FETCH` for a `;UID=` URL, as curl does.
