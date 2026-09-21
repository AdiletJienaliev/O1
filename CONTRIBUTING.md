# Contributing to Warlord

Thanks for taking the time. This is a Unity project, and Unity projects break in ways plain C# repos
do not — most of the rules below exist because of that.

*Русская версия — [ниже](#по-русски).*

## Before you start

- Open the project with **Unity 6000.4.3f1**. A different version rewrites asset files on import and
  turns a two-line change into a thousand-line diff.
- Run `git lfs install` before your first clone.
- Read the [Architecture section](README.md#-architecture) of the README. The layering is deliberate:
  `Core → Domain → Gameplay`, with `Presentation` and `UI` reading state and never writing it.

## Ground rules

**The server decides.** Anything that affects match state — gold, health, capture progress, unit
spawns — is computed on the server and replicated. A client sends a command and accepts the answer,
including a rejection.

**Rules go in `Domain`.** If a piece of logic can be expressed without a scene, a `MonoBehaviour` or a
network call, it belongs in `Warlord.Domain` as plain C#. Gameplay systems orchestrate; they do not
hide the rules.

**Balance goes in `Configs`.** Never hard-code a number that a designer could want to change, and never
mutate a `ScriptableObject` at runtime — room settings override, assets stay untouched.

**Enum order is a wire format.** Anything synced as a `byte` (match phase, order type, upgrade type,
slot kind…) must keep its declaration order. Append new values at the end.

**Comment the *why*.** The existing comments explain trade-offs and traps, not syntax. Match that:
if a line looks odd and the reason is not obvious from the code, write the reason down.

## Unity-specific etiquette

- **Commit `.meta` files.** Always, together with the asset they belong to. A missing `.meta` breaks
  every reference to that asset for everyone else.
- **Scenes and prefabs do not merge.** Before touching `Battle_Arena4.unity` or a shared prefab, say so
  in the issue or PR so two people don't edit it at once.
- **Don't commit generated files.** `Library/`, `Temp/`, `Logs/`, `.agent/`, `*.csproj`, `*.sln` are
  already ignored — keep it that way.
- **Keep new code inside an existing assembly** unless you have a reason not to. New `.asmdef` files
  change compile times and platform availability for everyone.

## Style

- 4 spaces, block-scoped namespaces, `private` fields prefixed with `_`, `[SerializeField]` over
  `public` for inspector data — follow the file you are editing.
- XML docs (`/// <summary>`) on public types and on anything with a non-obvious contract.
- Comments and docs in the codebase are in Russian; keep a file consistent with itself.

## Pull requests

1. Branch off the default branch, one topic per branch.
2. Make sure the project compiles with **zero console errors** and play mode reaches a running match.
3. Describe what you changed, why, and how you tested it — the PR template asks exactly that.
4. Screenshots or a short clip for anything visual. It is a game; a picture settles most review threads.

There is no CI on this repository yet: Unity builds need a licence, so **the human review is the check**.
Say plainly what you did and did not test.

---

<a name="по-русски"></a>

# Как участвовать в Warlord

Спасибо, что дочитали. Это проект на Unity, и ломается он не так, как обычный C#-репозиторий —
большая часть правил ниже существует именно поэтому.

## Перед началом

- Открывайте проект **Unity 6000.4.3f1**. Другая версия при импорте перепишет ассеты, и правка на две
  строки превратится в диф на тысячу.
- Перед первым клоном выполните `git lfs install`.
- Прочитайте раздел [«Архитектура»](README.ru.md#-архитектура) в README. Слои выстроены намеренно:
  `Core → Domain → Gameplay`, а `Presentation` и `UI` только читают состояние и никогда его не пишут.

## Базовые правила

**Решает сервер.** Всё, что влияет на состояние матча — золото, здоровье, прогресс захвата, спавн
юнитов, — считается на сервере и реплицируется. Клиент шлёт команду и принимает ответ, включая отказ.

**Правила живут в `Domain`.** Если логику можно выразить без сцены, `MonoBehaviour` и сетевых вызовов —
ей место в `Warlord.Domain` на чистом C#. Геймплейные системы оркеструют, но не прячут правила внутри себя.

**Баланс живёт в `Configs`.** Не хардкодьте число, которое дизайнер захочет поменять, и не мутируйте
`ScriptableObject` в рантайме — настройки комнаты перекрывают значения, ассет остаётся нетронутым.

**Порядок в enum — это формат данных.** Всё, что синкается байтом (фаза матча, тип приказа, тип
улучшения, вид слота…), обязано сохранять порядок объявления. Новые значения дописываются в конец.

**Комментируйте «почему».** Существующие комментарии объясняют компромиссы и грабли, а не синтаксис.
Держите ту же планку: если строка выглядит странно, а причина не видна из кода, — запишите причину.

## Специфика Unity

- **Коммитьте `.meta`-файлы.** Всегда и вместе с самим ассетом. Потерянный `.meta` ломает все ссылки
  на этот ассет у всех остальных.
- **Сцены и префабы не мержатся.** Прежде чем трогать `Battle_Arena4.unity` или общий префаб, скажите
  об этом в задаче или PR, чтобы двое не правили его одновременно.
- **Не коммитьте генерируемое.** `Library/`, `Temp/`, `Logs/`, `.agent/`, `*.csproj`, `*.sln` уже
  в `.gitignore` — пусть там и остаются.
- **Новый код — в существующую ассембли**, если нет веской причины поступить иначе. Новые `.asmdef`
  меняют время компиляции и доступность по платформам для всех.

## Стиль

- 4 пробела, блочные namespace, приватные поля с `_`, `[SerializeField]` вместо `public` для данных
  инспектора — ориентируйтесь на файл, который правите.
- XML-документация (`/// <summary>`) на публичных типах и везде, где контракт неочевиден.
- Комментарии и документация в коде — на русском; внутри файла держите единый язык.

## Пул-реквесты

1. Ветка от основной, одна тема на ветку.
2. Проект должен компилироваться **без единой ошибки в консоли**, а play mode — доходить до матча.
3. Опишите, что изменили, зачем и как проверяли — шаблон PR спрашивает ровно это.
4. Скриншоты или короткий ролик для всего визуального. Это игра: картинка закрывает половину вопросов.

CI в репозитории пока нет: сборке Unity нужна лицензия, поэтому **проверка — это ручное ревью**.
Пишите честно, что проверили, а что нет.
