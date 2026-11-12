# NPC на заправке. Пошаговое подключение

После нажатия «Играть» заправка создаёт двух нейтральных NPC. Один ходит у колонок, второй перед магазином. Оба используют `NpcController`. JSON назначает каждому внешность, режим `patrol`, параметры капсулы и маршрут. Ниже фрагменты «Было» показывают места в версии игры до подключения NPC, а «Стало» показывают код в рабочей версии.

Для старой копии проекта создай файлы из шага 1, положи подготовленные FBX и PNG из шага 2 и создай JSON из шага 3. Затем примени патч из приложения и перейди к шагу 9. Фрагменты «Было → Стало» в шагах 2 и 5–8 объясняют изменения из патча. Если вносишь изменения вручную, сверяй эти шаги с полным патчем.

## 1. Собери код в одной папке

Создай в основном проекте папку `Actors/NPC` и вставь в неё шесть файлов из блоков ниже. Патч в [приложении](#патч-для-старой-копии-проекта) удалит прежний `NPcContract.cs` и внесёт правки в существующие файлы.

**Было**

```text
Actors/NPC/
└── NPcContract.cs
```

**Стало**

```text
Actors/NPC/
├── NpcContracts.cs
├── NpcBrain.cs
├── NpcDirector.cs
├── NpcConfiguration.cs
├── NpcController.cs
└── NpcSystem.cs
```

Все шесть файлов используют пространство имён `_3DLight.Npcs`. SDK автоматически компилирует их в `3DLight`. Если у тебя была промежуточная версия с двумя проектами NPC, удали их записи из `3DLight.slnx`, ссылки `ProjectReference` из `3DLight.csproj` и старые файлы этих проектов после переноса исходников.

| Файл | Что делает |
| --- | --- |
| [NpcContracts.cs](Actors/NPC/NpcContracts.cs) | Описывает задачи и решения NPC. |
| [NpcBrain.cs](Actors/NPC/NpcBrain.cs) | Вычисляет перемещение за кадр для полученной задачи. |
| [NpcDirector.cs](Actors/NPC/NpcDirector.cs) | Хранит текущую точку маршрута и паузу каждого NPC. |

### `Actors/NPC/NpcContracts.cs`

```csharp
using Microsoft.Xna.Framework;

namespace _3DLight.Npcs;

internal enum NpcOrderKind
{
    Wait,
    GoTo
}

internal readonly record struct NpcOrder(
    NpcOrderKind Kind,
    Vector3 Destination);

internal readonly record struct NpcIntent(
    Vector3 Movement,
    bool ReachedDestination = false);
```

### `Actors/NPC/NpcBrain.cs`

```csharp
using Microsoft.Xna.Framework;

namespace _3DLight.Npcs;

internal sealed class NpcBrain(float speed)
{
    private const float ArrivalDistance = 0.18f;
    private const float ArrivalHeightTolerance = 0.55f;

    public NpcIntent Tick(Vector3 position, NpcOrder order, float seconds)
    {
        switch (order.Kind)
        {
            case NpcOrderKind.Wait:
                return default;

            case NpcOrderKind.GoTo:
                Vector3 offset = order.Destination - position;
                float heightDifference = MathF.Abs(offset.Y);
                offset.Y = 0f;
                float distance = offset.Length();

                if (distance <= ArrivalDistance && heightDifference <= ArrivalHeightTolerance)
                    return new(Vector3.Zero, ReachedDestination: true);
                if (distance < 0.001f)
                    return default;

                float step = MathF.Min(speed * seconds, distance);
                return new(offset / distance * step);

            default:
                throw new ArgumentOutOfRangeException(nameof(order), order.Kind, "Неизвестная команда NPC.");
        }
    }
}
```

### `Actors/NPC/NpcDirector.cs`

```csharp
using Microsoft.Xna.Framework;

namespace _3DLight.Npcs;

internal sealed class NpcDirector
{
    private readonly Dictionary<string, Patrol> patrols = new(StringComparer.OrdinalIgnoreCase);

    public Vector3 Register(string id, Vector3[] route, float waitSeconds)
    {
        patrols.Add(id, new Patrol(route, waitSeconds));
        return route[0];
    }

    public NpcOrder Update(string id, float seconds, bool reachedDestination)
    {
        Patrol patrol = patrols[id];
        if (reachedDestination)
        {
            patrol.Next = (patrol.Next + 1) % patrol.Route.Length;
            patrol.WaitRemaining = patrol.WaitSeconds;
        }

        if (patrol.WaitRemaining > 0f)
        {
            patrol.WaitRemaining -= seconds;
            return new(NpcOrderKind.Wait, default);
        }

        return new(NpcOrderKind.GoTo, patrol.Route[patrol.Next]);
    }

    private sealed class Patrol(Vector3[] route, float waitSeconds)
    {
        public Vector3[] Route { get; } = route;
        public float WaitSeconds { get; } = waitSeconds;
        public int Next { get; set; } = 1;
        public float WaitRemaining { get; set; }
    }
}
```

### `Actors/NPC/NpcConfiguration.cs`

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;

namespace _3DLight.Npcs;

internal sealed class NpcConfiguration
{
    public Dictionary<string, NpcSkin> Skins { get; init; } = [];
    public NpcDefinition[] Actors { get; init; } = [];

    public static NpcConfiguration Load(string levelName)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Content", "Levels", levelName + ".npcs.json");
        if (!File.Exists(path))
            return new();

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };
        var config = JsonSerializer.Deserialize<NpcConfiguration>(File.ReadAllText(path), options)
            ?? throw new InvalidDataException($"Пустой файл NPC: {path}");
        config.Validate();
        return config;
    }

    private void Validate()
    {
        if (Skins is null || Actors is null)
            throw new InvalidDataException("Настройки NPC должны содержать skins и actors.");

        foreach ((string id, NpcSkin skin) in Skins)
        {
            if (skin is null || string.IsNullOrWhiteSpace(skin.Model))
                throw new InvalidDataException($"У skin '{id}' укажи model.");
            if (string.IsNullOrWhiteSpace(skin.IdleClip) || string.IsNullOrWhiteSpace(skin.WalkClip))
                throw new InvalidDataException($"У skin '{id}' укажи idleClip и walkClip.");
            if (!float.IsFinite(skin.VisualYawDegrees))
                throw new InvalidDataException($"У skin '{id}' visualYawDegrees должен быть конечным числом.");
        }

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (NpcDefinition actor in Actors)
        {
            if (actor is null || string.IsNullOrWhiteSpace(actor.Id))
                throw new InvalidDataException("У каждого NPC должен быть id.");
            if (!ids.Add(actor.Id))
                throw new InvalidDataException($"Повторяется id NPC '{actor.Id}'.");
            if (actor.Skin is null || !Skins.ContainsKey(actor.Skin))
                throw new InvalidDataException($"У NPC '{actor.Id}' неизвестный skin '{actor.Skin}'.");

            actor.Validate();
        }
    }
}

internal sealed class NpcSkin
{
    public string Model { get; init; } = "";
    public string IdleClip { get; init; } = "Idle";
    public string WalkClip { get; init; } = "Walk";
    public float VisualYawDegrees { get; init; }
}

internal sealed class NpcDefinition
{
    public string Id { get; init; } = "";
    public string Skin { get; init; } = "";
    public string Behavior { get; init; } = "patrol";
    public float Speed { get; init; } = 1.35f;
    public float Radius { get; init; } = 0.3f;
    public float Height { get; init; } = 1.75f;
    public float WaitSeconds { get; init; } = 0.7f;
    public NpcWaypoint[] Route { get; init; } = [];

    public void Validate()
    {
        if (Behavior != "patrol")
            throw new InvalidDataException($"У NPC '{Id}' ожидается behavior 'patrol'.");
        if (!float.IsFinite(Speed) || Speed <= 0f)
            throw new InvalidDataException($"У NPC '{Id}' speed должен быть положительным конечным числом.");
        if (!float.IsFinite(Radius) || Radius <= 0f || !float.IsFinite(Height) || Height < 2f * Radius)
            throw new InvalidDataException($"У NPC '{Id}' нужны конечные radius > 0 и height >= 2 × radius.");
        if (!float.IsFinite(WaitSeconds) || WaitSeconds < 0f)
            throw new InvalidDataException($"У NPC '{Id}' waitSeconds должен быть неотрицательным конечным числом.");
        if (Route is not { Length: >= 2 } || Array.Exists(Route, point => point is null))
            throw new InvalidDataException($"У NPC '{Id}' route должен содержать хотя бы две заполненные точки.");
    }
}

internal sealed class NpcWaypoint
{
    public string? Marker { get; init; }
    public float[]? Position { get; init; }

    public Vector3 Resolve(Level level, string actorId)
    {
        switch (Position, Marker)
        {
            case ([var x, var y, var z], null) when float.IsFinite(x) && float.IsFinite(y) && float.IsFinite(z):
                return new(x, y, z);

            case (null, string marker) when !string.IsNullOrWhiteSpace(marker):
                if (level.TryGetMarkerPosition(marker, out Vector3 markerPosition))
                    return markerPosition;
                throw new InvalidDataException($"Маркер '{marker}' из маршрута NPC '{actorId}' отсутствует на уровне.");

            default:
                throw new InvalidDataException($"Для точки маршрута NPC '{actorId}' укажи position [x, y, z] или marker.");
        }
    }
}
```

### `Actors/NPC/NpcController.cs`

```csharp
using Ars3D.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace _3DLight.Npcs;

internal sealed class NpcController : IDisposable
{
    private readonly CharacterPhysicsComponent physics;
    private readonly CharacterAnimationComponent animation = new(modelScale: 1f);
    private readonly NpcBrain brain;
    private readonly NpcSkin skin;
    private readonly float visualYaw;

    public NpcDefinition Definition { get; }
    public CapsuleShape Capsule => new(physics.Position, physics.Radius, physics.Height);
    public bool ReachedDestination { get; private set; }

    public NpcController(NpcDefinition definition, NpcSkin skin)
    {
        Definition = definition;
        this.skin = skin;
        brain = new NpcBrain(definition.Speed);
        physics = new CharacterPhysicsComponent(Vector3.Zero, definition.Radius, definition.Height, gravity: -28f);
        visualYaw = MathHelper.ToRadians(skin.VisualYawDegrees);
    }

    public void LoadContent(GraphicsDevice graphics, Texture2D fallbackTexture, Effect effect)
    {
        animation.LoadContent(graphics, skin.Model, skin.IdleClip, fallbackTexture, effect,
            targetHeight: Definition.Height);
        if (animation.GetClipDuration(skin.IdleClip) <= 0f || animation.GetClipDuration(skin.WalkClip) <= 0f)
            throw new InvalidDataException($"Для NPC '{Definition.Id}' нужны клипы ожидания и ходьбы.");
    }

    public void SpawnAt(Vector3 position)
    {
        physics.SpawnAt(position);
        ReachedDestination = false;
        animation.RotationY = visualYaw;
        animation.Restart(skin.IdleClip, loop: true);
    }

    public void Update(Level level, NpcOrder order, float seconds, ReadOnlySpan<CapsuleShape> obstacles)
    {
        Vector3 before = physics.Position;
        NpcIntent intent = brain.Tick(before, order, seconds);
        ReachedDestination = intent.ReachedDestination;

        physics.Move(level, intent.Movement, seconds, obstacles);
        Vector3 actual = physics.Position - before;
        actual.Y = 0f;
        bool walking = intent.Movement != Vector3.Zero && actual.LengthSquared() > 0.000001f;
        if (walking)
            animation.RotationY = MathF.Atan2(actual.X, actual.Z) + visualYaw;

        animation.Play(walking ? skin.WalkClip : skin.IdleClip, loop: true);
        animation.Update(seconds);
    }

    public void Draw(Matrix view, Matrix projection, SceneLighting lighting, SceneRenderQueue queue) =>
        animation.Draw(physics.Position, view, projection, lighting, queue);

    public void Dispose() => animation.Dispose();
}
```

### `Actors/NPC/NpcSystem.cs`

```csharp
using Ars3D.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace _3DLight.Npcs;

internal sealed class NpcSystem : IDisposable
{
    private readonly List<NpcController> actors = [];
    private NpcDirector director = new();
    private CapsuleShape[] capsules = [];
    private CapsuleShape[] obstacles = [];

    public ReadOnlySpan<CapsuleShape> Capsules => capsules;

    public static NpcSystem Load(Level level, string levelName, GraphicsDevice graphics,
        Texture2D fallbackTexture, Effect effect)
    {
        NpcConfiguration config = NpcConfiguration.Load(levelName);
        var system = new NpcSystem();
        try
        {
            foreach (NpcDefinition definition in config.Actors)
            {
                var actor = new NpcController(definition, config.Skins[definition.Skin]);
                system.actors.Add(actor);
                actor.LoadContent(graphics, fallbackTexture, effect);
            }
            system.capsules = new CapsuleShape[system.actors.Count];
            system.obstacles = new CapsuleShape[system.actors.Count + 1];
            system.ResetRoutes(level);
            return system;
        }
        catch
        {
            system.Dispose();
            throw;
        }
    }

    public void ResetRoutes(Level level)
    {
        var replacement = new NpcDirector();
        var spawns = new Vector3[actors.Count];
        for (int i = 0; i < actors.Count; i++)
        {
            NpcDefinition definition = actors[i].Definition;
            Vector3[] route = definition.Route.Select(point => point.Resolve(level, definition.Id)).ToArray();
            spawns[i] = replacement.Register(definition.Id, route, definition.WaitSeconds);
        }
        for (int i = 0; i < actors.Count; i++)
        {
            actors[i].SpawnAt(spawns[i]);
            capsules[i] = actors[i].Capsule;
        }
        director = replacement;
    }

    public void Update(Level level, float seconds, CapsuleShape player, CapsuleShape skeleton)
    {
        if (actors.Count == 0)
            return;

        obstacles[0] = player;
        obstacles[1] = skeleton;
        for (int i = 0; i < actors.Count; i++)
        {
            // Копируем капсулы до и после текущего NPC.
            capsules.AsSpan(0, i).CopyTo(obstacles.AsSpan(2));
            capsules.AsSpan(i + 1).CopyTo(obstacles.AsSpan(i + 2));

            NpcController actor = actors[i];
            var order = director.Update(actor.Definition.Id, seconds, actor.ReachedDestination);
            actor.Update(level, order, seconds, obstacles);
            capsules[i] = actor.Capsule;
        }
    }

    public void Draw(Matrix view, Matrix projection, SceneLighting lighting, SceneRenderQueue queue)
    {
        foreach (NpcController actor in actors)
            actor.Draw(view, projection, lighting, queue);
    }

    public void Dispose()
    {
        foreach (NpcController actor in actors)
            actor.Dispose();
        actors.Clear();
        capsules = [];
        obstacles = [];
        director = new();
    }
}
```

## 2. Положи модели, анимации и текстуры

Скопируй `Assets/Characters_psx` целиком. В ней лежит [полный архив Characters_psx.rar](Assets/Characters_psx/Characters_psx.rar) и две подготовленные папки `npc_customer` и `npc_clerk`. В каждой папке есть `Model.fbx`, `Idle.fbx` и `Walk.fbx`. Клипы уже перенесены на риг соответствующей модели. Текстуры лежат в `Content/Assets/npc_customer` и `Content/Assets/npc_clerk`.

В [Assets/models.json](Assets/models.json) в массиве `models` раньше за моделями игрока и скелета не было записей гражданских NPC. В рабочем варианте добавлены следующие два элемента массива.

```json
[
  {
    "name": "npc_customer",
    "source": "Characters_psx/npc_customer/Model.fbx",
    "output": "../Content/Models/npc_customer.3dmodel",
    "clips": {
      "Idle": "Characters_psx/npc_customer/Idle.fbx",
      "Walk": "Characters_psx/npc_customer/Walk.fbx"
    }
  },
  {
    "name": "npc_clerk",
    "source": "Characters_psx/npc_clerk/Model.fbx",
    "output": "../Content/Models/npc_clerk.3dmodel",
    "clips": {
      "Idle": "Characters_psx/npc_clerk/Idle.fbx",
      "Walk": "Characters_psx/npc_clerk/Walk.fbx"
    }
  }
]
```

Вставляй объекты внутрь существующего `models`. Пути `source` и `clips` считаются от папки `Assets`, где лежит manifest. При сборке задача `CompileModels` запускает `ModelCompiler` и кладёт готовые `.3dmodel` в `Content/Models` и в папку запуска.

В группе копирования контента в [3DLight.csproj](3DLight.csproj) добавь правило для текстур NPC рядом с правилом уровней.

**Было**

```xml
<None Update="Content\Levels\*.json" CopyToOutputDirectory="PreserveNewest" />
```

**Стало**

```xml
<None Update="Content\Levels\*.json" CopyToOutputDirectory="PreserveNewest" />
<None Update="Content\Assets\npc_*\*.png" CopyToOutputDirectory="PreserveNewest" />
```

В папке запуска текстура должна быть в `Content/Assets/<имя модели>/`. Например, `npc_customer.3dmodel` ищет `Character_01.png` в папке `Content/Assets/npc_customer`.

## 3. Добавь JSON уровня

Создай `Content/Levels/gas_station.npcs.json` и вставь приведённый ниже JSON. Имя файла строится из имени уровня и суффикса `.npcs.json`. Для уровня `gas_station` это `gas_station.npcs.json`.

**Было** — заправка загружала игрока и скелета через `LevelSession`.

**Стало** — к ним добавляются две записи `actors` из JSON.

```json
{
  "skins": {
    "customer": { "model": "npc_customer", "idleClip": "Idle", "walkClip": "Walk" },
    "clerk": { "model": "npc_clerk", "idleClip": "Idle", "walkClip": "Walk" }
  },
  "actors": [
    {
      "id": "forecourt_visitor",
      "skin": "customer",
      "behavior": "patrol",
      "speed": 1.35,
      "radius": 0.30,
      "height": 1.78,
      "waitSeconds": 0.7,
      "route": [
        { "position": [3.0, 0.05, 15.0] },
        { "position": [6.0, 0.05, 15.0] },
        { "position": [6.0, 0.05, 10.0] },
        { "position": [3.0, 0.05, 10.0] }
      ]
    },
    {
      "id": "shop_visitor",
      "skin": "clerk",
      "behavior": "patrol",
      "speed": 1.25,
      "radius": 0.29,
      "height": 1.69,
      "waitSeconds": 1.0,
      "route": [
        { "position": [2.0, 0.05, -10.0] },
        { "position": [5.0, 0.05, -10.0] },
        { "position": [5.0, 0.05, -12.4] },
        { "position": [2.0, 0.05, -12.4] }
      ]
    }
  ]
}
```

- `skin` ссылается на ключ из `skins`. В `skins` поле `model` совпадает с `name` в `Assets/models.json`.
- `behavior` выбирает `patrol`. Это реализованный сейчас режим. Он выдаёт команды `Wait` и `GoTo`.
- `radius` и `height` задают капсулу. `height` включает обе полусферы и должна быть как минимум вдвое больше `radius`.
- `route[0]` задаёт появление. NPC идёт к `route[1]`, обходит точки по порядку и возвращается к `route[0]`.
- Каждая точка задаётся как `{ "position": [x, y, z] }` или `{ "marker": "ИмяBlenderEmpty" }`. Маркер должен попасть в FBX уровня. Маршрут состоит минимум из двух точек.

## 4. Разбери создание и обновление NPC

Эти файлы из шага 1 связывают режиссёра и поведение с игрой. Фрагменты дальше показывают, как они вызывают друг друга.

| Файл | Что делает |
| --- | --- |
| [NpcConfiguration.cs](Actors/NPC/NpcConfiguration.cs) | Читает JSON и проверяет связи `id` и `skin`. `NpcDefinition` проверяет параметры патруля, а `NpcWaypoint` разбирает координаты или маркер. |
| [NpcSystem.cs](Actors/NPC/NpcSystem.cs) | Создаёт контроллеры, регистрирует маршруты в режиссёре, обновляет капсулы и освобождает ресурсы. |
| [NpcController.cs](Actors/NPC/NpcController.cs) | Загружает модель, получает задачу, двигает капсулу и переключает `Idle` / `Walk`. |

В `NpcSystem.Load` конфигурация связывается с одним и тем же классом контроллера.

```csharp
foreach (NpcDefinition definition in config.Actors)
{
    var actor = new NpcController(definition, config.Skins[definition.Skin]);
    system.actors.Add(actor);
    actor.LoadContent(graphics, fallbackTexture, effect);
}
```

На кадре `NpcSystem.Update` передаёт режиссёру признак прибытия с предыдущего обновления. Режиссёр выбирает следующую цель или паузу. `NpcBrain.Tick` разбирает `Wait` и `GoTo` через `switch`. Для `GoTo` он рассчитывает шаг с учётом скорости, времени и расстояния до цели. Контроллер передаёт полученный шаг в `physics.Move`.

```csharp
var order = director.Update(actor.Definition.Id, seconds, actor.ReachedDestination);
actor.Update(level, order, seconds, obstacles);
capsules[i] = actor.Capsule;
```

## 5. Подгони модель под капсулу

Патч обновляет [CharacterAnimationComponent.cs](Component/CharacterAnimationComponent.cs). В `LoadContent` добавлен необязательный `float? targetHeight = null`. Контроллер NPC передаёт `Definition.Height`, а компонент рассчитывает `nextTransform` в том же блоке `try`, где создаётся аниматор. Преобразование сдвигает низ модели к ногам капсулы и подгоняет высоту.

```csharp
Matrix nextTransform = targetHeight is { } height
    ? ItemModelTransform.FitToHeight(replacement.Data, replacement.Hierarchy, height)
    : Matrix.CreateScale(modelScale);
```

Когда все объекты готовы, в конце `try` присвой их полям компонента. Рядом с `renderPose` сохрани `modelTransform = nextTransform`. При ошибке `catch` освобождает новую модель. После успешной замены освобождается предыдущая модель.

**Было в `Draw`**

```csharp
Matrix world = Matrix.CreateScale(modelScale) *
    Matrix.CreateRotationY(RotationY) * Matrix.CreateTranslation(position);
```

**Стало**

```csharp
Matrix world = modelTransform *
    Matrix.CreateRotationY(RotationY) * Matrix.CreateTranslation(position);
```

## 6. Подключи столкновения персонажей

Патч содержит все правки в [CapsuleCollision.cs](Collision/CapsuleCollision.cs) и [Level.CharacterCollision.cs](Levels/Level.CharacterCollision.cs). Они образуют пару. Первый считает контакт и время удара между капсулами, второй сравнивает эти удары со столкновениями с уровнем. Список `obstacles` учитывается также при выходе из пересечения и попытке подъёма на ступень.

Публичная сигнатура `Level.MoveCharacter` доступна для прежних вызовов. Для контроллеров появился внутренний перегруженный метод с дополнительным `ReadOnlySpan<CapsuleShape> obstacles`.

**Было в `CharacterPhysicsComponent.Move`**

```csharp
Position = level.MoveCharacter(Position, movement, Radius, Height,
    CurrentPlatform, ref verticalVelocity, out Level.Platform? platform);
```

**Стало**

```csharp
Position = level.MoveCharacter(Position, movement, Radius, Height,
    CurrentPlatform, ref verticalVelocity, out Level.Platform? platform, obstacles);
```

Патч также проводит `obstacles` через [CharacterPhysicsComponent.cs](Component/CharacterPhysicsComponent.cs), [FirstPersonController.cs](Camera/FirstPersonController.cs), [Player.cs](Actors/Player.cs) и [Skeleton.cs](Actors/Skeleton.cs). Старые публичные перегрузки `Player.Update` и `Skeleton.Update` передают пустой список. `GameplayWorld` вызывает новые перегрузки и даёт им капсулы NPC. Для каждого гражданского `NpcSystem.Update` собирает капсулы игрока, скелета и остальных NPC. Собственная капсула пропускается.

## 7. Запусти систему из `GameplayWorld`

Все правки этого шага находятся в [GameplayWorld.cs](Gameplay/GameplayWorld.cs). Вверху добавь `using _3DLight.Npcs;`, в класс добавь состояние системы и буфер для дверей.

```csharp
private CapsuleShape[] doorOccupants = [];
public NpcSystem Npcs { get; private set; } = null!;
```

В `LoadContent` найди загрузку `ToonShader`. Уровень к этому моменту уже загружен через `levelSession.LoadContent()`.

**Было**

```csharp
Effect toonEffect = content.Load<Effect>("ToonShader");
itemCatalog = ItemCatalog.Load(graphicsDevice, fallbackTexture, toonEffect);
```

**Стало**

```csharp
Effect toonEffect = content.Load<Effect>("ToonShader");
Npcs = NpcSystem.Load(Level, LevelSession.ModelName, graphicsDevice, fallbackTexture, toonEffect);
doorOccupants = new CapsuleShape[2 + Npcs.Capsules.Length];
itemCatalog = ItemCatalog.Load(graphicsDevice, fallbackTexture, toonEffect);
```

В начале `Update` запомни `levelSession.Version` перед `levelSession.Update()`. При смене версии вызови `Npcs.ResetRoutes(Level)`, чтобы позиции маркеров и спавн пересчитались для нового уровня. В `UpdatePlayer` допиши к вызову `Player.Update` аргумент `obstacles: Npcs.Capsules`.

**Было после обработки взаимодействий**

```csharp
UpdateDoors(seconds);
Skeleton.Update(Player, Level, seconds);
```

**Стало**

```csharp
UpdateDoors(seconds);
Skeleton.Update(Player, Level, seconds, Npcs.Capsules);
Npcs.Update(Level, seconds, CharacterCapsule(Player.Position), CharacterCapsule(Skeleton.Position));
```

**Было в `UpdateDoors`**

```csharp
ReadOnlySpan<CapsuleShape> occupants =
[
    new(Player.Position, GameplayCharacters.CharacterCollisionRadius, GameplayCharacters.CharacterCollisionHeight),
    new(Skeleton.Position, GameplayCharacters.CharacterCollisionRadius, GameplayCharacters.CharacterCollisionHeight)
];
Level.UpdateDoors(seconds, occupants);
```

**Стало**

```csharp
private void UpdateDoors(float seconds)
{
    doorOccupants[0] = CharacterCapsule(Player.Position);
    doorOccupants[1] = CharacterCapsule(Skeleton.Position);
    Npcs.Capsules.CopyTo(doorOccupants.AsSpan(2));
    Level.UpdateDoors(seconds, doorOccupants);
}

private static CapsuleShape CharacterCapsule(Vector3 feet) => new(feet,
    GameplayCharacters.CharacterCollisionRadius, GameplayCharacters.CharacterCollisionHeight);
```

В `GameplayWorld.Dispose()` после `disposed = true;` добавь `Npcs?.Dispose();`. Он вызовет `Dispose()` контроллеров и их анимационных компонентов.

## 8. Нарисуй NPC и капсулы

В [GameplayRenderer.Draw](Gameplay/GameplayRenderer.cs) используй очередь, в которую уже отправляются уровень и скелет.

**Было**

```csharp
world.Level.Draw(camera.View, camera.Projection, lighting, queue);
world.Skeleton.Draw(camera.View, camera.Projection, lighting, queue);
world.Items.Draw(camera.View, camera.Projection, lighting, queue);
```

**Стало**

```csharp
world.Level.Draw(camera.View, camera.Projection, lighting, queue);
world.Skeleton.Draw(camera.View, camera.Projection, lighting, queue);
world.Npcs.Draw(camera.View, camera.Projection, lighting, queue);
world.Items.Draw(camera.View, camera.Projection, lighting, queue);
```

Для режима `O` передай `world.Npcs.Capsules` последним аргументом в `world.Level.DrawColliders`. В [Level.cs](Levels/Level.cs) перенеси перегрузку `DrawColliders` с `ReadOnlySpan<CapsuleShape> npcCapsules`. После вывода капсул игрока и скелета она добавляет капсулы NPC.

```csharp
foreach (CapsuleShape npc in npcCapsules)
    AppendCapsuleLines(lines, npc.Bounds, Color.Cyan);
```

## 9. Запусти и проверь

Из корня репозитория выполни сборку, затем запусти игру и нажми «Играть».

```powershell
dotnet build 3DLight.csproj
dotnet run --project 3DLight.csproj --no-build
```

1. Посмотри у колонок и перед входом в магазин. Оба NPC должны ходить по кругу и делать паузы у точек.
2. Встань на пути одного NPC. При контакте капсулы должны остановить сближение. Освободи проход, чтобы NPC продолжил маршрут.
3. Нажми `O` и проверь голубые капсулы. После редактирования JSON собери проект снова, чтобы настройки скопировались в папку запуска.

Для быстрой смены внешности поменяй `skin` с `customer` на `clerk` у любого `actor`. Для смены пути меняй `route` у этой же записи. Дополнительный NPC создаётся новой записью с уникальным `id` и своим маршрутом.

В готовой рабочей копии проверка в [SceneGraphicsChecks.cs](Tools/CollisionTests/SceneGraphicsChecks.cs) загружает реальную заправку, проверяет полный круг обоих NPC и удар движущейся капсулы о NPC. При наличии обновлённого файла проверку можно запустить вместе с проверкой ресурсов сцены после сборки игры.

```powershell
dotnet build Tools/CollisionTests/CollisionTests.csproj
dotnet run --project Tools/CollisionTests/CollisionTests.csproj --no-build -- --scene-resources
```

## Патч для старой копии проекта

Этот патч подготовлен для версии `57efcd7` (`doors crashed`). Он обновляет существующие файлы игры и удаляет старый `Actors/NPC/NPcContract.cs`. Новые `.cs` и JSON бери из полных блоков выше, а модели и текстуры из шага 2.

Сохрани **содержимое** блока `diff` в файл `npc-existing-files.patch` в корне проекта в UTF-8 без BOM. В Windows при сохранении выбери окончания строк LF. Затем проверь и примени его из корня проекта.

```powershell
git apply --check npc-existing-files.patch
```

Если проверка прошла, выполни вторую команду.

```powershell
git apply npc-existing-files.patch
```

Если код ученика отличается от базовой версии, используй контекст строк из блоков «Было → Стало» и полного патча для ручного переноса соответствующих участков.

```diff
diff --git a/3DLight.csproj b/3DLight.csproj
index 76834dc..9939de7 100644
--- a/3DLight.csproj
+++ b/3DLight.csproj
@@ -41,6 +41,7 @@
     <None Update="Content\Assets\Christmas\*.png" CopyToOutputDirectory="PreserveNewest" />
     <None Update="Content\Items\*.json" CopyToOutputDirectory="PreserveNewest" />
     <None Update="Content\Levels\*.json" CopyToOutputDirectory="PreserveNewest" />
+    <None Update="Content\Assets\npc_*\*.png" CopyToOutputDirectory="PreserveNewest" />
     <None Update="Content\Player\*.json" CopyToOutputDirectory="PreserveNewest" />
 	  <None Update="Assets\GasStation\Textures\*.png;Assets\GasStation\Textures\*.jpg;Assets\GasStation\Textures\*.jpeg" TargetPath="Content\Assets\gas_station\%(Filename)%(Extension)" CopyToOutputDirectory="PreserveNewest" />
     <None Update="Content\Assets\Gun\Pistols_Colt_01_A.fbx">
diff --git a/Actors/NPC/NPcContract.cs b/Actors/NPC/NPcContract.cs
deleted file mode 100644
index a1b27e1..0000000
--- a/Actors/NPC/NPcContract.cs
+++ /dev/null
@@ -1,44 +0,0 @@
-﻿using System.Numerics;
-
-namespace Ars3D.NPC;
-
-public enum NpcOrderKind
-{
-    Wait,
-    GoTo,
-    FollowPlayer
-}
-
-public enum NpcState
-{
-    Idle,
-    Moving,
-    Blocked,
-    Hurt,
-    Dead
-}
-
-public readonly record struct NpcOrder(
-    NpcOrderKind Kind,
-    Vector3 Destination,
-    float StopDistance = 0.5f);
-
-public readonly record struct NpcFacts(
-    Vector3 Position,
-    bool IsDead,
-    bool IsHurt,
-    bool SeesPlayer,
-    Vector3 PlayerPosition);
-
-public readonly record struct NpcIntent(
-    Vector3 DesiredVelocity,
-    NpcState State,
-    bool ReachedDestination = false);
-
-public interface INpcNavigation
-{
-    bool TryGetNextWaypoint(
-        Vector3 from,
-        Vector3 destination,
-        out Vector3 waypoint);
-}
\ No newline at end of file
diff --git a/Actors/Player.cs b/Actors/Player.cs
index 86cac4c..70c5397 100644
--- a/Actors/Player.cs
+++ b/Actors/Player.cs
@@ -46,13 +46,17 @@ public sealed class Player
 
     public void SpawnAt(Vector3 position) => physics.SpawnAt(position);
     public void Update(float seconds, Vector2 movement, bool jumpPressed,
-        bool attackPressed, float cameraYaw, Level level)
+        bool attackPressed, float cameraYaw, Level level) =>
+        Update(seconds, movement, jumpPressed, attackPressed, cameraYaw, level, []);
+
+    internal void Update(float seconds, Vector2 movement, bool jumpPressed,
+        bool attackPressed, float cameraYaw, Level level, ReadOnlySpan<CapsuleShape> obstacles)
     {
         if (attackPressed && !IsDead)
             attack.TryStart();
         attack.Update(seconds);
 
         controller.Update(movement, jumpPressed, cameraYaw, level, seconds, Speed,
-            canMove: !IsDead, canJump: !IsDead && !attack.IsAttacking);
+            canMove: !IsDead, canJump: !IsDead && !attack.IsAttacking, obstacles);
     }
 }
diff --git a/Actors/Skeleton.cs b/Actors/Skeleton.cs
index f0c09b1..11041f2 100644
--- a/Actors/Skeleton.cs
+++ b/Actors/Skeleton.cs
@@ -65,7 +65,9 @@ public class Skeleton : IDisposable
 
     public void SpawnAt(Vector3 position) => physics.SpawnAt(position);
 
-    public void Update(Player player, Level level, float deltaTime)
+    public void Update(Player player, Level level, float deltaTime) => Update(player, level, deltaTime, []);
+
+    internal void Update(Player player, Level level, float deltaTime, ReadOnlySpan<CapsuleShape> obstacles)
     {
         //bool samePlatform = CurrentPlatform is not null &&
         //                    player.CurrentPlatform is not null &&
@@ -113,7 +115,7 @@ public class Skeleton : IDisposable
         Vector3 oldPosition = Position;
 
         // Во время Hurt и Dead движение равно Vector3.Zero; Attacking сохраняет преследование.
-        physics.Move(level, horizontalMovement, deltaTime);
+        physics.Move(level, horizontalMovement, deltaTime, obstacles);
 
         bool isRunning = horizontalMovement != Vector3.Zero &&
                          (Position.X != oldPosition.X || Position.Z != oldPosition.Z);
diff --git a/Assets/models.json b/Assets/models.json
index 498482b..05eb278 100644
--- a/Assets/models.json
+++ b/Assets/models.json
@@ -58,6 +58,24 @@
         "Die": "../Content/Assets/Player/RogueDeath.fbx"
       }
     },
+    {
+      "name": "npc_customer",
+      "source": "Characters_psx/npc_customer/Model.fbx",
+      "output": "../Content/Models/npc_customer.3dmodel",
+      "clips": {
+        "Idle": "Characters_psx/npc_customer/Idle.fbx",
+        "Walk": "Characters_psx/npc_customer/Walk.fbx"
+      }
+    },
+    {
+      "name": "npc_clerk",
+      "source": "Characters_psx/npc_clerk/Model.fbx",
+      "output": "../Content/Models/npc_clerk.3dmodel",
+      "clips": {
+        "Idle": "Characters_psx/npc_clerk/Idle.fbx",
+        "Walk": "Characters_psx/npc_clerk/Walk.fbx"
+      }
+    },
     {
       "name": "skeleton",
       "source": "../Content/Assets/Skeleton/SkeletonTPose.fbx",
diff --git a/Camera/FirstPersonController.cs b/Camera/FirstPersonController.cs
index 21a1cc5..b7ad27b 100644
--- a/Camera/FirstPersonController.cs
+++ b/Camera/FirstPersonController.cs
@@ -14,7 +14,8 @@ internal sealed class FirstPersonController
         this.physics = physics;
 
     public void Update(Vector2 movementInput, bool jumpPressed, float yaw, Level level,
-        float seconds, float speed, bool canMove, bool canJump)
+        float seconds, float speed, bool canMove, bool canJump,
+        ReadOnlySpan<CapsuleShape> obstacles = default)
     {
         Yaw = yaw;
         Vector3 movement = canMove
@@ -23,7 +24,7 @@ internal sealed class FirstPersonController
 
         if (canJump && jumpPressed)
             physics.TryJump(JumpImpulse);
-        physics.Move(level, movement, seconds);
+        physics.Move(level, movement, seconds, obstacles);
     }
 
     internal static Vector3 GetMoveDirection(Vector2 movementInput, float yaw)
diff --git a/Collision/CapsuleCollision.cs b/Collision/CapsuleCollision.cs
index 68c8cb6..c4c08cd 100644
--- a/Collision/CapsuleCollision.cs
+++ b/Collision/CapsuleCollision.cs
@@ -21,7 +21,55 @@ internal static class CapsuleCollision
     internal readonly record struct Contact(
         Vector3 AxisPoint, Vector3 SurfacePoint, Vector3 Normal, float Distance, float Penetration);
 
-    internal readonly record struct Hit(float Fraction, Vector3 Normal, Level.TriangleCollider Triangle);
+    internal readonly record struct Hit(float Fraction, Vector3 Normal, Level.TriangleCollider Triangle,
+        bool IsActor = false);
+
+    public static Contact GetContact(CapsuleShape moving, CapsuleShape obstacle)
+    {
+        Vector3 movingAxis = moving.Feet;
+        movingAxis.Y = MathHelper.Clamp(obstacle.AxisStart.Y, moving.AxisStart.Y, moving.AxisEnd.Y);
+        Vector3 obstacleAxis = obstacle.Feet;
+        obstacleAxis.Y = MathHelper.Clamp(movingAxis.Y, obstacle.AxisStart.Y, obstacle.AxisEnd.Y);
+        Vector3 difference = movingAxis - obstacleAxis;
+        float distance = difference.Length();
+        Vector3 normal = distance > DistanceTolerance ? difference / distance : Vector3.Right;
+        return new(movingAxis, obstacleAxis, normal, distance,
+            moving.Radius + obstacle.Radius + SkinWidth - distance);
+    }
+
+    public static bool Cast(CapsuleShape moving, Vector3 movement, CapsuleShape obstacle,
+        float maximumTime, out Hit hit)
+    {
+        float time = 0f;
+        Contact contact = default;
+        for (int iteration = 0; iteration < 48; iteration++)
+        {
+            contact = GetContact(moving with { Feet = moving.Feet + movement * time }, obstacle);
+            float closingSpeed = -Vector3.Dot(movement, contact.Normal);
+            if (closingSpeed <= 0.0000001f)
+            {
+                hit = default;
+                return false;
+            }
+
+            float gap = -contact.Penetration;
+            if (gap <= DistanceTolerance)
+                break;
+
+            float nextTime = time + gap / closingSpeed;
+            if (nextTime > maximumTime)
+            {
+                hit = default;
+                return false;
+            }
+            if (nextTime <= time)
+                break;
+            time = nextTime;
+        }
+
+        hit = new(time, contact.Normal, default, IsActor: true);
+        return true;
+    }
 
     public static Contact GetContact(CapsuleShape capsule, Level.TriangleCollider triangle)
     {
diff --git a/Component/CharacterAnimationComponent.cs b/Component/CharacterAnimationComponent.cs
index f1fb656..7485a3b 100644
--- a/Component/CharacterAnimationComponent.cs
+++ b/Component/CharacterAnimationComponent.cs
@@ -11,32 +11,44 @@ internal sealed class CharacterAnimationComponent : IDisposable
     private CharacterAnimator animator = null!;
     private BonePose[] renderPose = [];
     private readonly float modelScale;
+    private Matrix modelTransform;
 
     public float CurrentClipDuration => animator?.CurrentClipDuration ?? 0f;
     public float RotationY { get; set; }
 
-    public CharacterAnimationComponent(float modelScale) => this.modelScale = modelScale;
+    public CharacterAnimationComponent(float modelScale)
+    {
+        this.modelScale = modelScale;
+        modelTransform = Matrix.CreateScale(modelScale);
+    }
 
     public void LoadContent(
         GraphicsDevice graphicsDevice,
         string modelAssetName,
         string initialClip,
         Texture2D texture,
-        Effect toonEffect)
+        Effect toonEffect,
+        float? targetHeight = null)
     {
         CompiledModel replacement = ModelAssetLoader.Load(
             graphicsDevice, ModelPaths.ModelFile(modelAssetName), texture, toonEffect,
             ModelPaths.TextureDirectory(modelAssetName));
-        AnimationSampler replacementSampler;
-        CharacterAnimator replacementAnimator;
-        BonePose[] replacementPose;
+        CompiledModel? previous = model;
         try
         {
-            replacementSampler = new AnimationSampler(
+            var nextSampler = new AnimationSampler(
                 replacement.Data.Nodes, replacement.Data.Clips);
-            replacementAnimator = new CharacterAnimator(
-                replacementSampler, replacement.Hierarchy, initialClip);
-            replacementPose = new BonePose[replacementSampler.NodeCount];
+            var nextAnimator = new CharacterAnimator(nextSampler, replacement.Hierarchy, initialClip);
+            var nextPose = new BonePose[nextSampler.NodeCount];
+            Matrix nextTransform = targetHeight is { } height
+                ? ItemModelTransform.FitToHeight(replacement.Data, replacement.Hierarchy, height)
+                : Matrix.CreateScale(modelScale);
+
+            model = replacement;
+            sampler = nextSampler;
+            animator = nextAnimator;
+            renderPose = nextPose;
+            modelTransform = nextTransform;
         }
         catch
         {
@@ -44,11 +56,6 @@ internal sealed class CharacterAnimationComponent : IDisposable
             throw;
         }
 
-        CompiledModel? previous = model;
-        model = replacement;
-        sampler = replacementSampler;
-        animator = replacementAnimator;
-        renderPose = replacementPose;
         previous?.Dispose();
     }
 
@@ -94,7 +101,7 @@ internal sealed class CharacterAnimationComponent : IDisposable
     SceneRenderQueue? renderQueue = null)
     {
         Matrix world =
-            Matrix.CreateScale(modelScale) *
+            modelTransform *
             Matrix.CreateRotationY(RotationY) *
             Matrix.CreateTranslation(position);
 
diff --git a/Component/CharacterPhysicsComponent.cs b/Component/CharacterPhysicsComponent.cs
index 70ad7c8..29c10e4 100644
--- a/Component/CharacterPhysicsComponent.cs
+++ b/Component/CharacterPhysicsComponent.cs
@@ -46,7 +46,8 @@ internal sealed class CharacterPhysicsComponent
         CurrentPlatform = null;
     }
 
-    public void Move(Level level, Vector3 horizontalMovement, float deltaTime)
+    public void Move(Level level, Vector3 horizontalMovement, float deltaTime,
+        ReadOnlySpan<CapsuleShape> obstacles = default)
     {
         verticalVelocity += gravity * deltaTime;
         Vector3 movement = horizontalMovement;
@@ -59,7 +60,8 @@ internal sealed class CharacterPhysicsComponent
             Height,
             CurrentPlatform,
             ref verticalVelocity,
-            out Level.Platform? platform);
+            out Level.Platform? platform,
+            obstacles);
         CurrentPlatform = platform;
     }
 }
diff --git a/Gameplay/GameplayRenderer.cs b/Gameplay/GameplayRenderer.cs
index 26b41d9..5c8cdcb 100644
--- a/Gameplay/GameplayRenderer.cs
+++ b/Gameplay/GameplayRenderer.cs
@@ -46,12 +46,13 @@ internal sealed class GameplayRenderer : IDisposable
             graphicsDevice.RasterizerState = RasterizerState.CullNone;
             world.Level.DrawColliders(camera.View, camera.Projection,
                 CreateCharacterBounds(world.Player.Position),
-                CreateCharacterBounds(world.Skeleton.Position));
+                CreateCharacterBounds(world.Skeleton.Position), world.Npcs.Capsules);
             return;
         }
 
         world.Level.Draw(camera.View, camera.Projection, lighting, queue);
         world.Skeleton.Draw(camera.View, camera.Projection, lighting, queue);
+        world.Npcs.Draw(camera.View, camera.Projection, lighting, queue);
         world.Items.Draw(camera.View, camera.Projection, lighting, queue);
         projectileRenderer.Draw(world.Projectiles, camera.View, camera.Projection, lighting, queue);
         sceneRenderer.Draw(camera.View, camera.Projection, lighting);
@@ -68,4 +69,4 @@ internal sealed class GameplayRenderer : IDisposable
         try { projectileRenderer.Dispose(); }
         finally { sceneRenderer.Dispose(); }
     }
-}
\ No newline at end of file
+}
diff --git a/Gameplay/GameplayWorld.cs b/Gameplay/GameplayWorld.cs
index 486d981..c52f588 100644
--- a/Gameplay/GameplayWorld.cs
+++ b/Gameplay/GameplayWorld.cs
@@ -1,5 +1,6 @@
 using _3DLight.Items;
 using _3DLight.Interaction;
+using _3DLight.Npcs;
 using Ars3D.Rendering;
 using Camera;
 using Items;
@@ -17,11 +18,13 @@ internal sealed class GameplayWorld : IDisposable
     private readonly ContentManager content;
     private readonly LevelSession levelSession;
     private ItemCatalog? itemCatalog;
+    private CapsuleShape[] doorOccupants = [];
     private bool disposed;
     private bool flashlightOn = true;
 
     public Player Player { get; } = GameplayCharacters.CreatePlayer();
     public Skeleton Skeleton { get; } = GameplayCharacters.CreateSkeleton();
+    public NpcSystem Npcs { get; private set; } = null!;
     public FirstPersonCamera Camera { get; } = new();
     public SceneLighting Lighting { get; } = new();
     public Level Level => levelSession.Level;
@@ -56,6 +59,8 @@ internal sealed class GameplayWorld : IDisposable
 
         Texture2D fallbackTexture = content.Load<Texture2D>("Assets/Gun/T_PRP_Pistol_Colt_A_BaseColor");
         Effect toonEffect = content.Load<Effect>("ToonShader");
+        Npcs = NpcSystem.Load(Level, LevelSession.ModelName, graphicsDevice, fallbackTexture, toonEffect);
+        doorOccupants = new CapsuleShape[2 + Npcs.Capsules.Length];
         itemCatalog = ItemCatalog.Load(graphicsDevice, fallbackTexture, toonEffect);
         ItemSpawner.SpawnConfigured(Level, itemCatalog, Items, Player.Position);
         Camera.UpdateMatrices(Player.Position, graphicsDevice.Viewport.AspectRatio);
@@ -65,12 +70,16 @@ internal sealed class GameplayWorld : IDisposable
 
     public void Update(float seconds, GameplayInputFrame input)
     {
+        string previousVersion = levelSession.Version;
         levelSession.Update();
+        if (previousVersion != levelSession.Version)
+            Npcs.ResetRoutes(Level);
         UpdatePlayer(seconds, input);
         Interactions.Update(Level.Interactables, Camera.Position, Camera.Forward,
             input.InteractPressed, !input.SuppressInteraction && !Player.IsDead);
         UpdateDoors(seconds);
-        Skeleton.Update(Player, Level, seconds);
+        Skeleton.Update(Player, Level, seconds, Npcs.Capsules);
+        Npcs.Update(Level, seconds, CharacterCapsule(Player.Position), CharacterCapsule(Skeleton.Position));
         Lighting.PointPosition = Player.Position + new Vector3(0f, 1.25f, 0f);
         Projectiles.Update(seconds, Player);
         UpdateItems(seconds, input);
@@ -87,7 +96,8 @@ internal sealed class GameplayWorld : IDisposable
             jumpPressed: input.JumpPressed,
             attackPressed: input.AttackPressed,
             cameraYaw: Camera.Yaw,
-            level: Level);
+            level: Level,
+            obstacles: Npcs.Capsules);
         ApplyPlayerAttack();
         Camera.UpdateMatrices(Player.Position, graphicsDevice.Viewport.AspectRatio);
     }
@@ -112,14 +122,15 @@ internal sealed class GameplayWorld : IDisposable
 
     private void UpdateDoors(float seconds)
     {
-        ReadOnlySpan<CapsuleShape> occupants =
-        [
-            new(Player.Position, GameplayCharacters.CharacterCollisionRadius, GameplayCharacters.CharacterCollisionHeight),
-            new(Skeleton.Position, GameplayCharacters.CharacterCollisionRadius, GameplayCharacters.CharacterCollisionHeight)
-        ];
-        Level.UpdateDoors(seconds, occupants);
+        doorOccupants[0] = CharacterCapsule(Player.Position);
+        doorOccupants[1] = CharacterCapsule(Skeleton.Position);
+        Npcs.Capsules.CopyTo(doorOccupants.AsSpan(2));
+        Level.UpdateDoors(seconds, doorOccupants);
     }
 
+    private static CapsuleShape CharacterCapsule(Vector3 feet) => new(feet,
+        GameplayCharacters.CharacterCollisionRadius, GameplayCharacters.CharacterCollisionHeight);
+
     private void UpdateFlashlight(bool togglePressed)
     {
         if (togglePressed && Items.HeldItem?.State.Definition.Id == "flashlight")
@@ -155,6 +166,7 @@ internal sealed class GameplayWorld : IDisposable
             return;
 
         disposed = true;
+        Npcs?.Dispose();
         itemCatalog?.Dispose();
         Skeleton.Dispose();
         levelSession.Dispose();
diff --git a/Levels/Level.CharacterCollision.cs b/Levels/Level.CharacterCollision.cs
index 3a63959..52c8cc0 100644
--- a/Levels/Level.CharacterCollision.cs
+++ b/Levels/Level.CharacterCollision.cs
@@ -41,7 +41,8 @@ public partial class Level
     private bool CastCapsule(
         CapsuleShape capsule,
         Vector3 movement,
-        out CapsuleCollision.Hit hit)
+        out CapsuleCollision.Hit hit,
+        ReadOnlySpan<CapsuleShape> obstacles = default)
     {
         if (movement.LengthSquared() <= 0.000000000001f)
         {
@@ -62,11 +63,20 @@ public partial class Level
                 startBounds.Max + movement)
                 + new Vector3(CapsuleCollision.SkinWidth));
 
-        return CapsuleCollision.Cast(
+        bool found = CapsuleCollision.Cast(
             capsule,
             movement,
             GetCollisionCandidates(sweptBounds),
             out hit);
+        foreach (CapsuleShape obstacle in obstacles)
+        {
+            if (!CapsuleCollision.Overlaps(sweptBounds, obstacle.Bounds) ||
+                !CapsuleCollision.Cast(capsule, movement, obstacle, found ? hit.Fraction : 1f, out var candidate))
+                continue;
+            hit = candidate;
+            found = true;
+        }
+        return found;
     }
 
     public Vector3 MoveCharacter(
@@ -76,7 +86,18 @@ public partial class Level
         float height,
         Platform? currentPlatform,
         ref float verticalVelocity,
-        out Platform? groundPlatform)
+        out Platform? groundPlatform) => MoveCharacter(position, movement, radius, height,
+            currentPlatform, ref verticalVelocity, out groundPlatform, []);
+
+    internal Vector3 MoveCharacter(
+        Vector3 position,
+        Vector3 movement,
+        float radius,
+        float height,
+        Platform? currentPlatform,
+        ref float verticalVelocity,
+        out Platform? groundPlatform,
+        ReadOnlySpan<CapsuleShape> obstacles)
     {
         if (!float.IsFinite(radius) || radius <= 0f)
             throw new ArgumentOutOfRangeException(nameof(radius));
@@ -91,7 +112,7 @@ public partial class Level
         groundPlatform = null;
 
         var capsule = RecoverPenetration(
-            new CapsuleShape(position, radius, height));
+            new CapsuleShape(position, radius, height), obstacles);
 
         bool canUseSteps =
             currentPlatform is not null && movement.Y <= 0f;
@@ -102,7 +123,7 @@ public partial class Level
         SlideResult horizontal = MoveCapsule(
             capsule,
             horizontalMovement,
-            horizontalPass: true);
+            horizontalPass: true, obstacles);
 
         if (canUseSteps &&
             horizontal.HitWall &&
@@ -110,7 +131,7 @@ public partial class Level
                 capsule,
                 horizontalMovement,
                 horizontal.Position,
-                out Vector3 stepped))
+                out Vector3 stepped, obstacles))
         {
             horizontal = horizontal with
             {
@@ -126,14 +147,14 @@ public partial class Level
         SlideResult vertical = MoveCapsule(
             capsule,
             new Vector3(0f, movement.Y, 0f),
-            horizontalPass: false);
+            horizontalPass: false, obstacles);
 
         capsule = capsule with
         {
             Feet = vertical.Position
         };
 
-        if (movement.Y > 0f && vertical.HitCeiling)
+        if (vertical.HitActor || movement.Y > 0f && vertical.HitCeiling)
             verticalVelocity = 0f;
 
         if (movement.Y <= 0f)
@@ -144,7 +165,7 @@ public partial class Level
 
             Vector3 probe = Vector3.Down * probeDistance;
 
-            if (CastCapsule(capsule, probe, out var groundHit) &&
+            if (CastCapsule(capsule, probe, out var groundHit, obstacles) &&
                 IsWalkable(groundHit))
             {
                 capsule = capsule with
@@ -162,7 +183,7 @@ public partial class Level
     }
 
     private CapsuleShape RecoverPenetration(
-        CapsuleShape capsule)
+        CapsuleShape capsule, ReadOnlySpan<CapsuleShape> obstacles = default)
     {
         for (int iteration = 0; iteration < 12; iteration++)
         {
@@ -194,6 +215,17 @@ public partial class Level
                 normal = contact.Normal;
             }
 
+            foreach (CapsuleShape obstacle in obstacles)
+            {
+                if (!CapsuleCollision.Overlaps(bounds, obstacle.Bounds))
+                    continue;
+                var contact = CapsuleCollision.GetContact(capsule, obstacle);
+                if (contact.Penetration <= deepest)
+                    continue;
+                deepest = contact.Penetration;
+                normal = contact.Normal;
+            }
+
             if (normal == Vector3.Zero)
                 break;
 
@@ -209,17 +241,20 @@ public partial class Level
     private readonly record struct SlideResult(
         Vector3 Position,
         bool HitWall,
-        bool HitCeiling);
+        bool HitCeiling,
+        bool HitActor);
 
     private SlideResult MoveCapsule(
         CapsuleShape capsule,
         Vector3 movement,
-        bool horizontalPass)
+        bool horizontalPass,
+        ReadOnlySpan<CapsuleShape> obstacles)
     {
         Vector3 remaining = movement;
 
         bool hitWall = false;
         bool hitCeiling = false;
+        bool hitActor = false;
 
         Span<Vector3> planes =
             stackalloc Vector3[MaximumSlideIterations];
@@ -231,7 +266,7 @@ public partial class Level
              remaining.LengthSquared() > 0.0000000001f;
              iteration++)
         {
-            if (!CastCapsule(capsule, remaining, out var hit))
+            if (!CastCapsule(capsule, remaining, out var hit, obstacles))
             {
                 capsule = capsule with
                 {
@@ -257,6 +292,7 @@ public partial class Level
                 hit.Normal.Y > -MinimumGroundNormalY;
 
             hitCeiling |= hit.Normal.Y < -0.01f;
+            hitActor |= hit.IsActor;
 
             if (!horizontalPass &&
                 ((movement.Y < 0f && walkable) ||
@@ -287,7 +323,8 @@ public partial class Level
         return new SlideResult(
             capsule.Feet,
             hitWall,
-            hitCeiling);
+            hitCeiling,
+            hitActor);
     }
 
     private static Vector3 ClipAgainstPlanes(
@@ -319,7 +356,8 @@ public partial class Level
         CapsuleShape capsule,
         Vector3 movement,
         Vector3 blockedPosition,
-        out Vector3 result)
+        out Vector3 result,
+        ReadOnlySpan<CapsuleShape> obstacles)
     {
         result = blockedPosition;
 
@@ -328,7 +366,7 @@ public partial class Level
 
         Vector3 up = Vector3.Up * MaximumStepHeight;
 
-        if (CastCapsule(capsule, up, out var ceiling))
+        if (CastCapsule(capsule, up, out var ceiling, obstacles))
             up *= ceiling.Fraction;
 
         if (up.Y <= CapsuleCollision.SkinWidth)
@@ -342,7 +380,7 @@ public partial class Level
         SlideResult forward = MoveCapsule(
             raised,
             movement,
-            horizontalPass: true);
+            horizontalPass: true, obstacles);
 
         Vector3 direction = Vector3.Normalize(movement);
 
@@ -372,7 +410,7 @@ public partial class Level
 
         Vector3 down = Vector3.Down * dropDistance;
 
-        if (!CastCapsule(raised, down, out var landing) ||
+        if (!CastCapsule(raised, down, out var landing, obstacles) ||
             !IsWalkable(landing))
         {
             return false;
@@ -401,7 +439,7 @@ public partial class Level
 
     private static bool IsWalkable(
         CapsuleCollision.Hit hit) =>
-        hit.Normal.Y > 0.01f &&
+        !hit.IsActor && hit.Normal.Y > 0.01f &&
         MathF.Abs(hit.Triangle.Normal.Y) >= MinimumGroundNormalY;
 
     private Platform GetSupport(TriangleCollider triangle) =>
diff --git a/Levels/Level.cs b/Levels/Level.cs
index 15392af..975f53b 100644
--- a/Levels/Level.cs
+++ b/Levels/Level.cs
@@ -206,7 +206,14 @@ namespace _3DLight
             Matrix view,
             Matrix projection,
             BoundingBox playerBounds,
-            BoundingBox skeletonBounds)
+            BoundingBox skeletonBounds) => DrawColliders(view, projection, playerBounds, skeletonBounds, []);
+
+        internal void DrawColliders(
+            Matrix view,
+            Matrix projection,
+            BoundingBox playerBounds,
+            BoundingBox skeletonBounds,
+            ReadOnlySpan<CapsuleShape> npcCapsules)
         {
             if (collisionDebugEffect is null)
                 return;
@@ -226,6 +233,8 @@ namespace _3DLight
 
             AppendCapsuleLines(lines, playerBounds, Color.Red);
             AppendCapsuleLines(lines, skeletonBounds, Color.Orange);
+            foreach (CapsuleShape npc in npcCapsules)
+                AppendCapsuleLines(lines, npc.Bounds, Color.Cyan);
 
             if (lines.Count == 0)
                 return;
```
