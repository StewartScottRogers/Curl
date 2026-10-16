# ADR-0542 — The curl tool's settings and the engine-wide state reach `ManagedEasyTransfer`

- **Status:** Accepted
- **Date:** 2026-10-15

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2359.

## Context

ADR-0541 left `ManagedEasyTransfer` running each transfer with libcurl's defaults for the curl
tool's own settings (`EngineTransferSettings`), no upload source and a fresh state per perform. To
route `Curl.Console`'s serial transfers through `IEasyTransfer.PerformAsync` byte for byte (BL-2318),
every setting the runner hands `TransferContextFactory` and `TransferPerformer` today must reach the
easy handle, and what the tool shares across a run's transfers must be shared across an engine's
handles.

## Decision

- **Where libcurl has a `CURLOPT_*`, the easy option carries it.** `TransferSettingsOf` maps
  `CURLOPT_PROTOCOLS_STR` and `CURLOPT_REDIR_PROTOCOLS_STR` (`EasyNetworkOptions.Protocols` and
  `RedirectProtocols`, comma-separated, case-insensitive) to `--proto` and `--proto-redir`,
  `CURLOPT_DIRLISTONLY` (`EasyTransferOptions.DirListOnly`) to `-l`, and `CURLOPT_PROXYHEADER`
  (`EasyProxyOptions.ProxyHeaders`) to `--proxy-header`, beside ADR-0541's verify-peer, key password,
  crypto engine, tunnel and Unix socket. `--fail` is `CURLOPT_FAILONERROR`
  (`EasyBehaviourOptions.FailOnError`), which `HttpRequestOptionsMapping` already reads. `-G` and
  `--url-query` are the tool's: it builds the URL with the query before setting `CURLOPT_URL`, so the
  engine's URL already carries it. No new option is added to `Curl.Api.UnitLibrary`.
- **What is the tool's alone reaches the engine through an internal hook, not a public option.**
  `ManagedEasyTransfer.ToolSettings` is an `EngineTransferSettings?` for `Curl.Console` (which sees
  the engine's internals) to set from `EasyOptionsFromCommandLine.TransferSettingsOf` once its runner performs through the engine (BL-2318); when set it
  replaces `TransferSettingsOf` whole. It carries `-s`, `-S`, `--fail-with-body`,
  `--create-file-mode`, `--pass`, `--ech`'s refusal, the `-d` copy, `-U`, the `--ssl*` level,
  `--ftp-ssl-control`, `--upload-flags`, `--happy-eyeballs-timeout-ms` and the `-G` query exactly as
  the runner resolves them today, so the two paths cannot drift. `Curl.Api` and `Libcurl` handles never
  set it. `Duplicate` copies it and `Reset` clears it, as they treat options.
- **Uploads come through the read callback.** Under `CURLOPT_UPLOAD` (`EasyTransferOptions.Upload`) the
  handler reads `CURLOPT_READFUNCTION` through `CallbackReadStream`; a return of 0 ends the upload,
  one below 0 or past the buffer fails the read with libcurl's `read function returned funny value`
  (`CURL_READFUNC_ABORT` and `CURL_READFUNC_PAUSE` are not honoured yet). The engine has no standard
  input of its own: with no read callback the upload is empty, and the tool hands standard input or a
  `-T` file over as a read callback. The `-C` offset is `CURLOPT_RESUME_FROM_LARGE`
  (`EasyTransferOptions.ResumeFrom`), as ADR-0541 already reads it.
- **Engine-wide state is one `ManagedEngineSharedState` per engine.** It holds the connection, TLS
  session and DNS caches, one `CookieStore`, one `HstsTransferPolicy` and one `TransferPerformer`.
  Every handle's HTTP handler keeps cookies in the shared store whenever its options turn the cookie
  engine on (`EasyHttpOptions.Cookie`, `CookieFiles` or `CookieJar`, as `CookieEngine.FromHttpOptions`
  decides), every context gets the shared HSTS cache, every dispatcher pools through the shared
  connection cache, and each perform takes the next `%{xfer_id}` from the shared performer
  (`TransferInfo.TransferId`). This is the curl tool's run-wide sharing, which libcurl itself gives
  only through a share handle; the engine is what `Curl.Console` runs one command line on, so it
  shares as the tool does. Nothing is static, so two engines still share nothing.
- **curl's easy error text is supplied, not owned.** `CurlEasyErrorText` stays in `Curl.Console`,
  where `Gap/` tooling reads it by path; `ManagedCurlEngine`'s internal constructor takes it and hands
  it to the shared performer. The public constructor gives an empty text.

## Consequences

`ManagedEasyTransferTests` show `--fail`, `--proto`, `-G` and a read-callback upload giving the same
request bytes and `CurlCode` through the easy handle as through `TransferContextFactory` and
`TransferPerformer`, and two handles of one engine sharing a cookie and a pooled connection. Still
missing before BL-2318 can switch the runner over: loading the `-b` files, writing the `-c` jar, the
`--alt-svc` and `--hsts` files, the `%{conn_id}` numbering and the diagnostic log through the easy
handle (BL-2362); trace events and progress (BL-2360).
