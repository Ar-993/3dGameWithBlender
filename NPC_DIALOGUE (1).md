# Диалог с NPC на заправке

В примере `shop_visitor` говорит с игроком. Пока на него смотрят с близкого расстояния, над головой видно `[E] ГОВОРИТЬ`. Первое нажатие `E` открывает окно. Следующие нажатия листают реплики, а `Esc` закрывает разговор. Код рассчитан на версию проекта из [NPC_SYSTEM.md](NPC_SYSTEM.md).

## 1. Какую модель загрузить в Mixamo

Для `shop_visitor` возьми **`Assets/Characters_psx/npc_clerk/Model.fbx`**. Это та же модель и тот же риг, что использует `skin: "clerk"`. Для NPC со скином `customer` соответствующий файл лежит в `Assets/Characters_psx/npc_customer/Model.fbx`. [Скины и NPC заправки](Content/Levels/gas_station.npcs.json)

1. В [Mixamo](https://www.mixamo.com/) нажми **Upload Character** и выбери `npc_clerk/Model.fbx`. Это уже подготовленный FBX со скелетом. Проверь, что персонаж стоит прямо и предпросмотр сохраняет форму рук и ног. [Инструкция Adobe по загрузке персонажа](https://helpx.adobe.com/creative-cloud/help/mixamo-rigging-animation.html)
2. В поиске анимаций введи `Talking`. Выбери спокойные жесты, у которых ступни остаются примерно на месте. Проверь зацикливание в предпросмотре. Игровая капсула во время реплики будет стоять на месте.
3. Скачай FBX с клипом для выбранного персонажа. Для отдельного клипа подходят **FBX Binary**, **Without Skin**, **30 FPS**, если эти параметры доступны в окне загрузки.
4. Сохрани файл как `Assets/Characters_psx/npc_clerk/Talk.fbx`. Реплики добавляются в JSON на шаге 3. `Talk.fbx` содержит жесты персонажа.

### Если жесты на модели выглядят неправильно

Импортируй `Model.fbx` и скачанный клип в Blender. Проверь имена костей, родителей и исходную позу обоих ригов. Импортёр игры сопоставляет каналы по точному имени узла и берёт первый клип из FBX. Совпадение имён помогает импортировать ключи, а совпадение исходных преобразований позволяет правильно их проиграть. [Импорт клипов](Tools/ModelCompiler/AssimpModelImporter.cs)

- Если риг совпал, в **Dope Sheet → Action Editor** выбери действие разговора для арматуры модели и просмотри его. В файле Blender с несколькими слотами выбери слот арматуры, на котором записаны ключи. При корректной позе можно использовать скачанный FBX как `Talk.fbx`.
- Если Mixamo выдал другой риг, перенеси анимацию на арматуру `Model.fbx` и запеки её в отдельное действие. Для одинаковой исходной позы можно связать соответствующие кости корпуса, рук и головы через `Copy Rotation` в локальном пространстве. При разных осях костей сначала выровняй исходные позы в инструменте ретаргетинга. Проверь руки и ноги в начале и в конце клипа. Для запекания в Pose Mode используй **Pose → Animation → Bake Action** с `Visual Keying`. Убери горизонтальный дрейф корня.
- Экспортируй арматуру модели с активным действием в `Talk.fbx`. Для моделей из этого проекта использовались **Forward −Z**, **Up Y**, включённый **Bake Animation**, выключенные **Add Leaf Bones**, **NLA Strips** и **All Actions**. Проверь, что диапазон кадров охватывает весь цикл. [Экспорт FBX в Blender](https://docs.blender.org/manual/en/5.0/addons/import_export/scene_fbx.html)

## 2. Добавь клип в сборку

В `Assets/models.json` найди объект с `"name": "npc_clerk"` и замени его `clips`. Добавляй `Talk` после того, как `Talk.fbx` появился на диске.

**Было**

```json
"clips": {
  "Idle": "Characters_psx/npc_clerk/Idle.fbx",
  "Walk": "Characters_psx/npc_clerk/Walk.fbx"
}
```

**Стало**

```json
"clips": {
  "Idle": "Characters_psx/npc_clerk/Idle.fbx",
  "Walk": "Characters_psx/npc_clerk/Walk.fbx",
  "Talk": "Characters_psx/npc_clerk/Talk.fbx"
}
```

Сборка игры соберёт три клипа в `npc_clerk.3dmodel`. Интерфейс можно собрать и проверить до подготовки клипа. Для этого пока используй прежний `clips` и прежнюю запись `clerk`, тогда во время разговора будет играть `Idle`. [Манифест моделей](Assets/models.json)

## 3. Впиши реплики и добавь данные диалога

В `Content/Levels/gas_station.npcs.json` найди объект с `"id": "shop_visitor"`. Добавь `dialogue` рядом с `behavior`. Его маршрут, `skin` и остальные поля сохрани.

<!-- replace: Content/Levels/gas_station.npcs.json -->
**Было**

```json
      "id": "shop_visitor",
      "skin": "clerk",
      "behavior": "patrol",
```

**Стало**

```json
      "id": "shop_visitor",
      "skin": "clerk",
      "behavior": "patrol",
      "dialogue": {
        "name": "Кассир",
        "lines": [
          "Добрый вечер. Чем могу помочь?",
          "Колонки справа от входа.",
          "Если что, я буду у магазина."
        ]
      },
```

Когда добавишь `Talk` в сборку, замени запись в `skins`.

```json
"clerk": { "model": "npc_clerk", "idleClip": "Idle", "walkClip": "Walk", "talkClip": "Talk" }
```

В `Actors/NPC/NpcConfiguration.cs` допиши поля и вызов проверки. Конфигурация использует `UnmappedMemberHandling.Disallow`, поэтому добавлять поля в JSON нужно вместе с изменением C#.

<!-- replace: Actors/NPC/NpcConfiguration.cs -->
**Было в `NpcSkin`**

```csharp
    public string WalkClip { get; init; } = "Walk";
```

**Стало**

```csharp
    public string WalkClip { get; init; } = "Walk";
    public string? TalkClip { get; init; }
```

<!-- replace: Actors/NPC/NpcConfiguration.cs -->
**Было в `NpcDefinition`**

```csharp
    public NpcWaypoint[] Route { get; init; } = [];

    public void Validate()
    {
```

**Стало**

```csharp
    public NpcWaypoint[] Route { get; init; } = [];
    public NpcDialogue? Dialogue { get; init; }

    public void Validate()
    {
        Dialogue?.Validate(Id);
```

### `Actors/NPC/NpcDialogue.cs`

Создай файл целиком. `NpcTalkTarget` даёт системе взаимодействия цель на капсуле. `DialogueSession` хранит активного собеседника и номер реплики.

<!-- new: Actors/NPC/NpcDialogue.cs -->
```csharp
using _3DLight.Interaction;
using Microsoft.Xna.Framework;

namespace _3DLight.Npcs;

internal sealed class NpcDialogue
{
    public string Name { get; init; } = "";
    public string[] Lines { get; init; } = [];

    public void Validate(string npcId)
    {
        if (string.IsNullOrWhiteSpace(Name))
            throw new InvalidDataException($"У диалога NPC '{npcId}' нужно указать name.");
        if (Lines is not { Length: > 0 } || Array.Exists(Lines, string.IsNullOrWhiteSpace))
            throw new InvalidDataException($"У диалога NPC '{npcId}' нужны заполненные lines.");
    }
}

internal sealed class DialogueSession
{
    private NpcController? speaker;
    private NpcDialogue? dialogue;
    private int lineIndex;

    public bool IsOpen => speaker is not null;
    public string SpeakerName => dialogue?.Name ?? "";
    public string Line => dialogue is null ? "" : dialogue.Lines[lineIndex];
    public Vector3? SpeakerAnchor => speaker?.DialogueAnchor;

    public void Open(NpcController actor, NpcDialogue script)
    {
        actor.BeginDialogue();
        speaker = actor;
        dialogue = script;
        lineIndex = 0;
    }

    public void Update(float seconds, bool advance, bool cancel)
    {
        if (speaker is null)
            return;

        if (cancel || advance && lineIndex == dialogue!.Lines.Length - 1)
        {
            Close();
            return;
        }

        if (advance)
            lineIndex++;

        speaker.UpdateDialogue(seconds);
    }

    public void Close()
    {
        speaker?.EndDialogue();
        speaker = null;
        dialogue = null;
        lineIndex = 0;
    }
}

internal sealed class NpcTalkTarget(
    NpcController actor, NpcDialogue script, DialogueSession session) : IInteractable
{
    private static readonly IReadOnlySet<int> NoMeshIds = new HashSet<int>();

    public string InteractionText => "ГОВОРИТЬ";
    public float InteractionDistance => 2.5f;
    public IReadOnlySet<int> MeshIds => NoMeshIds;
    public Vector3 BubbleAnchor => actor.DialogueAnchor;

    public float? GetHitDistance(Ray ray) => ray.Intersects(actor.Capsule.Bounds);

    public void Interact() => session.Open(actor, script);
}
```

## 4. Подключи анимацию и цель взаимодействия

В `NpcController` точка бабла зависит от текущей позиции и высоты капсулы. Методы разговора управляют существующим `CharacterAnimationComponent`.

<!-- replace: Actors/NPC/NpcController.cs -->
**Было**

```csharp
    public void SpawnAt(Vector3 position)
```

**Стало**

```csharp
    public Vector3 DialogueAnchor => Capsule.Feet + Vector3.Up * (Capsule.Height + 0.18f);

    public void BeginDialogue() => animation.Restart(skin.TalkClip ?? skin.IdleClip, loop: true);
    public void UpdateDialogue(float seconds) => animation.Update(seconds);
    public void EndDialogue() => animation.Play(skin.IdleClip, loop: true);

    public void SpawnAt(Vector3 position)
```

В конец `NpcController.LoadContent` добавь проверку имени клипа, чтобы сразу поймать ошибку в манифесте или скине.

<!-- replace: Actors/NPC/NpcController.cs -->
**Было**

```csharp
            throw new InvalidDataException($"Для NPC '{Definition.Id}' нужны клипы ожидания и ходьбы.");
```

**Стало**

```csharp
            throw new InvalidDataException($"Для NPC '{Definition.Id}' нужны клипы ожидания и ходьбы.");
        if (skin.TalkClip is { } talkClip && animation.GetClipDuration(talkClip) <= 0f)
            throw new InvalidDataException($"У NPC '{Definition.Id}' talkClip должен иметь положительную длительность.");
```

В `NpcSystem` добавь список целей, обёрнутых в `NpcTalkTarget`. Отдельный список полезен при перезагрузке уровня, так как `Level` пересоздаёт свой список дверей.

<!-- replace: Actors/NPC/NpcSystem.cs -->
**Было**

```csharp
using Ars3D.Rendering;
```

**Стало**

```csharp
using _3DLight.Interaction;
using Ars3D.Rendering;
```

<!-- replace: Actors/NPC/NpcSystem.cs -->
**Было**

```csharp
    public ReadOnlySpan<CapsuleShape> Capsules => capsules;

    public static NpcSystem Load(Level level, string levelName, GraphicsDevice graphics,
        Texture2D fallbackTexture, Effect effect)
```

**Стало**

```csharp
    private readonly List<IInteractable> talkTargets = [];

    public ReadOnlySpan<CapsuleShape> Capsules => capsules;
    public IEnumerable<IInteractable> TalkTargets => talkTargets;

    public static NpcSystem Load(Level level, string levelName, GraphicsDevice graphics,
        Texture2D fallbackTexture, Effect effect, DialogueSession dialogue)
```

<!-- replace: Actors/NPC/NpcSystem.cs -->
**Было в цикле загрузки**

```csharp
                actor.LoadContent(graphics, fallbackTexture, effect);
```

**Стало**

```csharp
                actor.LoadContent(graphics, fallbackTexture, effect);
                if (definition.Dialogue is { } script)
                    system.talkTargets.Add(new NpcTalkTarget(actor, script, dialogue));
```

<!-- replace: Actors/NPC/NpcSystem.cs -->
**Было в `Dispose`**

```csharp
        actors.Clear();
```

**Стало**

```csharp
        actors.Clear();
        talkTargets.Clear();
```

## 5. Разведи нажатия E и Esc

`E` уже учитывается по новому нажатию. При открытом окне это кнопка следующей реплики. `Esc` закрывает диалог в обход обычного переключателя паузы.

<!-- replace: Gameplay/GameplayInputFrame.cs -->
**Было в `GameplayInputFrame`**

```csharp
    public bool InteractPressed { get; init; }
```

**Стало**

```csharp
    public bool InteractPressed { get; init; }
    public bool CloseDialoguePressed { get; init; }
```

<!-- replace: Gameplay/GameplayInput.cs -->
**Было в `GameplayInput`**

```csharp
    public GameplayInputFrame Update()
    {
        GameplayInputFrame frame = state.Update(
            Keyboard.GetState(), Mouse.GetState(), game.IsActive, game.Window.ClientBounds);
```

**Стало**

```csharp
    public GameplayInputFrame Update(bool dialogueOpen = false)
    {
        GameplayInputFrame frame = state.Update(
            Keyboard.GetState(), Mouse.GetState(), game.IsActive, game.Window.ClientBounds, dialogueOpen);
```

<!-- replace: Gameplay/GameplayInputState.cs -->
**Было в `GameplayInputState.Update`**

```csharp
        Rectangle clientBounds)
    {
        bool justActivated = isActive && !wasActive;
```

**Стало**

```csharp
        Rectangle clientBounds,
        bool dialogueOpen = false)
    {
        bool justActivated = isActive && !wasActive;
```

<!-- replace: Gameplay/GameplayInputState.cs -->
**Было внутри ветки активного окна**

```csharp
            if (Pressed(keyboard, Keys.Escape))
            {
                IsPaused = !IsPaused;
                MouseCaptured = !IsPaused;
                capturedThisFrame = MouseCaptured;
            }

            frame = IsPaused
                ? SuspendedFrame(keyboard)
                : ReadGameplayActions(keyboard, mouse, clientBounds, justActivated, capturedThisFrame);
```

**Стало**

```csharp
            bool escapePressed = Pressed(keyboard, Keys.Escape);
            if (escapePressed && !dialogueOpen)
            {
                IsPaused = !IsPaused;
                MouseCaptured = !IsPaused;
                capturedThisFrame = MouseCaptured;
            }

            frame = IsPaused
                ? SuspendedFrame(keyboard)
                : ReadGameplayActions(keyboard, mouse, clientBounds, justActivated, capturedThisFrame);

            if (dialogueOpen)
            {
                frame = frame with
                {
                    InteractPressed = !justActivated && Pressed(keyboard, Keys.E),
                    CloseDialoguePressed = escapePressed
                };
            }
```

<!-- replace: Scenes/GameplayScene.cs -->
**Было в `GameplayScene.Update`**

```csharp
        GameplayInputFrame frame = input.Update();
```

**Стало**

```csharp
        GameplayInputFrame frame = input.Update(world.Dialogue.IsOpen);
```

## 6. Включи диалог в игровой мир

В `GameplayWorld` добавь одну сессию диалога на весь мир и передай её в загрузчик NPC.

<!-- replace: Gameplay/GameplayWorld.cs -->
**Было**

```csharp
    public NpcSystem Npcs { get; private set; } = null!;
```

**Стало**

```csharp
    public NpcSystem Npcs { get; private set; } = null!;
    public DialogueSession Dialogue { get; } = new();
```

<!-- replace: Gameplay/GameplayWorld.cs -->
**Было в `LoadContent`**

```csharp
        Npcs = NpcSystem.Load(Level, LevelSession.ModelName, graphicsDevice, fallbackTexture, toonEffect);
```

**Стало**

```csharp
        Npcs = NpcSystem.Load(Level, LevelSession.ModelName, graphicsDevice, fallbackTexture, toonEffect, Dialogue);
```

Во время разговора обновляй реплику и анимацию собеседника. Остальная симуляция продолжится на следующем кадре после закрытия. Так игрок сможет читать, пока NPC стоит на месте.

<!-- replace: Gameplay/GameplayWorld.cs -->
**Было в начале `Update`**

```csharp
    public void Update(float seconds, GameplayInputFrame input)
    {
        string previousVersion = levelSession.Version;
```

**Стало**

```csharp
    public void Update(float seconds, GameplayInputFrame input)
    {
        if (Dialogue.IsOpen)
        {
            Items.SuppressInput(input.InteractDown);
            Dialogue.Update(seconds, input.InteractPressed, input.CloseDialoguePressed);
            return;
        }

        string previousVersion = levelSession.Version;
```

Цель по лучу теперь выбирается среди дверей и NPC. Существующая проверка видимости учитывает стены и двери. В кадре открытия разговора `return` перед обновлением предметов даёт первой клавише `E` ровно одно действие.

<!-- replace: Gameplay/GameplayWorld.cs -->
**Было после `UpdatePlayer`**

```csharp
        Interactions.Update(Level.Interactables, Camera.Position, Camera.Forward,
            input.InteractPressed, !input.SuppressInteraction && !Player.IsDead);
        UpdateDoors(seconds);
```

**Стало**

```csharp
        Interactions.Update(Level.Interactables.Concat(Npcs.TalkTargets), Camera.Position, Camera.Forward,
            input.InteractPressed, !input.SuppressInteraction && !Player.IsDead);
        if (Dialogue.IsOpen)
        {
            Items.SuppressInput(input.InteractDown);
            return;
        }

        UpdateDoors(seconds);
```

<!-- replace: Gameplay/GameplayWorld.cs -->
**Было в `Dispose`**

```csharp
        Npcs?.Dispose();
```

**Стало**

```csharp
        Dialogue.Close();
        Npcs?.Dispose();
```

## 7. Собери окно и бабл в Gum

В проекте `GameUi.Initialize` уже запускает Gum, а `GameUi.Draw` отрисовывает его. В примере фон окна, фон бабла и короткий указатель сделаны из `ColoredRectangleRuntime`. Для кириллицы текст рисуется через готовый `Fonts/OldStandardText` тем же способом, что подсказки предметов. [Gum в коде](https://docs.flatredball.com/gum/code/about/gum-in-code) · [Шрифт игры](Content/Fonts/README.md)

### `Ui/DialogueUi.cs`

Скопируй класс целиком. `UseSpeechBubble = false` показывает реплики в нижнем окне. Поставь `true`, чтобы показывать реплику в бабле над головой, когда она там помещается. Подсказка `[E] ГОВОРИТЬ` показывается над NPC в обоих режимах.

<!-- new: Ui/DialogueUi.cs -->
```csharp
using _3DLight.Npcs;
using Gum.GueDeriving;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using BlendState = Microsoft.Xna.Framework.Graphics.BlendState;

namespace _3DLight.Ui;

internal sealed class DialogueUi
{
    private const bool UseSpeechBubble = false;
    private const int Padding = 12;
    private const string Controls = "[E] ДАЛЬШЕ   [Esc] ЗАКРЫТЬ";

    private readonly ColoredRectangleRuntime panel = CreateBackground();
    private readonly ColoredRectangleRuntime bubble = CreateBackground();
    private readonly ColoredRectangleRuntime tail = CreateBackground();
    private Vector2 textPosition;
    private string text = "";

    public bool HasBubble => bubble.Visible;

    public void Layout(Viewport viewport, SpriteFont font, DialogueSession dialogue, Vector2? anchor)
    {
        panel.Visible = false;
        bubble.Visible = false;
        tail.Visible = false;

        if (dialogue.IsOpen)
        {
            if (UseSpeechBubble && anchor is { } head &&
                TryPlaceBubble(viewport, font, head, dialogue.SpeakerName + "\n" + dialogue.Line + "\n" + Controls))
                return;

            int width = Math.Min(760, viewport.Width - 24);
            text = dialogue.SpeakerName + "\n\n" +
                Wrap(font, dialogue.Line, width - 2 * Padding) + "\n\n" + Controls;
            int height = (int)MathF.Ceiling(font.MeasureString(text).Y) + 2 * Padding;
            int x = (viewport.Width - width) / 2;
            int y = Math.Max(8, viewport.Height - height - 24);
            SetBounds(panel, x, y, width, height);
            textPosition = new Vector2(x + Padding, y + Padding);
            return;
        }

        if (anchor is { } target)
            TryPlaceBubble(viewport, font, target, "[E] ГОВОРИТЬ");
    }

    public void DrawText(SpriteBatch batch, SpriteFont font)
    {
        if (!panel.Visible && !bubble.Visible)
            return;

        batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone);
        batch.DrawString(font, text, textPosition, Color.White);
        batch.End();
    }

    private bool TryPlaceBubble(Viewport viewport, SpriteFont font, Vector2 anchor, string message)
    {
        string wrapped = Wrap(font, message, Math.Min(300, viewport.Width - 40));
        Vector2 size = font.MeasureString(wrapped);
        int width = (int)MathF.Ceiling(size.X) + 2 * Padding;
        int height = (int)MathF.Ceiling(size.Y) + 2 * Padding;
        int y = (int)anchor.Y - height - 8;
        if (width > viewport.Width - 16 || y < 8)
            return false;

        int x = Math.Clamp((int)anchor.X - width / 2, 8, viewport.Width - width - 8);
        int tailX = Math.Clamp((int)anchor.X - 3, x, x + width - 6);
        SetBounds(bubble, x, y, width, height);
        SetBounds(tail, tailX, y + height, 6, 8);
        text = wrapped;
        textPosition = new Vector2(x + Padding, y + Padding);
        return true;
    }

    private static ColoredRectangleRuntime CreateBackground()
    {
        var rectangle = new ColoredRectangleRuntime
        {
            Color = new Color(18, 20, 24),
            Alpha = 235,
            Visible = false
        };
        rectangle.AddToRoot();
        return rectangle;
    }

    private static void SetBounds(ColoredRectangleRuntime rectangle, int x, int y, int width, int height)
    {
        rectangle.X = x;
        rectangle.Y = y;
        rectangle.Width = width;
        rectangle.Height = height;
        rectangle.Visible = true;
    }

    private static string Wrap(SpriteFont font, string text, float width)
    {
        var lines = new List<string>();
        foreach (string paragraph in text.Replace("\r", "").Split('\n'))
        {
            string line = "";
            foreach (string word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                string candidate = line.Length == 0 ? word : line + " " + word;
                if (line.Length > 0 && font.MeasureString(candidate).X > width)
                {
                    lines.Add(line);
                    line = word;
                }
                else
                {
                    line = candidate;
                }
            }
            lines.Add(line);
        }
        return string.Join("\n", lines);
    }
}
```

В `GameUi` создавай `DialogueUi` после `gum.Initialize`. Фоны прикрепляются к корню Gum один раз. `Layout` меняет их положение и видимость в каждом кадре, а `gum.Uninitialize()` освобождает дерево при выгрузке сцены.

<!-- replace: Ui/GameUi.cs -->
**Было**

```csharp
using _3DLight.Ui;
```

**Стало**

```csharp
using _3DLight.Npcs;
using _3DLight.Ui;
```

<!-- replace: Ui/GameUi.cs -->
**Было у полей**

```csharp
    private SpriteFont interfaceFont = null!;
```

**Стало**

```csharp
    private SpriteFont interfaceFont = null!;
    private DialogueUi dialogueUi = null!;
```

<!-- replace: Ui/GameUi.cs -->
**Было в `Initialize`**

```csharp
        inventoryIcons = new InventoryIconCache(game.GraphicsDevice);
```

**Стало**

```csharp
        inventoryIcons = new InventoryIconCache(game.GraphicsDevice);
        dialogueUi = new DialogueUi();
```

<!-- replace: Ui/GameUi.cs -->
**Было в `Draw`**

```csharp
    public void Draw()
    {
        gum.Draw();
```

**Стало**

```csharp
    public void SetDialogue(DialogueSession dialogue, Vector2? bubbleAnchor) =>
        dialogueUi.Layout(spriteBatch.GraphicsDevice.Viewport, interfaceFont, dialogue, bubbleAnchor);

    public void Draw()
    {
        gum.Draw();
        dialogueUi.DrawText(spriteBatch, interfaceFont);
```

Когда бабл виден, обычную подсказку снизу заменяет надпись над NPC.

<!-- replace: Ui/GameUi.cs -->
**Было**

```csharp
        if (worldInteractionText is not null || interactionItemName is not null && !interactionItemIsHeld)
```

**Стало**

```csharp
        if (!dialogueUi.HasBubble &&
            (worldInteractionText is not null || interactionItemName is not null && !interactionItemIsHeld))
```

## 8. Переведи точку над головой в координаты экрана

`Viewport.Project` переводит координаты уровня в пиксели окна. `NpcController.DialogueAnchor` уже даёт точку чуть выше капсулы. Проекция пересчитывается после отрисовки мира, когда снова активен основной viewport.

<!-- replace: Gameplay/GameplayHud.cs -->
**Было в `GameplayHud.cs`**

```csharp
using _3DLight.Items;
```

**Стало**

```csharp
using _3DLight.Items;
using _3DLight.Npcs;
```

<!-- replace: Gameplay/GameplayHud.cs -->
**Было**

```csharp
    public void PrepareInventory(ItemInteraction interaction)
```

**Стало**

```csharp
    public void SetDialogue(DialogueSession dialogue, Vector2? bubbleAnchor) =>
        ui.SetDialogue(dialogue, bubbleAnchor);

    public void PrepareInventory(ItemInteraction interaction)
```

<!-- replace: Scenes/GameplayScene.cs -->
**Было в `GameplayScene.cs`**

```csharp
using _3DLight.Gameplay;
```

**Стало**

```csharp
using _3DLight.Gameplay;
using _3DLight.Npcs;
```

<!-- replace: Scenes/GameplayScene.cs -->
**Было в `GameplayScene.Draw`**

```csharp
    public void Draw(GameTime gameTime)
    {
        hud.PrepareInventory(world.Items);
        renderer.Draw(world);
        hud.Draw(world.Player, world.Skeleton, world.Items, world.LevelVersion,
            world.Lighting.Enabled, world.ShowCollisionDebug,
            game.IsActive && input.MouseCaptured && !input.IsPaused && !world.Player.IsDead,
            world.Interactions.Hint);
    }
```

**Стало**

```csharp
    public void Draw(GameTime gameTime)
    {
        hud.PrepareInventory(world.Items);
        renderer.Draw(world);

        bool canInteract = game.IsActive && input.MouseCaptured && !input.IsPaused &&
            !world.Player.IsDead && !world.Dialogue.IsOpen;
        Vector3? anchor = world.Dialogue.SpeakerAnchor;
        if (canInteract && world.Interactions.Target is NpcTalkTarget target)
            anchor = target.BubbleAnchor;

        hud.SetDialogue(world.Dialogue, anchor is { } point ? ProjectBubble(point) : null);
        hud.Draw(world.Player, world.Skeleton, world.Items, world.LevelVersion,
            world.Lighting.Enabled, world.ShowCollisionDebug, canInteract, world.Interactions.Hint);
    }

    private Vector2? ProjectBubble(Vector3 point)
    {
        var camera = world.Camera;
        if (Vector3.Dot(point - camera.Position, camera.Forward) <= 0f)
            return null;

        var viewport = game.GraphicsDevice.Viewport;
        Vector3 projected = viewport.Project(point, camera.Projection, camera.View, Matrix.Identity);
        float x = projected.X - viewport.X;
        float y = projected.Y - viewport.Y;
        if (projected.Z < 0f || projected.Z > 1f || x < 0f || x >= viewport.Width ||
            y < 0f || y >= viewport.Height)
            return null;

        return new Vector2(x, y);
    }
```

## 9. Если нужен и сам текст в Gum

Рабочий пример выше использует готовый `SpriteFont`, который уже содержит русский алфавит. Если нужен редактируемый визуал целиком в Gum Tool, создай компонент с `Container`, добавь `ColoredRectangle` как фон и `Text` с отступами. Бабл можно собрать теми же элементами. Для указателя добавь маленький `ColoredRectangle` или спрайт треугольника под фоном.

1. Создай шрифт BMFont с латиницей, кириллицей и `Ёё`. Сохрани `Dialogue.fnt` и его `.png` рядом в `Content/Fonts/Dialogue`. Для генерации можно взять существующий `OldStandard-Regular.ttf`. [Создание шрифта для Gum](https://docs.flatredball.com/gum/gum-tool/gum-elements/text/use-custom-font)
2. Для `Text` включи `Use Custom Font` и выбери `Dialogue.fnt`. При создании через C# тот же шрифт можно указать через `UseCustomFont = true` и `CustomFontFile = Path.Combine(AppContext.BaseDirectory, "Content", "Fonts", "Dialogue", "Dialogue.fnt")`. Установи `Width` под фон с отступами и передавай реплику в `TextRuntime.Text`. [Шрифты TextRuntime](https://docs.flatredball.com/gum/code/standard-visuals/textruntime/fonts)
3. Скопируй `.fnt` и PNG в папку запуска через `<None Update="Content\Fonts\Dialogue\*.*" CopyToOutputDirectory="PreserveNewest" />`. Gum загружает эту пару с диска. [Загрузка файлов Gum](https://docs.flatredball.com/gum/code/files-and-fonts/file-loading)
4. После переноса надписей в `TextRuntime` выведи их через существующий `gum.Draw()`. Фон и текст удобно двигать вместе, поместив их в один контейнер.

## 10. Проверь по одному действию

1. Собери `dotnet build 3DLight.csproj`. Если `Talk.fbx` ещё готовится, используй `Idle` по умолчанию, как на шаге 2.
2. В игре подойди к `shop_visitor` перед магазином и наведи прицел. Бабл появляется над головой. Отвернись или отойди, чтобы проверить исчезновение подсказки.
3. Нажми `E`. Ожидается первая реплика, остановка движения и анимация `Talk` или `Idle`. Удержание `E` оставляет текущую строку на экране.
4. Отпусти `E` и нажми ещё раз. Дойди до последней реплики и закрой окно следующим нажатием. Начни разговор снова и проверь `Esc`. В обоих случаях NPC продолжит маршрут.
5. Проверь `UseSpeechBubble = true` и поговори с NPC с разных расстояний. Когда бабл помещается над головой, в нём появится реплика. Когда над головой мало места, реплика появится в нижнем окне.
6. Проверь дверь и предмет рядом. При открытии разговора `E` уходит в сессию диалога. При закрытом окне существующая система снова выбирает цель по лучу и дистанции.
