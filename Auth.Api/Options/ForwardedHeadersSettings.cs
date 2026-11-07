using System.ComponentModel.DataAnnotations;

namespace Auth.Api.Options;

/// <summary>
/// Trusted-proxy configuration for <c>X-Forwarded-For</c>. Auth.Api is reachable only through
/// the YARP gateway, so without this every request appears to originate from the gateway
/// address and all clients share one rate-limit bucket. Trust is opt-in and limited to the
/// hops listed here: honouring the header from any caller would let a client spoof an address
/// and bypass the limit entirely.
/// </summary>
public sealed class ForwardedHeadersSettings
{
    public const string SectionName = "ForwardedHeaders";

    /// <summary>
    /// Enables the <c>UseForwardedHeaders</c> middleware. Off by default so a deployment that
    /// exposes Auth.Api directly cannot be talked into trusting a client-supplied header.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Number of proxy hops to unwind. One hop is the gateway; values above one let a
    /// client-appended entry survive the unwinding and become the effective address.
    /// </summary>
    [Range(1, 10)]
    public int ForwardLimit { get; set; } = 1;

    /// <summary>
    /// Proxy addresses whose forwarded headers are trusted. When both this and
    /// <see cref="KnownNetworks"/> are empty the framework defaults (loopback) apply, which is
    /// what a gateway running on the same host needs.
    /// </summary>
    public string[] KnownProxies { get; set; } = [];

    /// <summary>
    /// Proxy networks in CIDR notation whose forwarded headers are trusted, e.g. the container
    /// network the gateway dials from.
    /// </summary>
    public string[] KnownNetworks { get; set; } = [];
}
