using System;
using System.Collections.Generic;
using Aegis.Core.Models;

namespace Aegis.App.ViewModels;

/// <summary>
/// Плитки раздела «Здоровье»: плейсхолдеры (что раздел умеет показывать — до проверки) и порядок плиток по
/// компоненту. Чистые статические функции — вынесены из MainWindowViewModel (презентация, не координация).
/// </summary>
internal static class HealthTiles
{
    /// <summary>
    /// Плитки-плейсхолдеры «Здоровья» (показываем ДО проверки приглушённо): человек сразу видит, что раздел
    /// умеет показывать, и что для этого нужно запустить проверку/тест (правка 1086).
    /// </summary>
    public static IEnumerable<Finding> CreatePlaceholders()
    {
        // Порядок сгруппирован по компоненту (как реальные плитки): процессор рядом с проверкой под нагрузкой,
        // затем видеокарта, память, диски, батарея, вентиляторы, устройства, стабильность, время (правка 1094).
        (string Id, string Title, string Icon, bool Test)[] tiles =
        [
            ("ph-cpuload", "Загрузка процессора", "cpu", false),
            ("ph-stress", "Проверка под нагрузкой", "cpu", true),
            ("ph-gputemp", "Температура видеокарты", "gpu", false),
            ("ph-ram", "Оперативная память", "memory", false),
            ("ph-disk", "Диски", "disk", false),
            ("ph-battery", "Батарея", "battery-full", false),
            ("ph-fan", "Вентиляторы", "fan", false),
            ("ph-devices", "Устройства", "plug", false),
            ("ph-stability", "Стабильность", "shield", false),
            ("ph-uptime", "Время без перезагрузки", "clock", false),
        ];

        foreach (var (id, title, icon, test) in tiles)
        {
            yield return new Finding
            {
                Id = id,
                Group = ScanGroup.Health,
                Severity = Severity.Info,
                Title = title,
                Explain = test
                    ? "Появится после теста под нагрузкой — запусти его в разделе «Тесты»."
                    : "Появится после проверки и тестов: нажми «Проверить компьютер», а проверку под нагрузкой запусти в разделе «Тесты».",
                Data = new Dictionary<string, string> { [FindingDataKeys.Placeholder] = "1", [FindingDataKeys.HealthIcon] = icon },
            };
        }
    }

    /// <summary>
    /// Порядок плиток «Здоровья» по компоненту: показатели процессора идут рядом, затем видеокарта, память,
    /// диски (все вместе), батарея, вентиляторы, время работы — чтобы данные одной детали не были разбросаны.
    /// </summary>
    public static int Order(Finding finding)
    {
        var id = finding.Id;
        if (id is "health-stress" or "ph-stress")
        {
            return 0; // проверка под нагрузкой (в т.ч. плейсхолдер) — про процессор, ставим рядом с загрузкой CPU
        }

        if (id.StartsWith("temp-", StringComparison.Ordinal) && id.Contains("роцессор", StringComparison.Ordinal))
        {
            return 1; // температура процессора
        }

        if (id == "health-cpuload")
        {
            return 2; // загрузка процессора
        }

        if (id.StartsWith("temp-", StringComparison.Ordinal))
        {
            return 3; // температура видеокарты (и прочие температуры) — рядом
        }

        if (id == "health-gpuload")
        {
            return 4; // загрузка видеокарты — сразу за её температурой
        }

        if (id == "health-ram")
        {
            return 5; // оперативная память
        }

        if (id.StartsWith("disk-health-", StringComparison.Ordinal))
        {
            return 6; // диски — все вместе
        }

        if (id.StartsWith("health-battery", StringComparison.Ordinal))
        {
            return 7; // батарея
        }

        if (id == "health-fan")
        {
            return 8; // вентиляторы
        }

        if (id == "health-devices")
        {
            return 9; // устройства с ошибками
        }

        if (id == "health-crashes")
        {
            return 10; // стабильность (синие экраны)
        }

        if (id == "health-uptime")
        {
            return 11; // время без перезагрузки
        }

        return 12;
    }
}
