using Logica;
using Servidor;

internal static class ComprobacionesArquitectura
{
    public static async Task Ejecutar(Action<bool, string> comprobar)
    {
        (TipoErrorRegla Tipo, int Estado)[] respuestas =
        [
            (TipoErrorRegla.DatosInvalidos, 400),
            (TipoErrorRegla.NoAutenticado, 401),
            (TipoErrorRegla.AccesoDenegado, 403),
            (TipoErrorRegla.NoEncontrado, 404),
            (TipoErrorRegla.Conflicto, 409),
            (TipoErrorRegla.LimiteIntentos, 429),
            (TipoErrorRegla.ServicioExternoNoDisponible, 502)
        ];
        foreach (var (tipo, estado) in respuestas)
            comprobar(ErroresHttp.Estado(tipo) == estado, $"{tipo} responde {estado}");
        comprobar(ErroresHttp.Estado((TipoErrorRegla)int.MaxValue) == 500, "un tipo de error desconocido responde 500");
        comprobar(new ReglaException("mensaje").Tipo == TipoErrorRegla.DatosInvalidos, "ReglaException sin tipo es DatosInvalidos");
        comprobar(typeof(ServicioIdentidad).Assembly.GetReferencedAssemblies().All(a =>
            a.Name is not ("Datos" or "Servidor" or "Escritorio" or "MySqlConnector")), "Lógica no depende de Datos, Servidor ni Escritorio");

        var transporte = new TransportePrueba();
        IEnviadorCorreo correo = new EnviadorCorreo(transporte);
        using var cancelacion = new CancellationTokenSource();
        foreach (var registrada in new[] { false, true })
        {
            await correo.EnviarCodigoAcceso("persona@prueba.test", "Ana <&>", "123456", registrada, cancelacion.Token);
            comprobar(transporte.Para == "persona@prueba.test" && transporte.Asunto.Contains("123456")
                && transporte.Texto.Contains("123456") && transporte.Html.Contains("Ana &lt;&amp;&gt;")
                && !transporte.Html.Contains("Ana <&>") && transporte.Cancelacion == cancelacion.Token,
                $"el correo de {(registrada ? "recuperación" : "registro")} lleva el código y escapa el nombre en el HTML");
            comprobar(transporte.Texto.Contains(registrada ? "elige una contraseña nueva" : "crea tu contraseña"),
                "registro y recuperación tienen textos distintos");
        }
        await correo.EnviarPrueba("persona@prueba.test", "Administración", cancelacion.Token);
        comprobar(transporte.Asunto == "¡Prueba de correo de Mérito!" && transporte.Texto.Contains("Administración"),
            "el correo de prueba nombra a quien lo pidió");
        transporte.Disponible = false;
        comprobar(!correo.Disponible, "sin transporte de correo, Disponible es falso");
        transporte.Error = new ReglaException("fallo de envío", TipoErrorRegla.ServicioExternoNoDisponible);
        try
        {
            await correo.EnviarCodigoAcceso("persona@prueba.test", "Ana", "123456", false, cancelacion.Token);
            comprobar(false, "un fallo de envío lanza el error");
        }
        catch (ReglaException error)
        {
            comprobar(ReferenceEquals(error, transporte.Error), "el error de envío llega sin cambios a Lógica");
        }
    }

    private sealed class TransportePrueba : ITransporteCorreo
    {
        public bool Disponible { get; set; } = true;
        public string Para { get; private set; } = "";
        public string Asunto { get; private set; } = "";
        public string Texto { get; private set; } = "";
        public string Html { get; private set; } = "";
        public CancellationToken Cancelacion { get; private set; }
        public ReglaException? Error { get; set; }

        public Task Enviar(string para, string asunto, string texto, string html, CancellationToken ct)
        {
            if (Error is { } error) throw error;
            (Para, Asunto, Texto, Html, Cancelacion) = (para, asunto, texto, html, ct);
            return Task.CompletedTask;
        }
    }
}
