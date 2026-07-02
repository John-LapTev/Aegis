# Frontend Design — плагин/скилл для Claude Code

> Сверено 2026-05-30. Источники: github.com/anthropics/claude-code/tree/main/plugins/frontend-design · claude.com/plugins/frontend-design

## Что это
Официальный плагин Anthropic для **самого Claude Code** (скилл `frontend-design`). Решает проблему «AI-slop» — когда сгенерированные фронтенды все на одно лицо. Заставляет Claude делать **осознанный, смелый дизайн** вместо шаблонного.

## Как работает
- **Активируется автоматически**, когда просишь Claude построить фронтенд/интерфейс.
- До написания кода требует определить **направление**: Purpose (назначение), Tone (сильный отчётливый эстетический тон — brutalist / maximalist / retro-futuristic / luxury / editorial / playful…), Constraints (ограничения), Differentiation (чем отличается).
- **Банит заезженные шрифты** (Inter, Roboto и пр.), толкает к нестандартным парам шрифтов.
- Прорабатывает: типографику, оркестрованную анимацию и scroll-триггеры, пространственную композицию (асимметрия, ломка сетки), глубину (градиенты, текстуры, слои).

## Установка
Через маркетплейс плагинов Claude Code (`/plugin` → marketplace `anthropics/claude-code`, плагин `frontend-design`). Обновлялся в феврале 2026.

## Claude Design vs Frontend Design plugin — что выбрать
- **Claude Design** (claude.ai/design) — визуальный холст: проектируешь дизайн «руками»/в диалоге, потом handoff в Claude Code. Лучше для проработки UI/прототипов, презентаций, дизайн-системы.
- **Frontend Design plugin** — прямо в Claude Code: повышает качество дизайна при кодогенерации без отдельного инструмента. Лучше, когда строишь сразу в коде (лендинги, marketing-страницы, портфолио).
- Можно совмещать: дизайн-система и макет в Claude Design → реализация в Claude Code с включённым frontend-design.

## Когда применять (в Project-Forge)
Любой дизайн-ёмкий проект на этапе фронтенда — закладывай этот плагин в новый проект (упомяни в его `knowledge-base/INDEX.md` и `ROADMAP`).
