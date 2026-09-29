---
id: BL-814
title: Send a redirect Location's own user name and password on the next hop
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-791]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests, Documentation/Planning/Decisions/ADR-0193-a-redirect-hop-sends-its-own-url-s-user-information-unless-command-line-credentials-win.md]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-814 — Send a redirect Location's own user name and password on the next hop

## Goal

Under `-L`, a `Location` that carries user information (`Location: http://c:d@127.0.0.1:<P>/b`) makes the next hop send that user name and password, as curl 8.21.0 does, instead of the first URL's credentials or none.

## Context

- Found in BL-791: `Curl.Console/TransferCredentialLookup.cs` turns the first URL's user information into `ITransferContext.Credentials` once per URL; `Curl.Core.UnitLibrary/RedirectFollower.cs` then keeps those credentials to the same host and drops them to another (`sendCredentials`), and never reads the hop URL's own `User`/`Password`.
- Related: BL-790 looks the netrc entry up again for each hop's host; keep the two consistent.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1` (two connections): a 302 to `/b` on the same host with and without user information in `Location`, and to another host (`localhost`) with user information, for `http://a:b@` and for `-u q:r`; request bytes copied into Notes.
- [x] `Curl.Core.UnitTests` pin each measured case.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Core` reports 100% line and branch coverage and no failing member.

## Notes

Measured 2026-09-29 with `Record-CurlExchange.ps1 -Connections 2`, curl 8.21.0 (Schannel,
mingw64), `curl -s -L [opts] <first>`, first response `302` with the `Location` shown, second
`200`. P = 18814. `YTpi` = a:b, `Yzpk` = c:d, `cTpy` = q:r, `Yzo=` = c:. Every run exit 0.

| First URL / opts | Location | Hop 1 | Hop 2 request |
| --- | --- | --- | --- |
| `http://a:b@127.0.0.1:P/a` | `/b` | `Authorization: Basic YTpi` | `GET /b`, `Host: 127.0.0.1:P`, `Authorization: Basic YTpi` |
| `http://a:b@127.0.0.1:P/a` | `http://c:d@127.0.0.1:P/b` | `YTpi` | `Host: 127.0.0.1:P`, `Authorization: Basic Yzpk` |
| `http://a:b@127.0.0.1:P/a` | `http://c:d@localhost:P/b` | `YTpi` | `Host: localhost:P`, `Authorization: Basic Yzpk` |
| `http://a:b@127.0.0.1:P/a` | `http://localhost:P/b` | `YTpi` | `Host: localhost:P`, no Authorization |
| `http://a:b@127.0.0.1:P/a` | `http://127.0.0.1:P/b` (absolute, same host) | `YTpi` | `Host: 127.0.0.1:P`, no Authorization |
| `http://a:b@127.0.0.1:P/a` | `http://c@127.0.0.1:P/b` | `YTpi` | `Authorization: Basic Yzo=` |
| `http://a:b@127.0.0.1:P/a`, `--location-trusted` | `http://localhost:P/b` | `YTpi` | no Authorization |
| `-u q:r http://127.0.0.1:P/a` | `/b` | `cTpy` | `Authorization: Basic cTpy` |
| `-u q:r http://127.0.0.1:P/a` | `http://c:d@127.0.0.1:P/b` | `cTpy` | `Authorization: Basic cTpy` |
| `-u q:r http://127.0.0.1:P/a` | `http://c:d@localhost:P/b` | `cTpy` | `Host: localhost:P`, `Authorization: Basic Yzpk` |
| `-u q:r http://127.0.0.1:P/a` | `http://localhost:P/b` | `cTpy` | no Authorization |
| `-u q:r --location-trusted http://127.0.0.1:P/a` | `http://c:d@localhost:P/b` | `cTpy` | `Authorization: Basic cTpy` |
| `-u q:r http://a:b@127.0.0.1:P/a` | `http://c:d@127.0.0.1:P/b` | `cTpy` | `Authorization: Basic cTpy` |
| `http://127.0.0.1:P/a` (none) | `http://c:d@127.0.0.1:P/b` | none | `Authorization: Basic Yzpk` |
| `-u a:b http://a:b@127.0.0.1:P/a` | `http://127.0.0.1:P/b` | `YTpi` | `Authorization: Basic YTpi` |

Rule: URL credentials belong to their URL (a relative `Location` resolves with the first URL's
user information, so it keeps them); `-u` credentials go where `--location-trusted` or same
origin lets them and there beat the hop URL's own; otherwise the hop URL's own are sent.

Decision (ADR-0193): `RedirectFollower` cannot see where `ITransferContext.Credentials` came
from, so first-hop credentials equal to the first URL's own user information are taken as the
URL's. Adding a source flag would change `Curl.Protocol.Abstractions.UnitLibrary` and
`Curl.Console`, both in the `touches` of tasks in Doing (BL-781, BL-608), so it stays inside
Curl.Core. The one differing case is the last row: Curl sends none there. Added
`Documentation/Planning/Decisions/ADR-0193-...` to `touches` for the ADR (no task in Doing
names it).

`Curl.Console/TransferCredentialLookup.cs`'s remarks still say "a redirect keeps the credentials
to the same host and drops them to another"; that stays true for `-u` and netrc credentials,
so it was left as is (outside this task's `touches`).

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. Under -L each hop sends its own Location URL's user name and password, and -u credentials win on same-origin or trusted hops, as curl 8.21.0 does
