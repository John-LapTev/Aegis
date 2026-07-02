using Aegis.Core.Models;
using CommunityToolkit.Mvvm.Input;

namespace Aegis.App.ViewModels;

/// <summary>Элемент списка «Бэкапы»: что и когда сохранено + кнопка отката.</summary>
public sealed class BackupItemViewModel
{
    private readonly BackupRecord _record;

    public BackupItemViewModel(BackupRecord record, Func<BackupItemViewModel, Task> onRestore)
    {
        _record = record;
        RestoreCommand = new AsyncRelayCommand(() => onRestore(this));
    }

    public string Id => _record.Id;

    public string Description => _record.Description;

    public string KindLabel => _record.Kind switch
    {
        BackupKind.SystemRestorePoint => "Точка восстановления",
        BackupKind.RegistryExport => "Экспорт реестра",
        BackupKind.FileQuarantine => "Карантин файла",
        BackupKind.SettingSnapshot => "Снимок настройки",
        _ => "Бэкап",
    };

    /// <summary>Раздел, к которому относится бэкап (для группировки «Бэкапов» по разделам, как просил пользователь).
    /// Определяется по виду бэкапа и описанию/затронутому — без точных метаданных, но осмысленно.</summary>
    public string Section
    {
        get
        {
            if (_record.Kind == BackupKind.SystemRestorePoint)
            {
                return "Система";
            }

            var text = (_record.Description + " " + string.Join(" ", _record.AffectedAreas)).ToLowerInvariant();
            if (text.Contains("автозапуск", StringComparison.Ordinal)) return "Автозапуск";
            if (text.Contains("процесс", StringComparison.Ordinal)) return "Процессы";
            if (text.Contains("служб", StringComparison.Ordinal) || text.Contains("nahimic", StringComparison.Ordinal)
                || text.Contains("dolby", StringComparison.Ordinal) || text.Contains("audio", StringComparison.Ordinal)
                || text.Contains("звук", StringComparison.Ordinal) || text.Contains("драйвер", StringComparison.Ordinal))
                return "Драйверы и звук";
            if (text.Contains("приватн", StringComparison.Ordinal) || text.Contains("телеметри", StringComparison.Ordinal)
                || text.Contains("uac", StringComparison.Ordinal) || text.Contains("брандмауэр", StringComparison.Ordinal)
                || text.Contains("firewall", StringComparison.Ordinal) || text.Contains("rdp", StringComparison.Ordinal)
                || text.Contains("обновлен", StringComparison.Ordinal) || text.Contains("задач", StringComparison.Ordinal)
                || text.Contains("приложен", StringComparison.Ordinal) || text.Contains("store", StringComparison.Ordinal))
                return "Настройки";
            if (_record.Kind == BackupKind.FileQuarantine || text.Contains("файл", StringComparison.Ordinal)
                || text.Contains("мусор", StringComparison.Ordinal))
                return "Мусор и файлы";
            return "Прочее";
        }
    }

    public string CreatedAtText => _record.CreatedAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm");

    public string AffectedText => string.Join(", ", _record.AffectedAreas);

    /// <summary>Есть ли что показать в строке «что затронуто» (скрываем пустую строку).</summary>
    public bool HasAffected => _record.AffectedAreas.Count > 0;

    public IAsyncRelayCommand RestoreCommand { get; }
}
