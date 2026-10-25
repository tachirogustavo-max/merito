namespace Escritorio.Presentacion;

/// <summary>
/// El formato del código de sección visto como opciones (prefijo, semestre, letra, carrera y separador),
/// para que nadie tenga que escribir marcadores. El servidor sigue recibiendo el formato con marcadores.
/// </summary>
public sealed record PiezasFormato(string Prefijo, int Semestre, string Letra, bool Carrera, string Separador)
{
    // Semestre: 0 no lleva, 1 sin ceros, 2 con dos cifras.
    public static string Armar(string prefijo, int semestre, string letra, bool carrera, string separador)
    {
        var partes = new List<string>();
        if (Limpio(prefijo) is { Length: > 0 } inicio) partes.Add(inicio);
        if (semestre > 0) partes.Add((semestre == 2 ? "{semestre:00}" : "{semestre}") + Limpio(letra));
        if (carrera) partes.Add("{carrera}");
        partes.Add("{seccion}");
        return string.Join(Limpio(separador), partes);
    }

    // Las llaves son de los marcadores: en un texto escrito por la persona no van.
    private static string Limpio(string texto) => texto.Trim().Replace("{", "").Replace("}", "");

    /// <summary>Reconoce un formato armado con estas opciones; si no, devuelve el de fábrica.</summary>
    public static PiezasFormato Leer(string formato)
    {
        if (formato == "{seccion}") return new("", 0, "", false, "-");
        foreach (var separador in new[] { "-", "/", ".", "_", " " })
        {
            var partes = formato.Split(separador);
            if (partes.Length < 2 || partes[^1] != "{seccion}") continue;
            var (prefijo, semestre, letra, carrera, valido) = ("", 0, "", false, true);
            for (var i = 0; i < partes.Length - 1 && valido; i++)
            {
                var parte = partes[i];
                if (parte == "{carrera}" && !carrera) carrera = true;
                else if (parte.StartsWith("{semestre:00}") && semestre == 0 && !carrera) (semestre, letra) = (2, parte["{semestre:00}".Length..]);
                else if (parte.StartsWith("{semestre}") && semestre == 0 && !carrera) (semestre, letra) = (1, parte["{semestre}".Length..]);
                else if (i == 0 && !parte.Contains('{')) prefijo = parte;
                else valido = false;
            }
            if (valido && !letra.Contains('{')) return new(prefijo, semestre, letra, carrera, separador);
        }
        return new("", 2, "S", true, "-");
    }
}
