using System.Net;
using System.Net.Sockets;

namespace Logica;

/// <summary>
/// Política de red: dentro de la red local se admite HTTP. Una dirección pública exige HTTPS,
/// porque ahí las contraseñas y sesiones viajarían por redes ajenas a la institución.
/// </summary>
public static class RedLocal
{
    public static bool EsPrivada(IPAddress direccion)
    {
        if (IPAddress.IsLoopback(direccion)) return true;
        if (direccion.IsIPv4MappedToIPv6) direccion = direccion.MapToIPv4();
        if (direccion.AddressFamily == AddressFamily.InterNetworkV6)
            return direccion.IsIPv6LinkLocal || direccion.IsIPv6SiteLocal || direccion.IsIPv6UniqueLocal;
        var b = direccion.GetAddressBytes();
        return b[0] == 10 || (b[0] == 172 && b[1] is >= 16 and <= 31) || (b[0] == 192 && b[1] == 168) || (b[0] == 169 && b[1] == 254);
    }

    /// <summary>Un nombre sin dominio («servidor-notas») o terminado en .local o .lan se resuelve dentro de la red.</summary>
    public static bool EsDireccionLocal(Uri direccion)
    {
        if (direccion.IsLoopback) return true;
        if (IPAddress.TryParse(direccion.Host.Trim('[', ']'), out var ip)) return EsPrivada(ip);
        var host = direccion.Host.ToLowerInvariant();
        return !host.Contains('.') || host.EndsWith(".local", StringComparison.Ordinal) || host.EndsWith(".lan", StringComparison.Ordinal);
    }
}
