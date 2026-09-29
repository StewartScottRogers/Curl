# ADR-0193 — A redirect hop sends its own URL's user information unless command-line credentials win

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-814.

## Context

Under `-L`, `RedirectFollower` (`Curl.Core.UnitLibrary`) kept the first hop's
`ITransferContext.Credentials` to the same host, port and scheme and dropped them to another.
It never read the hop URL's own user information. Measured with `Record-CurlExchange.ps1`
against curl 8.21.0 (BL-814 Notes), curl treats the two kinds of credentials differently:

- Credentials written in a URL belong to that URL. A relative `Location` keeps the first
  URL's (the resolved target carries them); an absolute one brings its own, or none - even
  to the same host.
- `-u` credentials go to every same-origin hop (every hop under `--location-trusted`) and
  there beat the hop URL's own; to another origin the hop URL's own are sent instead.

`Curl.Console` folds the first URL's user information into `ITransferContext.Credentials`
before the follower sees it (`TransferCredentialLookup`), so the follower cannot tell the two
kinds apart from the credentials alone, and `ITransferContext` has no field saying where they
came from.

## Decision

The follower takes the first hop's credentials as the URL's own when they equal the first
URL's percent-decoded user information, and as command-line credentials (`-u`, netrc)
otherwise. Each hop then sends the command-line credentials when it is same-origin or
`--location-trusted` and there are some, else its own URL's user information - user name and
password percent-decoded, either one empty when absent, none when both are.

No contract changes: `Curl.Protocol.Abstractions.UnitLibrary` and `Curl.Console` stay as they
are.

## Consequences

- Every measured case in BL-814's Notes is reproduced and pinned in `RedirectFollowerTests`.
- One case differs from curl: `-u` credentials that equal the first URL's own
  (`-u a:b http://a:b@host/`) followed by an absolute `Location` to the same host without user
  information. curl sends `a:b` (the `-u` ones); Curl sends none, taking them as the URL's.
  Both inputs name the same credentials, so the difference only shows on that one hop.

## Alternatives considered

- **Add a flag to `ITransferContext` saying the credentials came from the URL.** Exact, but it
  changes the shared contract every protocol depends on, and `Curl.Console` with it, both held
  by other work at the time. It remains the way to close the one differing case if it ever
  matters.
- **Keep sending the first hop's credentials to the same origin.** Sends `a:b` where curl
  sends `c:d` or nothing. Rejected: not a drop-in replacement.
