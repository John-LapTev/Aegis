using Aegis.App.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Aegis.App.Views;

public partial class MainWindow : Window
{
    // Быстрое выделение перетаскиванием: зажал на квадратике-чекбоксе и ведёшь мышью — пункты по пути
    // отмечаются (или снимаются) под значение стартового. Чекбокс сам обрабатывает обычный клик; мы лишь
    // подхватываем перетаскивание поверх соседних пунктов.
    private bool _dragSelecting;
    private bool _dragMoved;
    private bool _dragValue;
    private object? _dragStart; // FindingViewModel (находки) ИЛИ DriverEntryViewModel (драйверы, правка 944)

    public MainWindow()
    {
        InitializeComponent();

        FindingsScroll.AddHandler(InputElement.PointerPressedEvent, OnFindingsPointerPressed,
            RoutingStrategies.Tunnel, handledEventsToo: true);
        FindingsScroll.AddHandler(InputElement.PointerMovedEvent, OnFindingsPointerMoved,
            RoutingStrategies.Tunnel, handledEventsToo: true);
        FindingsScroll.AddHandler(InputElement.PointerReleasedEvent, OnFindingsPointerReleased,
            RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private void OnFindingsPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Старт только при нажатии на чекбокс — иначе не мешаем обычным кликам и прокрутке.
        if (e.Source is not Visual source || !IsInsideCheckBox(source))
        {
            return;
        }

        var target = SelectableAt(e.GetPosition(FindingsScroll));
        if (target is null || !CanSelect(target))
        {
            return;
        }

        _dragSelecting = true;
        _dragMoved = false;
        _dragStart = target;
        _dragValue = !GetSelected(target); // куда поведём: к выделению или к снятию
    }

    private void OnFindingsPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_dragSelecting)
        {
            return;
        }

        if (!e.GetCurrentPoint(FindingsScroll).Properties.IsLeftButtonPressed)
        {
            _dragSelecting = false;
            return;
        }

        var target = SelectableAt(e.GetPosition(FindingsScroll));
        if (target is null || !CanSelect(target) || ReferenceEquals(target, _dragStart))
        {
            return;
        }

        // Первый сдвиг на соседний пункт = это точно перетаскивание → отмечаем и стартовый пункт.
        if (!_dragMoved && _dragStart is not null)
        {
            _dragMoved = true;
            SetSelected(_dragStart, _dragValue);
        }

        if (GetSelected(target) != _dragValue)
        {
            SetSelected(target, _dragValue);
        }
    }

    private void OnFindingsPointerReleased(object? sender, PointerReleasedEventArgs e) => _dragSelecting = false;

    private static bool IsInsideCheckBox(Visual? source)
    {
        for (var visual = source; visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is CheckBox)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Выбираемая цель под точкой: предпочтительно строка драйвера (DriverEntryViewModel), иначе находка (FindingViewModel).</summary>
    private object? SelectableAt(Point position)
    {
        for (var visual = FindingsScroll.InputHitTest(position) as Visual; visual is not null; visual = visual.GetVisualParent())
        {
            switch (visual)
            {
                case Control { DataContext: DriverEntryViewModel driver }:
                    return driver;
                case Control { DataContext: FindingViewModel finding }:
                    return finding;
            }
        }

        return null;
    }

    private static bool CanSelect(object target) => target switch
    {
        FindingViewModel f => f.CanBatchSelect,
        DriverEntryViewModel d => d.CanAct,
        _ => false,
    };

    private static bool GetSelected(object target) => target switch
    {
        FindingViewModel f => f.IsSelected,
        DriverEntryViewModel d => d.IsSelected,
        _ => false,
    };

    private static void SetSelected(object target, bool value)
    {
        switch (target)
        {
            case FindingViewModel f:
                f.IsSelected = value;
                break;
            case DriverEntryViewModel d when d.CanAct:
                d.IsSelected = value;
                break;
        }
    }
}
