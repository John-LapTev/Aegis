using Microsoft.Win32;
using Aegis.System.Backup;
using Aegis.System.Fixing;
using Xunit;

namespace Aegis.System.Tests.Fixing;

/// <summary>
/// Сквозной регресс (только Windows): обратимое удаление ветки реестра. Проверяет, что бэкап ветки
/// (через <c>reg.exe export</c> с полным именем куста) и восстановление действительно работают —
/// раньше короткое имя <c>HKCU</c>/<c>HKLM</c> отклонялось reg.exe и удаление осиротевших записей
/// тихо не выполнялось. Работает на throwaway-ключе в HKCU, в конце всё убирает.
/// </summary>
public sealed class RegistryKeyDeleteFixTests
{
    [WindowsOnlyFact]
    public async Task ApplyAsync_BacksUpThenDeletes_AndBackupCanRestore()
    {
        var subKey = $@"Software\AegisTest\{Guid.NewGuid():N}";
        const string valueName = "probe";
        const string valueData = "восстанови-меня";

        // Подготовка: создаём throwaway-ключ со значением.
        using (var created = Registry.CurrentUser.CreateSubKey(subKey, writable: true))
        {
            created.SetValue(valueName, valueData);
        }

        var store = new RegistryKeyBackupStore();
        var fix = new RegistryKeyDeleteFix("registry-test", "HKCU", subKey, store);

        try
        {
            var outcome = await fix.ApplyAsync();

            // 1) Бэкап удался и ключ удалён.
            Assert.True(outcome.Success, outcome.Message);
            Assert.False(string.IsNullOrWhiteSpace(outcome.BackupId));
            Assert.Null(Registry.CurrentUser.OpenSubKey(subKey));

            // 2) Бэкап действительно восстанавливает ключ и значение.
            store.Restore(outcome.BackupId!);
            using var restored = Registry.CurrentUser.OpenSubKey(subKey);
            Assert.NotNull(restored);
            Assert.Equal(valueData, restored!.GetValue(valueName));
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(subKey, throwOnMissingSubKey: false);
            try
            {
                Registry.CurrentUser.DeleteSubKey(@"Software\AegisTest", throwOnMissingSubKey: false);
            }
            catch (InvalidOperationException)
            {
                // В папке остались другие подключи параллельных тестов — не трогаем.
            }
        }
    }
}
