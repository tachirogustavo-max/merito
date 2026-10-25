namespace Logica;

public sealed record ConsultaPagina(string Buscar, int Desde, int Cantidad)
{
    public const int CantidadPredeterminada = 100;
    public const int CantidadMaxima = 200;

    public static ConsultaPagina Crear(string? buscar, int? desde, int? cantidad)
    {
        var texto = (buscar ?? "").Trim();
        if (texto.Length > 100 || texto.Any(char.IsControl)) throw new ReglaException("La búsqueda admite hasta 100 caracteres.");
        var inicio = desde ?? 0;
        var tramo = cantidad ?? CantidadPredeterminada;
        if (inicio < 0 || tramo is < 1 or > CantidadMaxima)
            throw new ReglaException($"Cada página admite de 1 a {CantidadMaxima} registros.");
        return new ConsultaPagina(texto, inicio, tramo);
    }

    /// <summary>Patrón LIKE con los comodines del usuario escapados.</summary>
    public string Patron => "%" + Buscar.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_") + "%";
}
