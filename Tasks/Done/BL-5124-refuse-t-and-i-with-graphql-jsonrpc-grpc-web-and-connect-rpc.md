---
id: BL-5124
title: Refuse -T and -I with --graphql, --jsonrpc, --grpc-web and --connect-rpc with exit 2 before sending (EXT-051)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.Composition.UnitLibrary/BodyReplacingHookExclusivity.cs, Curl.Extension.Composition.UnitLibrary/ExtensionTransferHookCatalogue.cs, Curl.Extension.Composition.UnitTests/BodyReplacingHookExclusivityTests.cs, Documentation/Planning/Decisions/ADR-1262-two-body-replacing-extension-options-are-refused-with-exit-2.md]
requirement: none
created: 2026-11-13
completed: 2026-11-13
---
# BL-5124 — Refuse -T and -I with --graphql, --jsonrpc, --grpc-web and --connect-rpc with exit 2 before sending (EXT-051)

## Goal

`-T` (an upload stream) and `-I` (no body) given together with `--graphql`, `--jsonrpc`, `--grpc-web` or `--connect-rpc` fail with exit 2, naming both options, before anything is sent.

## Context

From the outside fitness review of 2026-11-13 (commit 56cb97ede, `preview` build), finding EXT-051 (Medium, Confirmed). Silent wrong operation: the call is dropped or sent under the wrong method. This is the remainder of the earlier EXT-043 fix, which handled `-d`/`-F` combinations only.

**Repro.** With a server whose `/echo` logs the request (PowerShell or a C# file-based app, never Python); `q.graphql` contains `{ hello }`; `up.txt` contains `upl`:
1. `curl -sS -m 5 --connect-rpc unary -T up.txt http://127.0.0.1:28000/echo`
2. `curl -sS -m 5 --graphql q.graphql -T up.txt http://127.0.0.1:28000/echo`
3. `curl -sS -m 5 --jsonrpc a -T up.txt http://127.0.0.1:28000/echo`
4. `curl -sS -m 5 --grpc-web -T up.txt http://127.0.0.1:28000/echo`
5. `curl -sS -m 5 --jsonrpc a -I http://127.0.0.1:28000/echo -o NUL -w "%{http_code} %{method}\n"` (also with `--graphql q.graphql`, `--grpc-web`, `--connect-rpc unary`)

**Expected.** ADR-1139: for `--connect-rpc`, "a body curl reads from a stream (`-T`, `@-` streamed) fails with exit 2". ADR-1026: GraphQL is "always a POST whose body is the JSON object". ADR-1028: JSON-RPC replaces the body. ADR-1132: gRPC-Web sends one frame and `-F` fails with exit 2. The port's own `-d x -I` exits 2. Each command should exit 2 before sending.

**Actual.** Steps 1-4 send `PUT /echo` with the raw `upl` body (the call is dropped; step 1 adds `Connect-Protocol-Version: 1` with no Content-Type; step 4 adds `X-Grpc-Web: 1` with an unframed body) and exit 0 (`--connect-rpc stream -T` exits 8). Step 5 sends `POST` with the call body, prints only headers, shows `%{method}` POST, exit 0.

**Where to start.**
- `Curl.Extension.ConnectRpc.UnitLibrary/ConnectTransferHook.cs` (`MessageOf(http.Body)` sees null for `-T` and falls back to `{}`, then curl's handler sends `context.Upload`), and the same gap in `Curl.Extension.GraphQl.UnitLibrary`, `Curl.Extension.JsonRpc.UnitLibrary` and `Curl.Extension.GrpcWeb.UnitLibrary`.
- The one place that knows all four options is `Curl.Extension.Composition.UnitLibrary/BodyReplacingHookExclusivity.cs` (ADR-1262, registered by `ExtensionTransferHookCatalogue`): `HookFor(ExtensionOptionValues)` returns a refusing hook only when two options are given. Extend it so that with one body-replacing option given it returns a hook that refuses (`CurlExitCode.FailedInit`, exit 2) when `context.Upload is not null` or `context.NoBody` is true, with a message naming the option and `-T` or `-I`, for example `--connect-rpc cannot be used with -T`, and otherwise calls `next`. Doing it there keeps the four extension libraries untouched and the touches narrow; if a different place proves necessary, add those files to `touches`.
- The hook must run before the extension's own hook and before curl's handler sends; check the order the catalogue gives.

## Acceptance criteria

- [x] New tests in `Curl.Extension.Composition.UnitTests/BodyReplacingHookExclusivityTests.cs` (or a new `BodyReplacingHookUploadAndHeadTests.cs` beside it) prove, for each of `--graphql`, `--jsonrpc`, `--grpc-web` and `--connect-rpc`, that a transfer whose context has an `Upload` stream returns exit 2 with a message naming that option and `-T`, and that `next` is never called (nothing is sent).
- [x] The same tests prove it for `NoBody` (`-I`): exit 2, a message naming the option and `-I`, `next` never called.
- [x] A regression test proves that with exactly one body-replacing option and neither `-T` nor `-I`, the hook calls `next` unchanged, and that the existing two-option refusal (ADR-1262) is unchanged.
- [x] The ADR-1262 text or the relevant extension's spec row is updated to state the `-T` and `-I` refusal, and `--extended-help` / `--ai-help` text for these options is checked and corrected if it says otherwise.
- [x] The tests are platform-neutral (no real socket or file path).
- [x] `dotnet build Curl.Extension.Composition.UnitLibrary -warnaserror` is clean, `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Done: hook now returns a -T/-I refusing hook for one option (ADR-1262 addendum; help text checked, says nothing contrary). Quality run: 0 failing.
- Review acceptance check, for a hand check: each of steps 1-5 exits 2 with a message naming the extension option and `-T` (or `-I`), and the server logs no request.
- If the ADR text must change, add the ADR file to `touches` with the edit (the task cannot edit files outside its `touches` without overlap risk).

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: 5249a0b5 ae47e37a 26f28a1f 2fe7de99 43bdfb60 a240fabc.
- 2026-11-13: Doing -> Done. -T and -I with --graphql/--jsonrpc/--grpc-web/--connect-rpc now exit 2 before sending
