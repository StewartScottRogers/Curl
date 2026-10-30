using System.Numerics;

namespace Curl.Extension.PrivacyPass.Tokens;

/// <summary>A point of the P-384 group (<see cref="P384Group"/>) in affine coordinates, or the identity element.</summary>
/// <param name="X">The x-coordinate, in [0, p); zero for the identity.</param>
/// <param name="Y">The y-coordinate, in [0, p); zero for the identity.</param>
/// <param name="IsIdentity">Whether this is the identity element, the point at infinity.</param>
public sealed record P384Point(BigInteger X, BigInteger Y, bool IsIdentity = false)
{
    /// <summary>Gets the identity element, the point at infinity.</summary>
    public static P384Point Identity { get; } = new(BigInteger.Zero, BigInteger.Zero, IsIdentity: true);
}
