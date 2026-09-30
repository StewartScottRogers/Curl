using Curl.Kerberos;
using Curl.Protocol.Abstractions;

namespace Curl.Authentication;

/// <summary>
/// The hand-built Negotiate and Kerberos route of ADR-0142 (S+K): the first step gets a
/// service ticket and makes the GSS-API Kerberos initial token, wrapped for Negotiate in a
/// SPNEGO NegTokenInit offering Kerberos V5 alone, as MIT's library does; the second reads
/// the acceptor's AP-REP, from inside a NegTokenResp for Negotiate, and completes the context.
/// The credential cache is always the one used: curl's GSS-API Negotiate ignores
/// <c>-u</c>'s password off Windows. <c>--delegation</c> forwards the ticket-granting ticket
/// in the initial token, as MIT's <c>gss_init_sec_context</c> does.
/// </summary>
/// <param name="request">The mechanism, <see cref="SecurityMechanism.Negotiate" /> or <see cref="SecurityMechanism.Kerberos" />, and the acceptor.</param>
/// <param name="tickets">Gets the service ticket.</param>
/// <param name="timeProvider">Gives the authenticator's time.</param>
/// <param name="randomSource">Gives the subkey, sequence number and confounders.</param>
internal sealed class HandBuiltKerberosSecurityContext(
    SecurityContextRequest request,
    KerberosServiceTicketSource tickets,
    TimeProvider timeProvider,
    IKerberosRandomSource randomSource) : ISecurityContext
{
    private KerberosCredential? serviceTicket;
    private KerberosCredential? forwardedTicketGrantingTicket;
    private KerberosGssContext? gss;

    /// <inheritdoc />
    public bool IsCompleted => gss?.IsCompleted == true;

    /// <inheritdoc />
    public async ValueTask<SecurityContextStep> NextTokenAsync(ReadOnlyMemory<byte> incomingToken, CancellationToken cancellationToken) =>
        gss is null
            ? await InitialStepAsync(cancellationToken).ConfigureAwait(false)
            : ReplyStep(gss, incomingToken);

    /// <inheritdoc />
    public byte[]? Wrap(ReadOnlySpan<byte> message, bool encrypt) => Established().Wrap(message, encrypt);

    /// <inheritdoc />
    public byte[]? Unwrap(ReadOnlySpan<byte> wrappedMessage)
    {
        KerberosGssContext context = Established();
        try
        {
            return context.Unwrap(wrappedMessage).Message;
        }
        catch (KerberosGssException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        gss?.Dispose();
        forwardedTicketGrantingTicket?.Dispose();
        serviceTicket?.Dispose();
    }

    private static SecurityContextStep Failure(SecurityContextStatus status) => new(status, []);

    private KerberosGssContext Established() =>
        gss is { IsCompleted: true } context ? context : throw new InvalidOperationException("The security context is not established.");

    private static SecurityContextStatus StatusOf(KerberosKdcError error) =>
        error is KerberosKdcError.NoCredentials or KerberosKdcError.TicketExpired
            ? SecurityContextStatus.NoCredentials
            : SecurityContextStatus.Refused;

    /// <summary>
    /// Gets the Kerberos token inside a NegTokenResp: its <c>responseToken</c>, or
    /// <see langword="null" /> when the acceptor rejected the exchange or sent none.
    /// </summary>
    private static ReadOnlyMemory<byte>? AcceptedTokenOf(ReadOnlyMemory<byte> incomingToken)
    {
        SpnegoNegotiationResponse response = SpnegoNegotiationResponse.Decode(incomingToken);
        return response.State == SpnegoNegotiationState.Reject ? null : response.ResponseToken;
    }

    private SecurityContextStep ReplyStep(KerberosGssContext context, ReadOnlyMemory<byte> incomingToken)
    {
        try
        {
            ReadOnlyMemory<byte>? mechanismToken = request.Mechanism == SecurityMechanism.Negotiate ? AcceptedTokenOf(incomingToken) : incomingToken;
            if (mechanismToken is not { } token)
            {
                return Failure(SecurityContextStatus.Refused);
            }

            context.NextToken(token.Span);
            return new SecurityContextStep(SecurityContextStatus.Completed, []);
        }
        catch (SpnegoTokenException)
        {
            return Failure(SecurityContextStatus.MalformedToken);
        }
        catch (KerberosGssException failure)
        {
            return Failure(failure.Error == KerberosGssError.MalformedToken ? SecurityContextStatus.MalformedToken : SecurityContextStatus.Refused);
        }
    }

    private static KerberosDelegation DelegationOf(SecurityDelegation delegation) => delegation switch
    {
        SecurityDelegation.Always => KerberosDelegation.Always,
        SecurityDelegation.Policy => KerberosDelegation.Policy,
        _ => KerberosDelegation.None,
    };

    /// <summary>
    /// Gets the forwarded ticket-granting ticket when the context will delegate: always for
    /// <see cref="SecurityDelegation.Always" />, and for <see cref="SecurityDelegation.Policy" />
    /// only with an ok-as-delegate service ticket, so no KDC exchange is spent otherwise.
    /// Any failure, a ticket-granting ticket that is not forwardable among them, gives
    /// <see langword="null" /> and so no delegation, as MIT's <c>gss_init_sec_context</c> drops
    /// the delegation flag when <c>krb5_fwd_tgt_creds</c> fails (ADR-0210).
    /// </summary>
    private async Task<KerberosCredential?> ForwardedTicketGrantingTicketAsync(KerberosCredential ticket, CancellationToken cancellationToken)
    {
        bool delegates = request.Delegation == SecurityDelegation.Always
            || (request.Delegation == SecurityDelegation.Policy && ticket.Flags.HasFlag(KerberosTicketFlags.OkAsDelegate));
        if (!delegates)
        {
            return null;
        }

        try
        {
            return await tickets.GetForwardedTicketGrantingTicketAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception failure) when (failure is KerberosKdcException or KerberosFileException or KerberosConfigurationException or KerberosCryptographyException)
        {
            return null;
        }
    }

    /// <summary>
    /// Gets the service ticket and makes the initial token. The channel bindings come first,
    /// before any credential is looked at, so a server certificate whose signature names no hash
    /// fails the transfer (<see cref="HttpAuthenticationFailedException" />, exit 91) even
    /// without a ticket, as curl 8.18.0 with MIT was measured doing (BL-965).
    /// </summary>
    private async ValueTask<SecurityContextStep> InitialStepAsync(CancellationToken cancellationToken)
    {
        byte[]? channelBindings = TlsServerEndPointChannelBindings.Of(request.ServerCertificate);
        try
        {
            serviceTicket = await tickets.GetAsync(request.ServiceName, request.HostName, cancellationToken).ConfigureAwait(false);
            forwardedTicketGrantingTicket = await ForwardedTicketGrantingTicketAsync(serviceTicket, cancellationToken).ConfigureAwait(false);
            KerberosGssContextOptions options = new()
            {
                Delegation = DelegationOf(request.Delegation),
                ForwardedTicketGrantingTicket = forwardedTicketGrantingTicket,
                ChannelBindings = channelBindings,
            };
            gss = new KerberosGssContext(serviceTicket, options, timeProvider, randomSource);
        }
        catch (KerberosFileException)
        {
            return Failure(SecurityContextStatus.NoCredentials);
        }
        catch (KerberosKdcException failure)
        {
            return Failure(StatusOf(failure.Error));
        }
        catch (Exception failure) when (failure is KerberosConfigurationException or KerberosCryptographyException)
        {
            return Failure(SecurityContextStatus.Refused);
        }

        byte[] token = gss.NextToken([]);
        return new SecurityContextStep(
            SecurityContextStatus.ContinueNeeded,
            request.Mechanism == SecurityMechanism.Negotiate ? SpnegoInitialToken.Encode(SpnegoMechanism.MitKerberosMechanismTypes, token) : token);
    }
}
