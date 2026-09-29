namespace Curl.Kerberos;

/// <summary>
/// Thrown when a Kerberos encryption type cannot encrypt, decrypt or make a key.
/// <see cref="Error" /> says why.
/// </summary>
public sealed class KerberosCryptographyException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KerberosCryptographyException" /> class.
    /// </summary>
    /// <param name="error">Why the operation failed.</param>
    public KerberosCryptographyException(KerberosCryptographyError error)
        : base($"Kerberos cryptography failed: {error}.")
    {
        Error = error;
    }

    /// <summary>Gets why the operation failed.</summary>
    public KerberosCryptographyError Error { get; }
}
