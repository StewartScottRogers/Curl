# Libcurl

The libcurl-faithful programming interface to Curl (ADR-0506): named `Libcurl`, with no `Curl.`
prefix, to match upstream's library name. A naming exception, with `Curl.Console` and `Curl.Api`.
Root namespace `Libcurl`; each area is a folder and namespace (`Libcurl/Global` is `Libcurl.Global`),
with its tests in the same folder of `Libcurl.UnitTests`.

`Global` holds `CurlGlobal` (`curl_global_init`, `_init_mem`, `_sslset`, `_trace`) and `CurlLibrary`,
the object `Init` returns over an injected `ICurlEngine`; its `DisposeAsync` is `curl_global_cleanup`
(ADR-0555). `CurlLibrary` is `partial`: each handle area adds its public creating method
(`curl_easy_init` and the rest) as a `CurlLibrary` part in its own folder, over the internal
`Create*` methods in `Global`. Tests that count inits use `CurlGlobal.InitCounting` with their own
`GlobalInitCounter`, never the process-wide count, since tests run in parallel.

Reference `Curl.Api.UnitLibrary` only. Never reference `Curl.Api`, a protocol library, or
`Curl.Engine.UnitLibrary` before the task that adds the engine-building overloads.

Held to the four quality gates in the root `CLAUDE.md`. Its tests live in `Libcurl.UnitTests`,
which sorts after every `Curl.*` project.
