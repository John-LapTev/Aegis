namespace Aegis.Core.Abstractions;

/// <summary>
/// Правка, умеющая сообщать ЖИВОЙ прогресс выполнения (0..1) — например, долгие SFC/DISM, которые
/// печатают проценты в свой вывод. UI крутит кольцо-заполнение по этим значениям, а не имитацией.
/// </summary>
public interface IProgressReportingFix
{
    /// <summary>Куда сообщать прогресс (0..1). Устанавливает вызывающий (ViewModel) перед применением; null — не сообщать.</summary>
    IProgress<double>? Progress { get; set; }
}
