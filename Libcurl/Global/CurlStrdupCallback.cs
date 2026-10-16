namespace Libcurl.Global;

/// <summary>
/// <c>curl_strdup_callback</c>, the string duplicator <see cref="CurlGlobal.InitMemory"/> accepts and
/// ignores: the managed engine copies strings through the .NET runtime, never through it.
/// </summary>
/// <param name="text">The zero-terminated string to copy.</param>
/// <returns>The copy.</returns>
public delegate nint CurlStrdupCallback(nint text);
