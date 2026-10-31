---
id: BL-4423
title: Send Oblivious HTTP inner credentials only as curl would and honour --fail-with-body
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Extension.ObliviousHttp.UnitLibrary/Relay/InnerRequest.cs, Curl.Extension.ObliviousHttp.UnitLibrary/Relay/InnerResponseOutput.cs, Curl.Extension.ObliviousHttp.UnitTests/Relay]
requirement: none
created: 2026-10-31
completed:
---
# BL-4423 — Send Oblivious HTTP inner credentials only as curl would and honour --fail-with-body

## Goal

Under `--oblivious-relay`, the encapsulated request carries `-u` credentials as Basic only when curl itself would send Basic, and a 4xx or 5xx inner response under `--fail-with-body` writes the body and exits 22.

## Context

Found by the shift-end review of 2026-10-31 (ADR-0710; code-reviewer and type-design-analyzer, Important) in the merge `55fa1ce15...ac0e3e86e`.

- `Curl.Extension.ObliviousHttp.UnitLibrary/Relay/InnerRequest.cs:53-58`: the new `AuthorizationOf` builds `Basic base64(user:password)` from `context.Credentials` whenever credentials are present. It ignores `HttpRequestOptions.AuthSchemes` (so `--digest`, `--ntlm`, `--negotiate` or `--anyauth` with `-u` send the password in reversible form, which curl never does) and `AuthorizationHeaderWithheld`. The `Refusals` table (`:42-50`) does not refuse those schemes either.
- `:29-35` with `FieldOf`: `-H "Authorization:"` (no value) removes curl's own header, but `FieldOf` returns null for it, so `AddUnlessGiven` sees no `authorization` field and adds Basic anyway. The same holds for the new `referer` and `cookie` additions with `-H "Referer:"` / `-H "Cookie:"`.
- `AuthorizationOf` encodes `user:password` as UTF-8; the rest of Curl encodes credentials with the command line's text encoding - use the same one.
- `Curl.Extension.ObliviousHttp.UnitLibrary/Relay/InnerResponseOutput.cs:42`: `FailsOnStatus` checks only `HttpFailMode.Fail`; `HttpFailMode.FailWithBody` (`--fail-with-body`) writes the body and exits 0 instead of 22.

## Acceptance criteria

- [ ] With `-u` and any `AuthSchemes` other than Basic alone, the hook either refuses the transfer with exit 2 through the `Refusals` table (naming the option) or sends no `Authorization`; it never sends Basic. A unit test in `Curl.Extension.ObliviousHttp.UnitTests/Relay` pins it for `--digest`.
- [ ] With `AuthorizationHeaderWithheld` set, no `Authorization` field is generated; a unit test pins it.
- [ ] `-H "Authorization:"`, `-H "Referer:"` and `-H "Cookie:"` each suppress the field the hook would otherwise add; a unit test pins each.
- [ ] Basic credentials are encoded the same way Curl's own HTTP handler encodes them; a test with a non-ASCII password pins the bytes.
- [ ] Under `HttpFailMode.FailWithBody` a 4xx inner response writes the body and returns exit 22 with curl's `The requested URL returned error: <code>` message; a unit test pins it.
- [ ] `dotnet build` is clean and the fast tests pass; the library stays at 100% line and branch coverage.

## Notes

## Log

- 2026-10-31: Created.
- 2026-10-31: Backlog -> Doing. Criteria at claim: 0603a425 c7726eb9 8ff87ff4 519f7cac 9f4b8cf2 88ec633d.
