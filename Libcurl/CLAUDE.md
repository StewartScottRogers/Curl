# Libcurl

The libcurl-faithful programming interface to Curl (ADR-0506): named `Libcurl`, with no `Curl.`
prefix, to match upstream's library name. A naming exception, with `Curl.Console` and `Curl.Api`.
Root namespace `Libcurl`; each area is a folder and namespace (`Libcurl/Global` is `Libcurl.Global`),
with its tests in the same folder of `Libcurl.UnitTests`.

`Global` holds `CurlGlobal` (`curl_global_init`, `_init_mem`, `_sslset`, `_trace`) and `CurlLibrary`,
the object `Init` returns over an injected `ICurlEngine`; its `DisposeAsync` is `curl_global_cleanup`
(ADR-0556). `CurlLibrary` is `partial`: each handle area adds its public creating method
(`curl_easy_init` and the rest) as a `CurlLibrary` part in its own folder, over the internal
`Create*` methods in `Global`. Tests that count inits use `CurlGlobal.InitCounting` with their own
`GlobalInitCounter`, never the process-wide count, since tests run in parallel.

`Options` holds the options table (ADR-0560): `CurlOption` and `CurlOptions` (`curl_easy_option_by_name`,
`_by_id`, `_next`) generated from curl 8.22.0's `lib/easyoptions.c` and `curl.h`, and the internal
`EasyOptionDispatch` `SetOption` goes through. Each option group adds its setter rows by implementing
its `Add<Group>Setters` partial method in an `EasyOptionSetters` part in `Options/<Group>`, keeping the
namespace `Libcurl.Options`; an option no group sets yet answers `NotBuiltIn`.

`Misc/ErrorText` holds `CurlErrorText`: `Multi`, `Share` and `Url` are `curl_multi_strerror`,
`curl_share_strerror` and `curl_url_strerror` with upstream's test1538 strings and fallbacks, and
`Header` is the header API's text, which upstream has no function for (ADR-0562). Each is a table
lookup, so a new upstream code is one appended string. `curl_easy_strerror` is still
`Curl.Console`'s `CurlEasyErrorText` until BL-2450 moves it.

`Easy` holds `CurlEasy`, the easy handle `CurlLibrary.EasyInit` (`curl_easy_init`) returns over the
engine's `IEasyTransfer` (ADR-0509): `SetOption` goes through `EasyOptionDispatch` and writes the
changed `EasyOptions` back to the transfer only on `Ok`; `PerformAsync` is the native shape, and
`Perform` blocks on it, the one sanctioned sync-over-async bridge in Libcurl. `Dispose` and
`DisposeAsync` are `curl_easy_cleanup` and dispose the transfer once; a member used afterwards throws
`ObjectDisposedException`.

Reference `Curl.Api.UnitLibrary` only. Never reference `Curl.Api`, a protocol library, or
`Curl.Engine.UnitLibrary` before the task that adds the engine-building overloads.

Held to the four quality gates in the root `CLAUDE.md`. Its tests live in `Libcurl.UnitTests`,
which sorts after every `Curl.*` project.
