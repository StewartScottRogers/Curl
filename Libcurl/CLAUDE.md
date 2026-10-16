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
namespace `Libcurl.Options`; an option no group sets yet answers `NotBuiltIn`. `Options/Behaviour` sets
the `EasyBehaviourOptions` options: an on/off option is on for any value but 0, the two permission
options take 0 to 0777, and an object option given a value its field cannot hold answers
`BadFunctionArgument`, as libcurl's `setopt.c` does. `Options/Network` sets the `EasyNetworkOptions`
options with `setopt.c`'s range checks, and turns the `PROTOCOLS` bitmasks into `PROTOCOLS_STR` names
(ADR-0568); `CURLU` still answers `NotBuiltIn` until BL-2462.

`Misc/ErrorText` holds `CurlErrorText`: `Multi`, `Share` and `Url` are `curl_multi_strerror`,
`curl_share_strerror` and `curl_url_strerror` with upstream's test1538 strings and fallbacks, and
`Header` is the header API's text, which upstream has no function for (ADR-0562). Each is a table
lookup, so a new upstream code is one appended string. `curl_easy_strerror` is still
`Curl.Console`'s `CurlEasyErrorText` until BL-2450 moves it.

`Easy` holds `CurlEasy`, the easy handle `CurlLibrary.EasyInit` (`curl_easy_init`) returns over the
engine's `IEasyTransfer` (ADR-0563): `SetOption` goes through `EasyOptionDispatch` and writes the
changed `EasyOptions` back to the transfer only on `Ok`; `PerformAsync` is the native shape, and
`Perform` blocks on it, the one sanctioned sync-over-async bridge in Libcurl. `Dispose` and
`DisposeAsync` are `curl_easy_cleanup` and dispose the transfer once; a member used afterwards throws
`ObjectDisposedException`. `GetInfo` is `curl_easy_getinfo` and goes through `Info`'s dispatch.

`Multi` holds `CurlMulti`, the multi handle `CurlLibrary.MultiInit` (`curl_multi_init`) returns over
the engine's `IMultiTransfer` (ADR-0570). It is `partial`: each multi area adds its members in its own
folder under `Libcurl/Multi`, keeping the namespace `Libcurl.Multi`. `Multi/Core` has `AddHandle`,
`RemoveHandle`, `PerformAsync`, `InfoRead` and `DisposeAsync` (`curl_multi_cleanup`); it maps each
added engine transfer back to its `CurlEasy`, so `InfoRead` answers a `CurlMessage` naming the handle
the caller added. A `null` or cleaned-up `CurlEasy` answers `BadEasyHandle`, one this multi holds
already `AddedAlready`, both without calling the engine.

`Info` holds `CurlInfo` (every `CURLINFO_*` of curl 8.22.0's `curl.h` by name and number) and the
internal `EasyInfoDispatch` `CurlEasy.GetInfo` goes through (ADR-0566): an id `curl.h` lacks answers
`UnknownOption`, a known info no group reads yet `NotBuiltIn`. Each info group adds its reader rows by
implementing its `Add<Group>Readers` partial method in an `EasyInfoReaders` part in `Info/<Group>`,
keeping the namespace `Libcurl.Info`, with each reader a named method in an explicit
`new EasyInfoReader(...)` (a lambda or cached method group counts against the adding method's
complexity). `Info/Strings` reads the strings, lists, `PRIVATE` and `TLS_SSL_PTR`, which answers a
`CurlTlsSessionInfo` with the build's backend and no native pointer.

Reference `Curl.Api.UnitLibrary` only. Never reference `Curl.Api`, a protocol library, or
`Curl.Engine.UnitLibrary` before the task that adds the engine-building overloads.

Held to the four quality gates in the root `CLAUDE.md`. Its tests live in `Libcurl.UnitTests`,
which sorts after every `Curl.*` project.
