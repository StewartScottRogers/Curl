---
id: BL-886
title: Renumber one of the two ADR-0187 decision records
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions, Curl.Core.UnitLibrary, Curl.Console, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-29
completed: 2026-09-30
---
# BL-886 — Renumber one of the two ADR-0187 decision records

## Goal

Every ADR number in `Documentation/Planning/Decisions` names exactly one decision: the duplicate ADR-0187 (and the duplicate ADR-0193) get the next free numbers, and every reference to the renumbered one is updated.

## Context

- Parallel lanes numbered ADRs from their own copies of the folder, so two numbers are taken twice (seen 2026-09-29):
  - `ADR-0187-a-forward-proxy-s-407-is-answered-once-like-a-401.md` (BL-603) and `ADR-0187-http-3-stream-resets-follow-curl-8-21-0-and-a-refused-stream-is-retried-on-a-new-connection.md` (BL-839, used by BL-834's code comments and tests).
  - `ADR-0193-a-redirect-hop-sends-its-own-url-s-user-information-unless-command-line-credentials-win.md` and `ADR-0193-pinnedpubkey-is-checked-in-the-shared-certificate-judgement-on-every-platform.md`.
- Keep the number on the record whose number more code already cites (grep `ADR-0187` and `ADR-0193` across the repository, `.cs` files included); renumber the other and update its references, including task files outside the `Done` archives.

## Acceptance criteria

- [x] `Get-ChildItem Documentation/Planning/Decisions -Filter 'ADR-*.md'` shows 0187 and 0193 once each (the other duplicate pairs are BL-663's and BL-887's; see Notes).
- [x] A grep for each renumbered record's old number finds only references to the record that kept it.
- [x] `dotnet build Curl.slnx -warnaserror` is clean if any `.cs` file changed.

## Notes

- 2026-09-29 (lane 4): the folder has nine duplicated numbers, not two: 0086, 0088, 0093, 0109 (BL-663 owns them), 0158, 0165, 0200 (filed as BL-887) and this task's 0187 and 0193. The first criterion was narrowed to 0187 and 0193 so it says what this task owns.
- Plan, from `git grep -n "ADR-0187\b"` and `"ADR-0193\b"`:
  - **0187:** the HTTP/3 record (BL-839) keeps it; about 20 comments in `Curl.Protocol.Http.*` and ADR-0172 cite it. Renumber `ADR-0187-a-forward-proxy-s-407-is-answered-once-like-a-401.md` (BL-603) to the next free number (0205 today; check `ls` first). Its references: `Curl.Console/CLAUDE.md:195`, `Curl.Console/CurlComposition.cs:42`, `Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:104` and `:1194`, `Curl.Protocol.Http.UnitTests/HttpProtocolHandlerTests.ProxyAuthentication.cs:282`, `Tasks/Backlog/BL-869-…md:21`. Add its row to `README.md`'s index (only the HTTP/3 row is there).
  - **0193:** the `--pinnedpubkey` record keeps it (all the `Curl.Networking.*` and `Curl.Console/TlsClientOptionsMapping.cs` citations). Renumber `ADR-0193-a-redirect-hop-sends-its-own-url-s-user-information-unless-command-line-credentials-win.md` to the number after 0187's. Its only reference: `Curl.Core.UnitLibrary/CLAUDE.md:34`. Add its README row. `Tasks/Backlog/BL-877-load-a-pkcs-12-…md:21` says "BL-804 (ADR-0193)" but means ADR-0195 (the blinded CRT key); correct it to ADR-0195.
- Touches widened to `Curl.Core.UnitLibrary` (0193's reference), `Curl.Console` and `Curl.Protocol.Http.UnitLibrary`/`.UnitTests` (0187's references). `Curl.Console` is in BL-589's touches and `Curl.Protocol.Http.*` in BL-835's, both in Doing, so the task returns to Backlog until they finish.

- 2026-09-30 (lane 1): done. Numbers chosen: the forward-proxy 407 record (BL-603) is now **ADR-0239** and the redirect user-information record (BL-814) **ADR-0240**, not 0288/0289 above the top. Why: 0239 and 0240 are gaps no file on any local branch and no live task (BL-983 reserves 0235-0238) names, while max+1 is exactly what parallel lanes take for new ADRs, so a gap cannot collide again. Both have README index rows now.
- References updated: `Curl.Console/CLAUDE.md`, `Curl.Console/CurlComposition.cs`, `Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs` (lines 104 and 1745; the other ADR-0187 citations there are the HTTP/3 retry), `Curl.Core.UnitLibrary/CLAUDE.md`, ADR-0246 and ADR-0270 (both cited the proxy record), and BL-983's plan ("`ADR-0239` line 51"). The `HttpProtocolHandlerTests.ProxyAuthentication.cs` citation in the plan no longer exists. BL-946 (was BL-877) now says ADR-0195. The two remaining non-HTTP/3-file hits for ADR-0187 (ADR-0223, BL-942) mean the HTTP/3 record, which kept the number.
- The second fast run hung in `Curl.Quic.UnitTests` (first run passed it 405/405); unrelated to this comment-only change, filed as BL-1067.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Backlog. Its 0187 references are in Curl.Console (BL-589 in Doing) and Curl.Protocol.Http.UnitLibrary/UnitTests (BL-835 in Doing); resumes when they finish.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. Each of ADR-0187 and ADR-0193 names one record; the proxy 407 and redirect user-information records are ADR-0239 and ADR-0240 with every reference updated
