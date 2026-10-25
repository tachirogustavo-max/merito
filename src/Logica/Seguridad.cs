using System.Security.Cryptography;
using System.Text;

namespace Logica;

public static class Seguridad
{
    private const int Iteraciones = 600_000;

    public static void ValidarClave(string? clave)
    {
        if (clave is null || clave.Length < 12 || clave.Length > 128 || string.IsNullOrWhiteSpace(clave))
            throw new ReglaException("Usa una contraseña de entre 12 y 128 caracteres.");
    }

    public static void ConfirmarClave(string nueva, string repetida)
    {
        if (nueva != repetida) throw new ReglaException("Las contraseñas no coinciden.");
        ValidarClave(nueva);
    }

    public static string DerivarClave(string clave)
    {
        ValidarClave(clave);
        var sal = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(clave, sal, Iteraciones, HashAlgorithmName.SHA256, 32);
        return $"pbkdf2${Iteraciones}${Convert.ToBase64String(sal)}${Convert.ToBase64String(hash)}";
    }

    public static bool VerificarClave(string? clave, string? almacenada)
    {
        if (clave is null || clave.Length > 128 || almacenada is null) return false;
        try
        {
            var partes = almacenada.Split('$');
            if (partes.Length != 4 || partes[0] != "pbkdf2" || !int.TryParse(partes[1], out var iteraciones)
                || iteraciones is < 100_000 or > 1_000_000) return false;
            var calculado = Rfc2898DeriveBytes.Pbkdf2(clave, Convert.FromBase64String(partes[2]),
                iteraciones, HashAlgorithmName.SHA256, 32);
            return CryptographicOperations.FixedTimeEquals(calculado, Convert.FromBase64String(partes[3]));
        }
        catch (FormatException) { return false; }
    }

    public static string NuevoToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    // Para escribirlo a mano desde una hoja: 16 caracteres sin los que se confunden (0, O, 1, I, L), unos 79 bits de azar.
    private const string AlfabetoCodigo = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    public const int LargoCodigo = 16;

    // Seis cifras para el correo: pocas combinaciones, por eso vence en minutos y admite pocos intentos.
    public static string NuevoCodigoCorreo() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);

    public static string NuevoCodigoAcceso()
    {
        var letras = Enumerable.Range(0, LargoCodigo).Select(_ => AlfabetoCodigo[RandomNumberGenerator.GetInt32(AlfabetoCodigo.Length)]).ToArray();
        return string.Join("-", Enumerable.Range(0, LargoCodigo / 4).Select(i => new string(letras, i * 4, 4)));
    }
    public static string Huella(string valor) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(valor)));
}
