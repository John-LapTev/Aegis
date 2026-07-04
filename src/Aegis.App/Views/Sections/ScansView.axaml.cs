using System;
using System.Collections.Generic;
using Aegis.App.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Aegis.App.Views.Sections;

public partial class ScansView : UserControl
{
    // Быстрое выделение перетаскиванием: зажал на квадратике-чекбоксе и ведёшь мышью — пункты по пути
    // отмечаются под значение стартового. Ведёшь ОБРАТНО по уже пройденным — выделение с них снимается
    // (модель «след от старта», запрос Ивана 1299). Чекбокс сам обрабатывает обычный клик; мы лишь
    // подхватываем перетаскивание поверх соседних пунктов.
    private bool _dragSelecting;
    private bool _dragMoved;
    private bool _dragValue;
    private object? _dragStart; // FindingViewModel (находки) ИЛИ DriverEntryViewModel (драйверы, правка 944)

    // След перетаскивания: пункты в порядке захода, начиная со стартового. Возврат на пункт из середины следа
    // = «откат» — всё, что после него, возвращаем в исходное состояние. Исходные состояния — чтобы корректно снять.
    private readonly List<object> _dragTrail = [];
    private readonly Dictionary<object, bool> _dragOriginals = new(ReferenceEqualityComparer.Instance);

    public ScansView()
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
        _dragTrail.Clear();
        _dragOriginals.Clear();
        _dragTrail.Add(target);
        _dragOriginals[target] = GetSelected(target); // исходное состояние стартового — до покраски
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
        if (target is null || !CanSelect(target))
        {
            return;
        }

        // Первый сдвиг на соседний пункт = это точно перетаскивание → красим и стартовый пункт.
        if (!_dragMoved && _dragStart is not null)
        {
            _dragMoved = true;
            SetSelected(_dragStart, _dragValue);
        }

        // Уже стоим на «хвосте» следа — ничего не меняем.
        if (_dragTrail.Count > 0 && ReferenceEquals(target, _dragTrail[^1]))
        {
            return;
        }

        var index = _dragTrail.FindIndex(t => ReferenceEquals(t, target));
        if (index >= 0)
        {
            // Вернулись на пункт из середины следа → откатываем всё, что было после него, в исходное состояние.
            for (var i = _dragTrail.Count - 1; i > index; i--)
            {
                SetSelected(_dragTrail[i], _dragOriginals[_dragTrail[i]]);
                _dragTrail.RemoveAt(i);
            }

            return;
        }

        // Новый пункт по ходу → запоминаем исходное состояние и красим под значение перетаскивания.
        _dragOriginals.TryAdd(target, GetSelected(target));
        SetSelected(target, _dragValue);
        _dragTrail.Add(target);
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

    /// <summary>Смена раздела сканов → прокрутка списка находок в начало (сверху самое важное, правка Ивана 1176).</summary>
    private void OnScanGroupChanged(object? sender, SelectionChangedEventArgs e)
    {
        // После перестроения списка (Loaded) ставим прокрутку в самый верх.
        Dispatcher.UIThread.Post(() => FindingsScroll.Offset = new Vector(0, 0), DispatcherPriority.Loaded);
    }

    private DispatcherTimer? _scrollTimer;

    /// <summary>Клик по чипу подсекции: раскрыть секцию и ПЛАВНО проскроллить к ней (правка Ивана 1124).</summary>
    private void OnJunkChipClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: FindingSectionViewModel section })
        {
            return;
        }

        section.IsExpanded = true; // если была свёрнута — раскрываем, чтобы было что показать
        // Даём списку перестроиться после раскрытия, затем плавно прокручиваем.
        Dispatcher.UIThread.Post(() =>
        {
            if (SectionsList.ContainerFromItem(section) is Control container)
            {
                SmoothScrollTo(container);
            }
        }, DispatcherPriority.Loaded);
    }

    /// <summary>Плавная (быстрая, ~150 мс) прокрутка к элементу с плавным замедлением; при сбое — мгновенный переход.</summary>
    private void SmoothScrollTo(Control container)
    {
        var scroll = container.FindAncestorOfType<ScrollViewer>();
        if (scroll is null || container.TranslatePoint(new Point(0, 0), scroll) is not { } point)
        {
            container.BringIntoView();
            return;
        }

        var startY = scroll.Offset.Y;
        var targetY = Math.Max(0, startY + point.Y - 12); // небольшой отступ сверху

        _scrollTimer?.Stop();
        var step = 0;
        const int steps = 12; // ~150 мс при интервале 12 мс
        _scrollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(12) };
        _scrollTimer.Tick += (_, _) =>
        {
            step++;
            var t = Math.Min(1.0, (double)step / steps);
            var eased = 1 - Math.Pow(1 - t, 3); // easeOutCubic — быстро, с мягким торможением
            scroll.Offset = scroll.Offset.WithY(startY + ((targetY - startY) * eased));
            if (t >= 1.0)
            {
                _scrollTimer?.Stop();
            }
        };
        _scrollTimer.Start();
    }
}
