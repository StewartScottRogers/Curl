namespace Curl.Extended.Protocol.AzureStorage.Bodies;

/// <summary>Reports a body that is well-formed XML or JSON but lacks a value it must have, or holds one of the wrong kind.</summary>
/// <param name="message">What the body lacks.</param>
internal sealed class AzureStorageBodyException(string message) : Exception(message);
