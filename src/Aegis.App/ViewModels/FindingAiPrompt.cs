using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Aegis.Core.Models;

namespace Aegis.App.ViewModels;

/// <summary>
/// Построение промпта и веб-запроса к ИИ по находке (раньше жило прямо в <see cref="FindingViewModel"/> — вынесено
/// отдельной «механикой», чтобы VM отвечала только за состояние/презентацию, а не за формулировки для нейросети).
/// Чистые функции над <see cref="Finding"/> — без состояния и без привязок к UI.
/// </summary>
internal static class FindingAiPrompt
{
    /// <summary>Промпт для ИИ: объяснить находку простыми словами + дать короткий вывод о безопасности.</summary>
    public static string Build(Finding finding, IReadOnlyList<string> driverEntryTexts)
    {
        // Общая «рамка» для ИИ: на результаты веб-поиска опирайся, ссылки бери ТОЛЬКО из них, по-русски и по делу.
        const string frame = "Опирайся на результаты веб-поиска ниже (если они есть); ссылки бери ТОЛЬКО из них, " +
                             "не выдумывай. Отвечай по-русски, коротко и простыми словами, без жаргона.";

        // Раздел «Драйверы»: ответ зависит от ТИПА находки (правка 885 — раньше всё считалось «драйвером» и
        // даже на модель ПК выдавалось «версию найти не удалось»). Теперь — осмысленно под каждый тип.
        if (finding.Group == ScanGroup.Drivers)
        {
            // Модель компьютера (не драйвер!) — объяснить, что это, и дать страницу поддержки производителя.
            if (finding.Id == "driver-model")
            {
                var model = finding.Title.Replace("Ваш компьютер:", string.Empty, StringComparison.Ordinal).Trim();
                return $"Это МОДЕЛЬ компьютера пользователя: «{model}». {frame} Кратко скажи, что это за компьютер, " +
                       "и дай ПРЯМУЮ ссылку на официальную страницу поддержки производителя, где собраны драйверы именно " +
                       "для этой модели. Про версию отдельного драйвера НЕ пиши — здесь это не нужно.";
            }

            // Категория установленных драйверов («Звук (10)»): задача — ПОИСКОМ найти, что реально можно обновить.
            if (finding.Id.StartsWith("driver-cat-", StringComparison.Ordinal))
            {
                var devices = driverEntryTexts.Count > 0
                    ? string.Join("; ", driverEntryTexts.Take(12))
                    : finding.Title;
                return $"Это установленные драйверы категории «{finding.Title}». Список с версиями: {devices}. {frame} " +
                       "Главное: ПОИСКОМ в интернете определи, какие из них реально МОЖНО ОБНОВИТЬ (на официальном сайте есть " +
                       "более новая версия, чем установлена). Ответь КОРОТКО: перечисли только те, что можно обновить — с последней " +
                       "версией и ссылкой; если все актуальны или это встроенные драйверы Windows — одной строкой «Все актуальны, " +
                       "обновлять нечего». Не объясняй, что такое драйвер.";
            }

            // Конкретное устройство / драйвер / видеокарта — ИЩЕМ последнюю версию и сравниваем (правка 910: суть раздела — обновлять).
            var installed = string.IsNullOrWhiteSpace(finding.Detail) ? string.Empty : $" Установлена: {finding.Detail}.";
            return $"Это устройство/драйвер: «{finding.Title}».{installed} {frame} ПОИСКОМ в интернете найди последнюю версию " +
                   "драйвера именно для него на ОФИЦИАЛЬНОМ сайте и сравни с установленной. Ответь КОРОТКО одной из формулировок: " +
                   "«У тебя версия X · последняя Y · можно обновить» (+ ссылка) ИЛИ «У тебя актуальная версия — новее в сети нет» " +
                   "ИЛИ, если поиск реально ничего не дал, «Проверить версию не удалось» (+ ссылка на офиц. страницу). Номер версии " +
                   "НЕ выдумывай — бери только из результатов поиска. Не объясняй, что такое драйвер.";
        }

        // Раздел «Утилиты»: устройство/фирменная программа. Если модель не определена — ИИ определяет её по поиску.
        if (finding.Group == ScanGroup.Missing)
        {
            var dev = string.IsNullOrWhiteSpace(finding.Detail) ? string.Empty : $" ({finding.Detail})";
            return $"Это раздел «Утилиты»: устройство или фирменная программа «{finding.Title}»{dev}. {frame} " +
                   "Если модель устройства точно не определена — по результатам поиска ОПРЕДЕЛИ её и назови. Подскажи, " +
                   "есть ли официальная фирменная утилита/драйвер для этого устройства, и дай ссылку. Коротко и по делу.";
        }

        var subject = finding.Group switch
        {
            ScanGroup.Processes => "запущенном процессе",
            ScanGroup.Threats => "файле/драйвере/задаче",
            ScanGroup.Drivers => "устройстве или драйвере",
            _ => "программе в автозапуске",
        };

        var path = string.IsNullOrWhiteSpace(finding.Detail) ? string.Empty : $", путь: {finding.Detail}";
        var publisher = finding.Data?.GetValueOrDefault(FindingDataKeys.Publisher) is { Length: > 0 } pub ? $", издатель: {pub}" : string.Empty;

        return "Ты помощник в программе для обычных людей, которые не разбираются в компьютерах. Объясни простыми " +
               $"словами по-русски, КОРОТКО (2–4 предложения), о {subject}: «{finding.Title}»{path}{publisher}. " +
               "Что это, нужно ли это обычно и безопасно ли (не вирус и не майнер ли это). В самом конце добавь " +
               "ОТДЕЛЬНОЙ строкой вывод одним из вариантов: «Вывод: безопасно» / «Вывод: обычно безопасно» / " +
               "«Вывод: стоит проверить» / «Вывод: похоже на угрозу».";
    }

    /// <summary>
    /// Короткий запрос для веб-поиска по находке: для процессов/автозапуска/угроз — имя exe (svchost.exe),
    /// для драйверов/устройств — название (заголовок). Плюс издатель, если известен. По нему ИИ ищет в сети.
    /// </summary>
    public static string WebQuery(Finding finding)
    {
        var detail = finding.Detail;
        var hasPath = !string.IsNullOrWhiteSpace(detail)
                      && (detail!.Contains(":\\", StringComparison.Ordinal)
                          || detail.StartsWith("\\\\", StringComparison.Ordinal));

        var baseQuery = finding.Group != ScanGroup.Drivers && hasPath
            ? Path.GetFileName(ScanViewHelpers.ExtractExecutablePath(detail!))
            : finding.Title;
        if (string.IsNullOrWhiteSpace(baseQuery))
        {
            baseQuery = finding.Title;
        }

        // Модель ПК: убираем префикс «Ваш компьютер:», чтобы он не засорял поисковый запрос (аудит 2026-07-04).
        baseQuery = baseQuery.Replace("Ваш компьютер:", string.Empty, StringComparison.Ordinal).Trim();

        // Аппаратный код устройства (VID/PID) — добавляем в запрос, чтобы по нему определить модель/драйвер (правка 907).
        var idHint = ExtractVidPid(finding.Data?.GetValueOrDefault(FindingDataKeys.DeviceId));

        // Драйверы — запрос заточен под поиск ПОСЛЕДНЕЙ версии на официальном сайте (правка 910).
        if (finding.Group == ScanGroup.Drivers)
        {
            var query = $"{baseQuery} драйвер последняя версия скачать официальный сайт";
            return string.IsNullOrEmpty(idHint) ? query : $"{query} {idHint}";
        }

        // Утилиты/периферия с неизвестной моделью — ищем по аппаратному коду, чтобы определить устройство (правка 907).
        if (finding.Group == ScanGroup.Missing && !string.IsNullOrEmpty(idHint))
        {
            return $"{baseQuery} {idHint} устройство модель драйвер";
        }

        var publisher = finding.Data?.GetValueOrDefault(FindingDataKeys.Publisher);
        return string.IsNullOrEmpty(publisher) ? baseQuery : $"{baseQuery} {publisher}";
    }

    /// <summary>Достаёт «VID_xxxx&PID_xxxx» из аппаратного кода устройства — по нему ИИ ищет модель/драйвер (правка 907).</summary>
    private static string? ExtractVidPid(string? hardwareId)
    {
        if (string.IsNullOrEmpty(hardwareId))
        {
            return null;
        }

        var match = Regex.Match(hardwareId, @"VID_[0-9A-Fa-f]{4}.{0,3}PID_[0-9A-Fa-f]{4}");
        return match.Success ? match.Value : null;
    }
}
