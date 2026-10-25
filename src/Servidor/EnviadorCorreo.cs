using Logica;

namespace Servidor;

public interface ITransporteCorreo
{
    bool Disponible { get; }
    Task Enviar(string para, string asunto, string texto, string html, CancellationToken ct);
}

public sealed class EnviadorCorreo(ITransporteCorreo transporte) : IEnviadorCorreo
{
    public bool Disponible => transporte.Disponible;

    public Task EnviarCodigoAcceso(string para, string nombres, string codigo, bool registrada, CancellationToken ct)
    {
        var (asunto, texto, html) = Mensajes.CodigoAcceso(nombres, codigo, registrada);
        return transporte.Enviar(para, asunto, texto, html, ct);
    }

    public Task EnviarPrueba(string para, string quien, CancellationToken ct)
    {
        var (asunto, texto, html) = Mensajes.Prueba(quien);
        return transporte.Enviar(para, asunto, texto, html, ct);
    }
}
