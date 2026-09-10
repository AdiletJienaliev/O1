# Agent Bridge — управление живым редактором Unity из CLI

Мост между командной строкой и **уже открытым** редактором. Нужен, чтобы агент
(или скрипт) мог смотреть игру и управлять ею, не закрывая Unity: batch-режим
на залоченном проекте не запускается, а скриншоты Game View оттуда не снять.

## Из чего состоит

| Что | Где |
|---|---|
| Editor-скрипты моста | `Assets/_InternalAssets/Code/Warlord/Editor/AgentBridge/` |
| CLI-обёртка | `Tools/agent/unity.sh` |
| Синтетический ввод (Win32) | `Tools/agent/input.ps1` |
| Обмен файлами | `.agent/` в корне проекта (в `.gitignore`) |

Мост крутится в `EditorApplication.update`, читает запросы из `.agent/inbox/*.req`
и кладёт JSON-ответ в `.agent/outbox/<id>.res`. Выключается в меню
**Warlord → Agent Bridge → Включён**.

## Команды

```bash
Tools/agent/unity.sh status                       # play/compile/ошибки/сцены
Tools/agent/unity.sh screenshot target=game       # PNG в .agent/shots
Tools/agent/unity.sh screenshot target=scene      # кадр из окна Scene
Tools/agent/unity.sh play                         # войти в play mode
Tools/agent/unity.sh stop
Tools/agent/unity.sh refresh                      # импорт + компиляция без фокуса
Tools/agent/unity.sh logs level=error count=20    # консоль редактора
Tools/agent/unity.sh ui                           # все кнопки и тексты на экране
Tools/agent/unity.sh click path=UI_Root/...       # нажать кнопку UGUI
Tools/agent/unity.sh hierarchy depth=4            # дерево сцены
Tools/agent/unity.sh inspect path=UI_Root/...     # поля компонентов
Tools/agent/unity.sh menu path="Warlord/UI/1. ..." # выполнить пункт меню
Tools/agent/unity.sh exec method=Namespace.Type.Method
Tools/agent/unity.sh sceneview pivot=0,0,0 angles=38,45,0 size=90
```

Таймаут ответа — `UNITY_AGENT_TIMEOUT` в секундах (по умолчанию 30). Во время
компиляции и перезагрузки домена запрос ждёт в очереди и выполняется после.

## Грабли, на которые уже наступили

- **Обновление ассетов.** Unity импортирует изменения при *смене* фокуса окна.
  Повторный `AppActivate` на уже активном окне ничего не даёт. Поэтому для
  пересборки используйте команду `refresh` — она зовёт `AssetDatabase.Refresh()`
  изнутри и фокус не трогает.
- **Play mode не перезагружает домен.** Правки кода не подхватятся, пока не выйти
  из play mode: сначала `stop`, потом `refresh`.
- **Окно Scene не перерисовывается, пока скрыто.** Перед `screenshot target=scene`
  выполните `menu path="Window/General/Scene"`, иначе камера отдаст прошлый кадр.
- **2D-режим окна Scene** молча игнорирует заданный поворот — `sceneview` его снимает.
- **Синтетический ввод ненадёжен.** `input.ps1` шлёт события через Win32 в активное
  окно, но игра читает старый Input Manager, и клавиши доходят только когда
  клавиатурный фокус держит именно Game View. Для UI надёжнее `click`.
- **Переходы между экранами не мгновенные.** Между `click` по «ГОТОВ» и «НАЧАТЬ МАТЧ»
  нужна пауза: лобби регистрирует игрока по сети, и слишком ранний клик проходит
  вхолостую.
- **C# с escape-последовательностями нельзя писать через bash heredoc** — `\\`
  схлопывается в `\`. Только через файловые инструменты.
