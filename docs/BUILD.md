# Сборка и запуск — Aegis

> Aegis — десктоп под **Windows 10/11**. UI — на **Avalonia** (ADR 0006), поэтому, в отличие от WPF,
> приложение **собирается и публикуется в Windows `.exe` на любой ОС, включая Linux**.
> Запускается готовый `.exe` — на Windows.

## Требования
- **.NET SDK 9.0+** — https://dotnet.microsoft.com/download/dotnet/9.0
- Для **запуска** `.exe` — Windows 10/11, **от имени администратора** (манифест `requireAdministrator`, ADR 0004).
- Включённая «Защита системы» (точки восстановления) — для обратимых правок (ADR 0002).

## Сборка и тесты (на любой ОС, в т.ч. Linux)

```bash
dotnet restore
dotnet build -c Release            # всё решение, включая Avalonia-UI
dotnet test                        # все тесты (Core / Scanners / Threats)
```
> `Aegis.App` (Avalonia, `net9.0-windows`) собирается и на Linux — Windows-targeting pack качается из NuGet.
> Запуск окна — только на Windows (на Linux GUI не поднимется, но сборка/публикация работают).

## Публикация — самодостаточный `.exe` для заказчика

> ⚠️ **ОБЯЗАТЕЛЬНО с ключами.** Ключи ИИ/VirusTotal/поиска зашиваются в `.exe` ТОЛЬКО через `-p:`-параметры
> при публикации. **Без них ИИ, онлайн-проверка VirusTotal и веб-поиск в готовом `.exe` МОЛЧА отключены** —
> тумблеры в интерфейсе просто ничего не делают. Такой билд заказчику отправлять НЕЛЬЗЯ. Значения ключей —
> в `.personal/credentials.md` (в репозиторий не коммитятся).

```bash
# Ключи подставь из .personal/credentials.md (здесь — имена переменных, не значения).
dotnet publish src/Aegis.App/Aegis.App.csproj -c Release -r win-x64 \
  --self-contained true -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true \
  -p:VirusTotalKey="$VT" -p:GeminiKey="$GKEY" \
  -p:OpenRouterKey="$ORK" -p:ApiglueKey="$AGK" \
  -p:TavilyKey="$TAV" -p:SerperKey="$SER" \
  -o publish
```
Результат — один `publish/Aegis.exe` (win-x64, ~46 МБ со сжатием), запускается на чистой Windows 10/11
**без установки .NET**. Этот файл и отправляем Ивану.
Провайдеры ИИ: Gemini (беспл.) → ChatGPT/OpenRouter (беспл.) → Claude/apiglue (платн., последним). Поиск:
Tavily → Serper → (без ключей) DuckDuckGo.

## Запуск из исходников (на Windows)

```bash
dotnet run --project src/Aegis.App        # поднимется UAC-запрос прав администратора
```

## Стандарты сборки
- `Directory.Build.props`: `Nullable=enable`, `TreatWarningsAsErrors=true`, .NET-анализаторы.
  Сборка проходит **без предупреждений** (см. [`.claude/rules/coding-standards.md`](../.claude/rules/coding-standards.md)).

## Среда разработки
- Разработка/сборка идут на Linux-машине; локально установлен .NET 9 SDK (`~/.dotnet`).
- Кроссплатформенно собираются и тестируются: `Aegis.Core`, `Aegis.Scanners`, `Aegis.Threats`, `Aegis.App` (Avalonia)
  и (когда появится) `Aegis.System` (`net9.0-windows` без WPF — тоже компилируется на Linux).
- Живой запуск окна и Windows-операции (реестр/WMI/точки восстановления) — только на Windows.
