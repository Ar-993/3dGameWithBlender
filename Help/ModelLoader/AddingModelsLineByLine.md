# Добавление моделей: построчный разбор

Ниже создаются два объекта:

1. Статический ящик `Crate`.
2. Анимированный противник `Orc`.

Каждый пример сначала приведён целиком, затем разобран строка за строкой.

# Часть 1. Статическая модель

## 1. Файлы

```text
Content/Assets/Props/Crate.fbx
Content/Assets/Props/crate_texture.png
```

- `Crate.fbx` содержит геометрию, normal vectors и UV.
- `crate_texture.png` содержит цветовую текстуру.

## 2. Запись в `Assets/models.json`

```json
{
  "name": "crate",
  "source": "../Content/Assets/Props/Crate.fbx",
  "output": "../Content/Models/crate.3dmodel"
}
```

Построчно:

```json
{
```

Начинается описание одной модели.

```json
"name": "crate",
```

`crate` — runtime-имя. Игра будет загружать модель вызовом:

```csharp
CompiledModel.Load(..., "crate", ...);
```

```json
"source": "../Content/Assets/Props/Crate.fbx",
```

Путь к исходному FBX относительно папки `Assets`, в которой находится `models.json`.

```json
"output": "../Content/Models/crate.3dmodel"
```

Путь, куда ModelCompiler запишет готовый бинарник.

```json
}
```

Описание модели закончено. Если после него идёт следующая модель, нужна запятая.

## 3. Запись текстуры в MGCB

```text
#begin Assets/Props/crate_texture.png
/importer:TextureImporter
/processor:TextureProcessor
/processorParam:GenerateMipmaps=True
/processorParam:PremultiplyAlpha=True
/processorParam:TextureFormat=Color
/build:Assets/Props/crate_texture.png
```

Разбор:

- `#begin` начинает описание content asset.
- `TextureImporter` читает PNG.
- `TextureProcessor` преобразует его в XNB.
- `GenerateMipmaps=True` создаёт уменьшенные версии текстуры для дальних объектов.
- `PremultiplyAlpha=True` подготавливает alpha для MonoGame.
- `TextureFormat=Color` сохраняет цветовой формат.
- `/build` указывает входной файл и asset name.

В C# расширение не указывается:

```csharp
content.Load<Texture2D>("Assets/Props/crate_texture");
```

## 4. Полный класс `Crate`

```csharp
using _3DLight;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

public sealed class Crate
{
    private CompiledModel model = null!;

    public Vector3 Position { get; set; }
    public float RotationY { get; set; }
    public float Scale { get; set; } = 1f;

    public void LoadContent(
        GraphicsDevice graphicsDevice,
        ContentManager content)
    {
        Texture2D texture = content.Load<Texture2D>(
            "Assets/Props/crate_texture");

        Effect toonEffect = content.Load<Effect>(
            "ToonShader");

        model = CompiledModel.Load(
            graphicsDevice,
            "crate",
            texture,
            toonEffect);
    }

    public void Draw(Matrix view, Matrix projection)
    {
        Matrix world =
            Matrix.CreateScale(Scale) *
            Matrix.CreateRotationY(RotationY) *
            Matrix.CreateTranslation(Position);

        model.Draw(world, view, projection);
    }
}
```

## 5. Разбор `Crate` построчно

```csharp
using _3DLight;
```

Подключает namespace, в котором находится `CompiledModel`.

```csharp
using Microsoft.Xna.Framework;
```

Подключает `Vector3` и `Matrix`.

```csharp
using Microsoft.Xna.Framework.Content;
```

Подключает `ContentManager` для загрузки XNB-текстуры и эффекта.

```csharp
using Microsoft.Xna.Framework.Graphics;
```

Подключает `GraphicsDevice`, `Texture2D` и `Effect`.

```csharp
public sealed class Crate
```

Создаётся игровой класс ящика. `sealed` запрещает наследование, если оно не нужно.

```csharp
private CompiledModel model = null!;
```

Поле будет хранить загруженную модель. `null!` сообщает nullable-анализатору: поле будет заполнено в `LoadContent` до первого `Draw`.

```csharp
public Vector3 Position { get; set; }
```

Положение ящика в мировых координатах.

```csharp
public float RotationY { get; set; }
```

Поворот вокруг вертикальной оси в радианах.

```csharp
public float Scale { get; set; } = 1f;
```

Масштаб модели. `1f` означает исходный размер.

```csharp
public void LoadContent(
    GraphicsDevice graphicsDevice,
    ContentManager content)
```

Метод вызывается один раз из `LightGame.LoadContent`.

```csharp
Texture2D texture = content.Load<Texture2D>(
    "Assets/Props/crate_texture");
```

Из XNB загружается текстура. Путь совпадает с asset name в MGCB.

```csharp
Effect toonEffect = content.Load<Effect>(
    "ToonShader");
```

Загружается скомпилированный `ToonShader.fx`.

```csharp
model = CompiledModel.Load(
    graphicsDevice,
    "crate",
    texture,
    toonEffect);
```

Происходит runtime-загрузка:

1. Открывается `Content/Models/crate.3dmodel`.
2. Читаются nodes, meshes и clips.
3. Создаются `VertexBuffer` и `IndexBuffer`.
4. Для мешей клонируется ToonShader.

```csharp
public void Draw(Matrix view, Matrix projection)
```

Метод вызывается каждый кадр из `LightGame.Draw`.

```csharp
Matrix.CreateScale(Scale)
```

Создаёт масштабирование вокруг локального origin.

```csharp
Matrix.CreateRotationY(RotationY)
```

Создаёт поворот вокруг Y.

```csharp
Matrix.CreateTranslation(Position)
```

Переносит объект в игровую позицию.

Порядок умножения важен:

```text
Scale → Rotation → Translation
```

Сначала меняется размер, затем объект поворачивается, затем переносится.

```csharp
model.Draw(world, view, projection);
```

Вызывается статическая перегрузка `Draw`. Она использует bind pose и рисует меши.

## 6. Подключение `Crate` в `LightGame`

Поле:

```csharp
private readonly Crate crate = new();
```

Объект создаётся один раз вместе с игрой.

В `LoadContent`:

```csharp
crate.LoadContent(GraphicsDevice, Content);
crate.Position = new Vector3(845f, 18f, 45f);
```

Первая строка загружает ресурсы. Вторая задаёт стартовую позицию.

В `Draw`:

```csharp
crate.Draw(camera.View, camera.Projection);
```

Передаются текущие матрицы камеры.

# Часть 2. Анимированный персонаж

## 7. Файлы Orc

```text
Content/Assets/Orc/OrcTPose.fbx
Content/Assets/Orc/OrcIdle.fbx
Content/Assets/Orc/OrcRun.fbx
Content/Assets/Orc/OrcAttack.fbx
Content/Assets/Orc/orc_texture.png
```

`OrcTPose.fbx` содержит основной меш и эталонный скелет. Остальные FBX содержат animation channels того же скелета.

## 8. Manifest Orc

```json
{
  "name": "orc",
  "source": "../Content/Assets/Orc/OrcTPose.fbx",
  "output": "../Content/Models/orc.3dmodel",
  "clips": {
    "Idle": "../Content/Assets/Orc/OrcIdle.fbx",
    "Run": "../Content/Assets/Orc/OrcRun.fbx",
    "Attack": "../Content/Assets/Orc/OrcAttack.fbx"
  }
}
```

Разбор:

- `name` — имя для `CompiledModel.Load`.
- `source` — файл, из которого берутся mesh, skeleton и bind pose.
- `output` — готовый бинарник.
- `clips` — словарь `логическое имя → FBX`.
- `Idle`, `Run`, `Attack` становятся именами для `Draw` и `Play`.

## 9. Полный класс `Orc`

```csharp
using _3DLight;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

public enum OrcState
{
    Idle,
    Run,
    Attack
}

public sealed class Orc
{
    private CompiledModel model = null!;
    private float animationTime;
    private OrcState currentState = OrcState.Idle;

    public Vector3 Position { get; set; }
    public float RotationY { get; set; }
    public float Scale { get; set; } = 1f;

    public void LoadContent(
        GraphicsDevice graphicsDevice,
        ContentManager content)
    {
        Texture2D texture = content.Load<Texture2D>(
            "Assets/Orc/orc_texture");

        Effect toonEffect = content.Load<Effect>(
            "ToonShader");

        model = CompiledModel.Load(
            graphicsDevice,
            "orc",
            texture,
            toonEffect);
    }

    public void SetState(OrcState newState)
    {
        if (currentState == newState)
            return;

        currentState = newState;
        animationTime = 0f;
    }

    public void Update(GameTime gameTime)
    {
        animationTime +=
            (float)gameTime.ElapsedGameTime.TotalSeconds;
    }

    public void Draw(Matrix view, Matrix projection)
    {
        string clipName = currentState.ToString();
        bool loop = currentState != OrcState.Attack;

        Matrix world =
            Matrix.CreateScale(Scale) *
            Matrix.CreateRotationY(RotationY) *
            Matrix.CreateTranslation(Position);

        model.Draw(
            clipName,
            animationTime,
            loop,
            world,
            view,
            projection);
    }
}
```

## 10. Разбор `Orc` построчно

```csharp
public enum OrcState
```

Enum описывает разрешённые состояния. Имена `Idle`, `Run`, `Attack` совпадают с именами клипов в manifest.

```csharp
private float animationTime;
```

Хранит число секунд с момента начала текущего клипа.

```csharp
private OrcState currentState = OrcState.Idle;
```

Начальное состояние — ожидание.

`LoadContent` работает так же, как у `Crate`, но загружает `orc.3dmodel` и texture Orc.

```csharp
public void SetState(OrcState newState)
```

Метод переключает игровой state.

```csharp
if (currentState == newState)
    return;
```

Если состояние не изменилось, время нельзя сбрасывать. Иначе Idle начинался бы заново каждый кадр.

```csharp
currentState = newState;
animationTime = 0f;
```

При настоящем переключении новая анимация начинается с первого кадра.

```csharp
animationTime += elapsedSeconds;
```

Каждый Update продвигает анимацию вперёд.

```csharp
string clipName = currentState.ToString();
```

`OrcState.Run` превращается в строку `"Run"`, совпадающую с manifest.

```csharp
bool loop = currentState != OrcState.Attack;
```

Idle и Run повторяются, Attack проигрывается один раз.

```csharp
model.Draw(
    clipName,
    animationTime,
    loop,
    world,
    view,
    projection);
```

Анимированная перегрузка:

1. Находит клип по имени.
2. Вычисляет animation tick.
3. Интерполирует keyframes.
4. Строит матрицы костей.
5. Передаёт их в ToonShader.
6. Рисует меш.

## 11. Подключение Orc к игре

В `LightGame`:

```csharp
private readonly Orc orc = new();
```

В `LoadContent`:

```csharp
orc.LoadContent(GraphicsDevice, Content);
orc.Position = new Vector3(840f, 18f, 40f);
```

В `Update`:

```csharp
orc.Update(gameTime);
```

Переключение состояния:

```csharp
if (isPlayerNear)
    orc.SetState(OrcState.Attack);
else if (isMoving)
    orc.SetState(OrcState.Run);
else
    orc.SetState(OrcState.Idle);
```

В `Draw`:

```csharp
orc.Draw(camera.View, camera.Projection);
```

# Часть 3. Новый клип Rogue

## 12. Добавить `RogueRoll.fbx`

Файл:

```text
Content/Assets/Player/RogueRoll.fbx
```

В `player.clips`:

```json
"Roll": "../Content/Assets/Player/RogueRoll.fbx"
```

Левая часть `Roll` — runtime-имя. Правая — путь к FBX.

В `Player.cs`, в словарь `playerAnims`:

```csharp
{ "Roll", "RogueRoll.fbx" }
```

Это регистрирует имя в игровом аниматоре.

В логике:

```csharp
character.Play(
    "Roll",
    loop: false,
    deltaTime);
```

- `"Roll"` выбирает клип.
- `false` запрещает повтор.
- `deltaTime` продвигает время.

После изменения:

```powershell
dotnet build
```

ModelCompiler добавит Roll внутрь `player.3dmodel`.

# Часть 4. Частые ошибки

## 13. `Анимация отсутствует`

Имя различается между:

- `models.json`;
- словарём персонажа;
- `Play` или `Draw`.

Проверьте регистр и опечатки.

## 14. Модель белая

PNG не добавлен в MGCB либо путь `Content.Load<Texture2D>` неверен.

## 15. Модель не появляется

Проверьте:

- вызван ли `LoadContent`;
- вызывается ли `Draw`;
- находится ли объект перед камерой;
- появился ли `.3dmodel` после сборки;
- совпадает ли runtime-имя.

## 16. Скелет ломается

Animation FBX использует другой armature, другие имена костей или другой root transform. Экспортируйте mesh и clips из одного rig с одинаковыми export settings.

## 17. Неправильный размер

Лучше исправить scale при экспорте. Если используется `Matrix.CreateScale`, вместе с визуальным размером нужно согласовать collision radius, height, camera distance, movement speed и attack range.
