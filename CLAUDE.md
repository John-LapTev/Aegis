# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

> **Aegis** — десктоп .exe под Windows 10/11 (C#/.NET 9 + **Avalonia**), оптимизатор + чистильщик + сканер безопасности «для себя»: сканы по вкладкам, «?»-подсказки простыми словами, починка по одному / массово, бэкап (System Restore) и откат. UI — русский.

> **Статус: в разработке.** Решение `Aegis.sln`: `Aegis.Core` · `Aegis.Scanners` (30 сканеров: 8 вкладок + раздел «Здоровье») · `Aegis.System` (Windows-слой: 30 пробников, обратимые правки, бэкап/откат, репутация) · `Aegis.Threats` (VirusTotal + ИИ Gemini→ChatGPT→Claude) · `Aegis.App` (**Avalonia**, UI) + тесты. Проверено реальной сборкой (`dotnet test`, без warnings): движок сканов и обратимых правок, сканеры, Windows-пробники, планировщик удаления майнеров, воронка Defender→VirusTotal (с троттлингом/кешем), температуры CPU/GPU (LibreHardwareMonitor), анализатор места на диске, удаление UWP-хлама, звук, обслуживание (SFC/DISM+сеть), фирменные утилиты под модель ПК (winget, периферия по USB VID), приватность/ИИ-слежка/антиреклама (Recall/Copilot и т.п.), остатки удалённых программ (пустые папки) и игр Steam (по базе appmanifest), удаление с выбором «в Корзину/навсегда», MVVM-UI со связкой пробники→сканеры→починка/бэкапы — **318 тестов** (на Linux 317 + 1 Windows-only пропускается). UI на Avalonia **собирается и публикуется в Windows `.exe` прямо на Linux** (ADR 0006). **Редизайн шапки (v2.4, согласован с Иваном по рендерам):** разделы — блоки-капсулы со шкалой сканирования (синий→зелёный внутри контура) и флажками снизу; «Дашборд»/«Логи» убраны; все иконки — SVG, без эмодзи. **Безопасность:** удаление в Корзину переведено на `SHFileOperation` с предупреждением о безвозвратном; **обратимость честная (аудит 2026-07-02):** кнопка «Вернуть» — только у правок с реальным бэкапом; неизвестный откат бросает ошибку, а не молчит; ребут-флоу откатывает по реальным бэкап-id (не по фиктивному Guid точки восстановления). **Идеи из чужих инструментов (GhostHunter/Win11Debloat) перенесены clean-room** — свой код, без копирования GPL. **Главное «осталось»:** живой прогон обратимости/ребут-флоу на Win11 и исполнитель удаления майнеров (Фаза 4). Полный аудит и план — [`docs/AUDIT-2026-07-02.md`](docs/AUDIT-2026-07-02.md). См. [`docs/ROADMAP.md`](docs/ROADMAP.md), [`docs/PROJECT_STRUCTURE.md`](docs/PROJECT_STRUCTURE.md), [`docs/BUILD.md`](docs/BUILD.md).

## 🧭 С чего начать
1. [`docs/VISION.md`](docs/VISION.md) — зачем и что строим.
2. [`docs/ROADMAP.md`](docs/ROADMAP.md) — план по фазам.
3. [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) и [`docs/decisions/`](docs/decisions/) — архитектура и решения (ADR).
4. [`docs/PROJECT_STRUCTURE.md`](docs/PROJECT_STRUCTURE.md) — карта файлов.
5. [`docs/DESIGN.md`](docs/DESIGN.md) и [`docs/DESIGN_BRIEF.md`](docs/DESIGN_BRIEF.md) — облик (тёмные XAML-токены, реализуются на Avalonia) и промпт для Claude Design.

## 📐 Правила (в [`.claude/rules/`](.claude/rules/), загружаются автоматически)
- [`documentation-discipline`](.claude/rules/documentation-discipline.md) — синхрон код/доки/структура/память.
- [`project-hygiene`](.claude/rules/project-hygiene.md) — без мусора, чистота.
- [`ai-collaboration`](.claude/rules/ai-collaboration.md) — порядок работы агента.
- [`coding-standards`](.claude/rules/coding-standards.md) — стандарты кода (C#/.NET 9 + Avalonia): коллизия namespace, паттерн Windows-рантайма/тестируемости, путь «находка→починка».
- [`git-workflow`](.claude/rules/git-workflow.md) — git и изоляция репозитория.
- [`release-process`](.claude/rules/release-process.md) — выпуск версии: publish со всеми ключами + sha256, changelog, тег, GitHub Release.
- [`telegram-interaction`](.claude/rules/telegram-interaction.md) — общение через Telegram: отвечать в том же канале; вопросы текстом (без интерактивных пикеров).

## 🗂 Карта
- `docs/` — документация (обязателен `PROJECT_STRUCTURE.md`, держать в синхроне).
- `knowledge-base/` — база знаний (по требованию). Индекс: `knowledge-base/INDEX.md`.
- `.personal/` — личные данные (токен бота, VirusTotal API key). **Не в репозитории** (`.gitignore`). Не клади это в CLAUDE.md или в авто-память.

## 🧱 Стек
- **C# 13 / .NET 9 + Avalonia** (XAML UI, кроссплатформенная сборка) — десктоп под Windows 10/11; прямой доступ к Windows API. UI `Aegis.App` таргетит `net9.0-windows`, но собирается/публикуется в win-x64 `.exe` и на Linux (ADR 0006).
- **MVVM** (CommunityToolkit.Mvvm), DI, async/await (не блокировать UI).
- **Доступ к системе:** WMI (System.Management), реестр (Microsoft.Win32.Registry), службы/автозапуск, Process API, при необходимости P/Invoke (безопасные обёртки). GPU: NVIDIA NVAPI/nvidia-smi, AMD ADLX/ADL (best-effort, поздняя фаза).
- **Бэкап/откат:** точки восстановления Windows (System Restore) + экспорт затрагиваемых веток реестра ПЕРЕД правкой; авто-старт после ребута через RunOnce/Task Scheduler.
- **Безопасность:** свой эвристический сканер + VirusTotal API (сверка хэшей), опц. Windows Defender. Полноценный AV-движок не строим.
- **Логирование:** Serilog. **Тесты:** xUnit.
- **Сборка:** single-file self-contained publish, win-x64. `app.manifest` → requireAdministrator.

## ⚙️ Команды (подробно — [`docs/BUILD.md`](docs/BUILD.md))
- `dotnet build` — сборка решения. Avalonia-проект `Aegis.App` собирается и на Linux; публикация `.exe` — `dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true`.
- `dotnet run --project src/Aegis.App` — запуск приложения (Windows, с правами администратора).
- `dotnet test` — тесты xUnit (`Aegis.Core`/`Aegis.Scanners` и тесты — кроссплатформенные).
- `dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true` — самодостаточный .exe.

## 🌐 Язык
- С пользователем — **русский**, коротко и по делу. Код/имена/коммиты — **английский**. UI приложения — **русский**.
