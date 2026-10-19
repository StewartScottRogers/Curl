---
id: BL-2788
title: Retry an RTSP request with credentials after a 401 under CURLAUTH_ANY so upstream case 3100 passes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2789]
touches: [Curl.Protocol.Rtsp.UnitLibrary, Curl.Protocol.Rtsp.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Engine.UnitLibrary/TransferContextFactory.cs, Curl.Engine.UnitTests/TransferContextFactoryTests.cs, Curl.Conformance.UnitLibrary/RtspServerConnection.cs, Curl.Conformance.UnitTests/RtspServerConnectorTests.cs]
requirement: none
created: 2026-10-18
completed:
---
# BL-2788 — Retry an RTSP request with credentials after a 401 under CURLAUTH_ANY so upstream case 3100 passes

## Goal

An RTSP request made with `CURLAUTH_ANY` (or `--anyauth`) and credentials is sent first without `Authorization`, and after the server's `401` with `WWW-Authenticate: Basic` is resent once with `CSeq` incremented and `Authorization: Basic ...`, as curl does; upstream case 3100 then passes.

## Context

Found by BL-2699. With BL-2617 and BL-2704 Done, `CURL_UPSTREAM_CASE=3100` and `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust` run case 3100 (not skipped), and it fails at `<verify><protocol>` byte 82 (line 5): Curl sends the first `DESCRIBE` (lines 1-4 match) and then stops. Missing lines:

```
- 5: "DESCRIBE rtsp://127.0.0.1:8996/3100 RTSP/1.0\r\n"
- 6: "CSeq: 2\r\n"
- 7: "Accept: application/sdp\r\n"
- 8: "Authorization: Basic dXNlcjpwYXNzd29yZA==\r\n"
- 9: "\r\n"
```

`Curl.Protocol.Rtsp.UnitLibrary/RtspProtocolHandler.cs` builds only a pre-emptive `Authorization` (line ~418, `authenticator.CreateAuthorization`); it has no 401 retry. The client is `Curl.Conformance.LibTest.UnitLibrary/Lib3100.cs` (`CURLOPT_HTTPAUTH = CURLAUTH_ANY`, `CURLOPT_RTSP_REQUEST = DESCRIBE`). Upstream: `tests/data/test3100`, `lib/rtsp.c` / `lib/http.c` auth negotiation, curl 8.x targeted by `Gap/Baselines/target.json`.

## Acceptance criteria

- [ ] A unit test in `Curl.Protocol.Rtsp.UnitTests` shows an RTSP `DESCRIBE` under any-auth sent once without `Authorization`, then after a `401` with `WWW-Authenticate: Basic` resent with the next `CSeq` and `Authorization: Basic dXNlcjpwYXNzd29yZA==`.
- [ ] Case 3100 passes through the runner on Windows (`CURL_UPSTREAM_CASE=3100`) and is in `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [ ] `Curl.Protocol.Rtsp.UnitLibrary` stays at 100% line and branch coverage with no method past the complexity gate (`Measure-CodeQuality.ps1 -Library Curl.Protocol.Rtsp.UnitLibrary`).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-10-18 (lane 3, first claim): work done and left uncommitted for the shift's stash:
  - `Curl.Protocol.Rtsp.UnitLibrary`: `RtspReplyHeadParser` keeps `WWW-Authenticate` values (`RtspReplyHead.Challenges`); `RtspProtocolHandler.ExchangeAsync` sends the request once more, next `CSeq`, with the authenticator's answer when a request sent without `Authorization` gets a successful-`CSeq` 401 with challenges (`AnswerChallenges`). Tests: `RtspProtocolHandlerAuthenticationRetryTests` (7 pass).
  - Two prerequisites found outside `touches`, added below (no task in Doing on `origin/work/dark-factory` names them): `Curl.Engine.UnitLibrary/TransferContextFactory.cs` ignored `CURLOPT_USERNAME`/`CURLOPT_PASSWORD` (only `CURLOPT_USERPWD` made credentials), so Lib3100 had none; now `CredentialsOf(EasyAuthenticationOptions)` (test `Create_LibcurlUserNameAndPassword_CarriesThemAsCredentials`). `Curl.Conformance.UnitLibrary/RtspServerConnection.cs` lacked rtspd's `swsbounce`, so the second `DESCRIBE` got `<data>` again; now bounced to `<data1>` (test `ReplyWithSwsbounce_...` in `RtspServerConnectorTests`).
  - With those, case 3100's `<verify><protocol>` matches; it fails next at stdout byte 273: the SDP body is never written (handler discards RTSP bodies). Filed BL-2789 for that; this task depends on it.
  - Not yet done on resume: run the fast tests (the engine credential change may move other libcurl cases on the ratchet), Measure-CodeQuality for Rtsp, Engine and Conformance libraries, then list 3100.

## Log

- 2026-10-18: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: 52475b47 2a0a28a6 395361c3 3d836d5e.
- 2026-10-18: Doing -> Backlog. Waits on BL-2789 (RTSP DESCRIBE body to the write callback); 401 retry, libcurl USERNAME/PASSWORD credentials and rtspd swsbounce are done and stashed
- 2026-10-19: Backlog -> Doing. Criteria at claim: 52475b47 2a0a28a6 395361c3 3d836d5e.
- 2026-10-19: Doing -> Blocked. Stewart: dark factory its runs of the last day already cost 4.07 of its 4.61 US dollar cost cap, so it was not run again; split the task; see Z:\repos\Curl.logs\BL-2788-*.jsonl
