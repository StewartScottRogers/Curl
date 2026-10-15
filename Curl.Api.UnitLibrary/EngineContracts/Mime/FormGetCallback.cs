namespace Curl.Api.EngineContracts.Mime;

/// <summary>
/// <c>curl_formget_callback</c>: receives the next piece of a serialised form. <c>arg</c> is the
/// delegate's closure.
/// </summary>
/// <param name="buffer"><c>buf</c> and <c>len</c>: the piece, valid only during the call.</param>
/// <returns>The bytes taken; anything but <paramref name="buffer"/>'s length stops the serialisation.</returns>
public delegate long FormGetCallback(ReadOnlySpan<byte> buffer);
