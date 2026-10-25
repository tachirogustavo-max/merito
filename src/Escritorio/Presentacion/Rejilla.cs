using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace Escritorio.Presentacion;

/// <summary>
/// Tabla de solo lectura con filtro de texto que sobrevive a cada recarga.
/// Tab entra y sale de la tabla una sola vez; Enter o doble clic sobre una fila la activan.
/// </summary>
public sealed class Rejilla : DataGrid
{
    private string filtro = "";
    public event EventHandler? Activada;

    /// <summary>Columnas en una línea: «Título=Campo; Título=Campo». Los anchos y formatos salen del nombre del campo.</summary>
    public static readonly DependencyProperty ColumnasProperty = DependencyProperty.Register(nameof(Columnas), typeof(string), typeof(Rejilla),
        new PropertyMetadata(null, (o, e) => ((Rejilla)o).Definir(Leer((string?)e.NewValue))));
    public string? Columnas { get => (string?)GetValue(ColumnasProperty); set => SetValue(ColumnasProperty, value); }

    /// <summary>Para textos que hay que leer enteros, como los errores de un Excel: pasan a la línea siguiente en vez de cortarse.</summary>
    public static readonly DependencyProperty AjustarTextoProperty = DependencyProperty.Register(nameof(AjustarTexto), typeof(bool), typeof(Rejilla),
        new PropertyMetadata(false, (o, _) => ((Rejilla)o).Definir(Leer(((Rejilla)o).Columnas))));
    public bool AjustarTexto { get => (bool)GetValue(AjustarTextoProperty); set => SetValue(AjustarTextoProperty, value); }

    /// <summary>Para tablas sin desplazamiento propio dentro de una lista que sí se desplaza.</summary>
    public static readonly DependencyProperty CedeRuedaProperty = DependencyProperty.Register(nameof(CedeRueda), typeof(bool), typeof(Rejilla),
        new PropertyMetadata(false));
    public bool CedeRueda { get => (bool)GetValue(CedeRuedaProperty); set => SetValue(CedeRuedaProperty, value); }

    private static IEnumerable<(string Titulo, string Campo)> Leer(string? texto) =>
        (texto ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(par => par.Split('=', 2, StringSplitOptions.TrimEntries)).Where(p => p.Length == 2).Select(p => (p[0], p[1]));

    private static readonly Dictionary<string, double> Anchos = new()
    {
        ["Cedula"] = 130, ["Seccion"] = 130, ["SeccionesTexto"] = 140, ["Periodo"] = 100, ["Fila"] = 90, ["Rol"] = 150, ["Activo"] = 120, ["EstadoCuenta"] = 150, ["Cerrado"] = 130, ["Estado"] = 160, ["Peso"] = 120, ["Orden"] = 90,
        ["Matriculados"] = 100, ["SemestreTexto"] = 110, ["Codigo"] = 180, ["Pendientes"] = 110, ["Nota"] = 110, ["AportePorcentual"] = 120, ["AporteSobreVeinte"] = 120, ["OcurridoEn"] = 160, ["Actor"] = 200, ["Uc"] = 80, ["AprobatoriaTexto"] = 130, ["Compartida"] = 150, ["Accion"] = 280, ["Materias"] = 160
    };
    private static readonly HashSet<string> Numericos = ["Fila", "Uc", "Peso", "Orden", "Matriculados", "Pendientes", "Nota", "AportePorcentual", "AporteSobreVeinte"];

    public void Definir(IEnumerable<(string Titulo, string Campo)> columnas)
    {
        Columns.Clear();
        var app = Application.Current;
        foreach (var (titulo, campo) in columnas)
        {
            var ancho = Anchos.TryGetValue(campo, out var fijo) ? new DataGridLength(fijo)
                      : new DataGridLength(campo is "Detalle" ? 3 : campo is "Materia" or "Evaluacion" or "Problema" or "NombreCompleto" or "Docente" or "Estudiante" ? 2 : 1, DataGridLengthUnitType.Star);
            var minimo = ancho.IsAbsolute ? ancho.Value : 120;
            if (campo is "Activo" or "Cerrado" or "Estado" or "EstadoCuenta")
            {
                Columns.Add(new DataGridTemplateColumn { Header = titulo, Width = ancho, MinWidth = minimo, CellTemplate = PlantillaInsignia(campo), SortMemberPath = campo });
                continue;
            }
            var enlace = new Binding(campo);
            if (campo is "Nota" or "AportePorcentual" or "AporteSobreVeinte") { enlace.TargetNullValue = "Pendiente"; enlace.StringFormat = "{0:0.####}"; }
            if (campo == "Peso") enlace.StringFormat = "{0:0.##} %";
            if (campo == "Cedula") enlace.Converter = new ConvertidorCedula();
            if (campo == "Rol") enlace.Converter = (IValueConverter)app.FindResource("Capital");
            if (campo == "OcurridoEn") enlace.Converter = (IValueConverter)app.FindResource("Fecha");
            var numerico = Numericos.Contains(campo);
            var estilo = (Style)app.FindResource(numerico ? "TextoCeldaNumero" : campo is "Cedula" or "OcurridoEn" ? "TextoCeldaTenue" : "TextoCelda");
            if (AjustarTexto && !numerico)
                estilo = new Style(typeof(TextBlock), estilo)
                {
                    Setters = { new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap), new Setter(TextBlock.TextTrimmingProperty, TextTrimming.None),
                        new Setter(MarginProperty, new Thickness(0, 11, 0, 11)) }
                };
            var columna = new DataGridTextColumn { Header = titulo, Binding = enlace, Width = ancho, MinWidth = minimo, ElementStyle = estilo };
            if (numerico) columna.HeaderStyle = (Style)app.FindResource("CabeceraNumerica");
            Columns.Add(columna);
        }
    }

    private static DataTemplate PlantillaInsignia(string campo)
    {
        var borde = new FrameworkElementFactory(typeof(Border));
        borde.SetResourceReference(StyleProperty, "Insignia");
        borde.SetBinding(Border.BackgroundProperty, new Binding(campo) { Converter = new ConvertidorInsignia { Modo = "fondo", Campo = campo } });
        var texto = new FrameworkElementFactory(typeof(TextBlock));
        texto.SetBinding(TextBlock.TextProperty, new Binding(campo) { Converter = new ConvertidorInsignia { Modo = "etiqueta", Campo = campo } });
        texto.SetBinding(TextBlock.ForegroundProperty, new Binding(campo) { Converter = new ConvertidorInsignia { Modo = "texto", Campo = campo } });
        texto.SetValue(TextBlock.FontSizeProperty, 12.5);
        texto.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        borde.AppendChild(texto);
        return new DataTemplate { VisualTree = borde };
    }

    protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
    {
        if (CedeRueda && !e.Handled)
        {
            e.Handled = true;
            (VisualTreeHelper.GetParent(this) as UIElement)?.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta) { RoutedEvent = MouseWheelEvent, Source = this });
            return;
        }
        base.OnPreviewMouseWheel(e);
    }

    public Rejilla()
    {
        IsReadOnly = true;
        SetResourceReference(StyleProperty, typeof(DataGrid));
        KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.Once);
        MouseDoubleClick += (_, e) =>
        {
            if (e.OriginalSource is DependencyObject origen && Ancestro<DataGridRow>(origen) is not null && SelectedItem is not null)
                Activada?.Invoke(this, EventArgs.Empty);
        };
    }

    public void Filtrar(string texto)
    {
        filtro = texto.Trim();
        Aplicar();
    }

    protected override void OnItemsSourceChanged(IEnumerable oldValue, IEnumerable newValue)
    {
        base.OnItemsSourceChanged(oldValue, newValue);
        Aplicar();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (IsReadOnly && e.Key == Key.Tab)
        {
            e.Handled = true;
            var direccion = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? FocusNavigationDirection.Previous : FocusNavigationDirection.Next;
            (Keyboard.FocusedElement as UIElement ?? this).MoveFocus(new TraversalRequest(direccion));
            return;
        }
        if (IsReadOnly && e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None && SelectedItem is not null && Activada is not null)
        {
            e.Handled = true;
            Activada(this, EventArgs.Empty);
            return;
        }
        base.OnPreviewKeyDown(e);
    }

    public void Enfocar()
    {
        if (Items.Count == 0 || Columns.Count == 0) { Focus(); return; }
        if (SelectedIndex < 0) SelectedIndex = 0;
        ScrollIntoView(SelectedItem);
        UpdateLayout();
        CurrentCell = new DataGridCellInfo(SelectedItem, Columns[0]);
        if (ItemContainerGenerator.ContainerFromIndex(SelectedIndex) is DataGridRow fila
            && Descendiente<DataGridCellsPresenter>(fila)?.ItemContainerGenerator.ContainerFromIndex(0) is DataGridCell celda)
            celda.Focus();
        else Focus();
    }

    private void Aplicar()
    {
        if (ItemsSource is null || CollectionViewSource.GetDefaultView(ItemsSource) is not { CanFilter: true } vista) return;
        vista.Filter = filtro.Length == 0 ? null : Coincide;
    }

    private bool Coincide(object elemento)
    {
        var comparador = CultureInfo.CurrentCulture.CompareInfo;
        const CompareOptions opciones = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;
        return elemento.GetType().GetProperties()
            .Where(p => p.PropertyType == typeof(string) && p.GetIndexParameters().Length == 0)
            .Any(p => p.GetValue(elemento) is string valor && comparador.IndexOf(valor, filtro, opciones) >= 0);
    }

    private static T? Ancestro<T>(DependencyObject? elemento) where T : DependencyObject
    {
        while (elemento is not null and not T)
            elemento = elemento is Visual ? VisualTreeHelper.GetParent(elemento) : LogicalTreeHelper.GetParent(elemento);
        return elemento as T;
    }

    private static T? Descendiente<T>(DependencyObject raiz) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(raiz); i++)
        {
            var hijo = VisualTreeHelper.GetChild(raiz, i);
            if (hijo is T encontrado) return encontrado;
            if (Descendiente<T>(hijo) is { } profundo) return profundo;
        }
        return null;
    }

}
