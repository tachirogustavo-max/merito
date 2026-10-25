using System.Windows;
using System.Windows.Threading;
using Escritorio.Presentacion;
using Escritorio.Servicios;

namespace Escritorio;

public partial class App : Application
{
    internal bool MostrarVentanaInicial { get; set; } = true;
    private bool mostrandoFallo;

    protected override void OnStartup(StartupEventArgs e)
    {
        var cultura = System.Globalization.CultureInfo.GetCultureInfo("es-VE");
        System.Globalization.CultureInfo.CurrentCulture = cultura;
        System.Globalization.CultureInfo.CurrentUICulture = cultura;
        FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement), new FrameworkPropertyMetadata(
            System.Windows.Markup.XmlLanguage.GetLanguage(cultura.IetfLanguageTag)));

        // Con un tema de alto contraste de Windows, sus colores reemplazan a los de la marca.
        if (SystemParameters.HighContrast || Environment.GetEnvironmentVariable("MERITO_ALTO_CONTRASTE") == "1")
            Resources.MergedDictionaries.Insert(1, new ResourceDictionary { Source = new Uri("pack://application:,,,/Merito;component/Tema/AltoContraste.xaml") });

        DispatcherUnhandledException += FalloEnPantalla;
        // Tareas en segundo plano cuyo error nadie esperó: se registran sin cerrar el programa.
        TaskScheduler.UnobservedTaskException += (_, error) =>
        {
            RegistroErrores.Registrar(error.Exception);
            error.SetObserved();
        };
        // Último recurso: el proceso va a terminar, al menos queda registrado.
        AppDomain.CurrentDomain.UnhandledException += (_, error) =>
        {
            if (error.ExceptionObject is Exception fallo) RegistroErrores.Registrar(fallo);
        };
        base.OnStartup(e);
        if (MostrarVentanaInicial) new MainWindow().Show();
    }

    private void FalloEnPantalla(object sender, DispatcherUnhandledExceptionEventArgs error)
    {
        error.Handled = true;
        var referencia = RegistroErrores.Registrar(error.Exception);
        // Si ya hay un aviso abierto no se encadenan ventanas.
        if (mostrandoFallo) return;
        mostrandoFallo = true;
        try
        {
            var ventana = Windows.OfType<Window>().FirstOrDefault(v => v.IsActive) ?? MainWindow;
            Dialogo.Informar(ventana, "Algo salió mal",
                "Ocurrió un error inesperado. Comprueba el último guardado antes de continuar; si vuelve a ocurrir, comunica la referencia a administración.",
                Tono.Peligro, referencia);
        }
        catch (Exception)
        {
            // Si el propio aviso no puede dibujarse queda el mensaje del sistema.
            MessageBox.Show($"Ocurrió un error inesperado. Referencia: {referencia}.", "Mérito", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { mostrandoFallo = false; }
    }
}
