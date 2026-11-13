# ADR-1264 — A redirect hop under `-u` and a netrc option sends its own host's netrc entry

- **Status:** Accepted
- **Date:** 2026-11-12

Decided by Claude under Stewart's delegation (task BL-5035, finding STD-040).

## Context

With `-u user:pass --netrc-optional --netrc-file netrc -L`, curl 8.22.0 (Schannel) sends `user:pass` to the
first host and, after a redirect to another host, that host's own netrc entry (`buser:bpass`). Curl sent nothing
there: `TransferCredentialLookup.ForRedirectHops` gave no selector under a `-u` user name, so `RedirectFollower`'s
origin rules dropped `-u` on the cross-host hop and nothing looked the new host up.

## Decision

1. Under a `-u` user name and a netrc option, `ForRedirectHops` takes the transfer's first URL and gives a
   selector: a hop `-u` is sent to (the first URL's scheme, host and port, or any hop under
   `--location-trusted`) sends `-u`'s credentials as the first hop does; any other hop is looked up as if `-u`
   were not given: its host's netrc entry, else the hop URL's own credentials, else none.
2. Under `--location-trusted` `-u` wins on every hop, as it wins on the first: curl only consults netrc when no
   user name is in effect for the connection. This case was not measured; it follows the first hop's measured
   precedence, and changes nothing Curl did before.
3. The curl tool's runner builds its follower after the URL is parsed so it can pass the first URL; the library
   handle passes its URL the same way. Without a netrc option, or without `-u`, nothing changes.

## Why

- Match the platform's curl: the review measured REF sending the new host's entry.
- `-u` is still never sent to another host without `--location-trusted`; only the new host's own entry is added.
