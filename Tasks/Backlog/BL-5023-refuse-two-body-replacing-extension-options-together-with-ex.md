---
id: BL-5023
title: Refuse two body-replacing extension options together with exit 2
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.Composition.UnitLibrary/ExtensionTransferHookCatalogue.cs, Curl.Extension.Composition.UnitLibrary/BodyReplacingHookExclusivity.cs, Curl.Extension.Composition.UnitTests/BodyReplacingHookExclusivityTests.cs, Documentation/Planning/Decisions/ADR-1026-graphql-is-a-transfer-hook-extension-with-four-options.md, Documentation/Planning/Decisions/ADR-1028-jsonrpc-sends-calls-named-in-one-option-value-and-pairs-replies-by-id.md]
requirement: none
created: 2026-11-12
completed:
---
# BL-5023 — Refuse two body-replacing extension options together with exit 2

## Goal

Giving two body-replacing extension options in one option group (for example `--graphql` with `--jsonrpc`) exits 2 with a message naming both options and sends nothing.

## Context

From the outside fitness review of 2026-11-12 (commit 2d2ad7f8b, `preview` curl-win-x64.zip), finding EXT-043 (Low, Confirmed). Data loss / silent wrong operation: yes (one requested operation is dropped without notice). The review names `--graphql` with `--jsonrpc`; `--connect-rpc` and `--grpc-web` likely behave the same (unconfirmed).

**Repro.** Create `q.graphql` containing `{ hello }`, then run `curl -s --graphql q.graphql --jsonrpc 'a [1]' http://127.0.0.1:48000/echo` against a server that echoes the request.

**Expected.** The port refuses other conflicting extension options with exit 2 before any request: `--webdav-list and --webdav-sync cannot be used together` (`WebDavToolsExtension.BothOptionsMessage`, via `RefusedWebDavTransferHook`), and ADR-1075 says giving two OAuth2 flows is an error. ADR-1026 (GraphQL) and ADR-1028 (JSON-RPC) each say their body replaces the request body, which cannot both be true.

**Actual.** One POST is sent with body `{"jsonrpc":"2.0","method":"a","params":[1],"id":1}` and GraphQL's header `Accept: application/graphql-response+json, application/json;q=0.9`. curl exits 0 with no warning.

**Where to start.** `Curl.Extension.Composition.UnitLibrary/ExtensionTransferHookCatalogue.cs` has no exclusivity check between hooks that each replace the body. The catalogue is a one-entry-per-line shared list (ADR-1007), so put the check in a new file in `Curl.Extension.Composition.UnitLibrary` (for example `BodyReplacingHookExclusivity.cs`) that `ExtensionTransferHookCatalogue.Entries` consumers or the catalogue calls, and keep the list one entry per line. Body-replacing options to cover: `--graphql`, `--jsonrpc`, `--connect-rpc`, `--grpc-web`; confirm the full set in the extensions' ADRs and `GraphQlOptions.cs`, `JsonRpcExtension.cs`, `ConnectRpcExtension.cs`, `GrpcWebExtension.cs`.

## Acceptance criteria

- [ ] A new `Curl.Extension.Composition.UnitTests/BodyReplacingHookExclusivityTests.cs` shows `--graphql` with `--jsonrpc` gives a transfer that fails with exit 2 (`CurlExitCode.FailedInit`) and a message naming both options (for example `--graphql and --jsonrpc cannot be used together`) without the wrapped handler being called (no request sent).
- [ ] The same test class covers every other pair among `--graphql`, `--jsonrpc`, `--connect-rpc` and `--grpc-web`, and the message names the two options given.
- [ ] Regression: each of those options alone still activates its hook and sends its body, and a body-replacing option together with a non-body option (for example `--har`) is not refused.
- [ ] The exclusivity is stated in ADR-1026 and ADR-1028 (and the ConnectRpc and GrpcWeb ADRs if they exist) or in one new ADR marked "Decided by Claude under Stewart's delegation".
- [ ] `--ai-help` stays right: the extension option text in `Curl.Cli.UnitLibrary`/`Curl.Console` help for these options says they cannot be combined, and a test in the existing `--ai-help` tests covers it if the help text changes.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). The unit tests need no server. Decision (curl-compatible simplest thing): refuse with exit 2, as the WebDAV pair does. Find where `--ai-help` text for these options lives with Grep before editing and add that file to `touches`; it is not named here because it was not located.

## Log

- 2026-11-12: Created.
