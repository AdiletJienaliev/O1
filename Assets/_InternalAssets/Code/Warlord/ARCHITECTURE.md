# Архитектура игры о полководце

Код построен по ГДД v0.1. Ссылки вида «ГДД §9» в комментариях указывают на раздел документа.

## Слои и зависимости

Зависимости идут строго сверху вниз, обратных нет.

```
Networking      Bootstrap, лобби, лаг-компенсация, квантизация трансформов
    |
Gameplay        FishNet-объекты и серверные системы (единственный слой, знающий про сеть и сцену)
    |
Domain          Чистый C#: экономика, захват, построения, статы, победа. Тестируется без Unity
    |
Configs         ScriptableObject-данные. Никакой логики
    |
Core            Перечисления, слоты игроков, планировщик серверных систем
```

## Ключевые решения

**Один composition root.** `MatchContext` создаёт все серверные сервисы и раздаёт их через
`IMatchContext`. Единственная статика во всём проекте — `MatchManager.Instance`: без неё
объекты, созданные из префабов по сети, не смогли бы получить зависимости.

**Единый боевой такт 20 Гц.** `FixedStepAccumulator` превращает тик FishNet в фиксированный
боевой шаг. `ServerSystemScheduler` прогоняет системы в порядке `ServerSystemOrder`:

| Порядок | Система | Что делает |
|---|---|---|
| 0 | `MatchClockSystem` | таймер матча |
| 100 | `CaptureSystem` | шкалы захвата, награды, выбывание |
| 200 | `EconomySystem` | золото, XP, время удержания флага |
| 300 | `SpawnQueueSystem` | очередь постройки юнитов |
| 400 | `ArmyFormationSystem` | пересборка слотов построения |
| 500 | `UnitAiSystem` | решения юнитов и заявки на урон |
| 600 | `ProjectileSystem` | полёт стрел |
| 700 | `CombatResolutionSystem` | **весь урон применяется здесь** |
| 800 | `HeroLifecycleSystem`, `BaseHealSystem` | регенерация и респавн |
| 900 | `VictorySystem` | проверка условий победы |

Урон копится в `DamageQueue` и применяется одним пакетом в конце такта. Поэтому два юнита,
убивающие друг друга в одном такте, умирают оба — преимущества по пингу нет (ГДД §12).

**Сетевая модель.** Сервер авторитетен во всём. Юниты не предсказываются: сжатый трансформ
(16 бит на ось, 1 байт на поворот) едет на 15 Гц через `UnitTransformSync`, клиент показывает
интерполированное прошлое с задержкой 100 мс. Полководец использует FishNet Prediction v2,
удар мечом валидируется сервером с перемоткой позиций через `ILagCompensator`.

**Точки расширения.** Новый приказ армии — новый `IUnitOrderBehaviour` плюс строка в
`UnitOrderBehaviourCatalog`. Новое построение — наследник `FormationConfig`. Новая награда за
точку захвата — реализация `ICaptureRewardHandler`. Новая механика в такте — реализация
`IServerSystem` с местом в `ServerSystemOrder`. Ни один из этих шагов не требует правок
существующего кода.

**Статы считаются в одном месте.** `UnitStatsResolver` — единственный код, применяющий древо
прокачки к базовым числам из `UnitConfig`. `ArmyStatsCache` держит результат на игрока и
пересобирается только при покупке перка, поэтому прокачка мгновенно действует на всех живых
юнитов (ГДД §11).

**Презентация отделена.** Игровой код не знает про UI. Всё, что нужно HUD, лежит в SyncVar
на `PlayerState`, `CapturePointBehaviour` и `MatchManager`, а разовые события приходят через
`MatchEvents`.

## Что нужно собрать в редакторе

1. Ассеты конфигов через `Create > Warlord > ...`, собрать их в один `GameConfig`.
2. Префаб юнита: `NetworkObject` + `UnitEntity` + `UnitTransformSync` + `NavMeshUnitLocomotion`
   + `NavMeshAgent`. Добавить `NetworkObserver` с `DistanceCondition` на `unitObserverRange`.
3. Префаб полководца: `NetworkObject` + `HeroController` + `HeroCombat` + `HeroCommandRouter`
   + `CharacterController` + источник ввода.
4. Префаб `PlayerState` (без визуала).
5. Все префабы — в `DefaultPrefabObjects`.
6. Сцена: `NetworkManager`, `MatchManager` (с ссылкой на `GameConfig`), `WarlordPlayerSpawner`,
   `NetworkBootstrap`, `LobbyManager`, точки захвата `CapturePointBehaviour`
   (одна центральная и по одной на базу с проставленным `zoneOwnerSlot`).
7. Заполнить `MapConfig`: позиции баз, точки спавна юнитов и полководцев, центр карты.
