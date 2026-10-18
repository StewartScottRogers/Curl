namespace Libcurl.Global;

/// <summary>
/// <c>curl_strdup_callback</c>, the string duplicator <see cref="CurlGlobal.InitMemory"/> calls for the
/// allocations of its easy handles (ADR-0664).
/// </summary>
/// <param name="text">The zero-terminated string to copy.</param>
/// <returns>The copy.</returns>
public delegate nint CurlStrdupCallback(nint text);
