# Добавление моделей и анимаций

Эта инструкция описывает полный путь от нового FBX до его отрисовки в игре.

## 1. Какой путь проходит новый ресурс

```text
FBX
 ↓ запись в Assets/models.json
ModelCompiler + Assimp
 ↓ dotnet build
Content/Models/name.3dmodel
 ↓ CompiledModel.Load
GPU buffers + ToonShader
```

FBX нужен только компилятору. Запущенная игра читает готовый `.3dmodel`.

# Статическая модель

Ниже добавляется ящик `Crate`.

## 2. Добавить исходные файлы

Создайте папку:

```text
Content/Assets/Props
```

Положите туда:

```text
Content/Assets/Props/Crate.fbx
Content/Assets/Props/crate_texture.png
```

FBX должен содержать применённые transforms, нормали и UV-развёртку.

## 3. Зарегистрировать модель

Откройте `Assets/models.json` и добавьте объект в массив `models`:

```json
{
  "name": "crate",
  "source": "../Content/Assets/Props/Crate.fbx",
  "output": "../Content/Models/crate.3dmodel"
}
```

Значение `name` станет runtime-именем. Оно должно быть уникальным.

## 4. Добавить текстуру в MGCB

Добавьте `crate_texture.png` через MGCB Editor или вручную в `Content/Content.mgcb`:

```text
#begin Assets/Props/crate_texture.png
/importer:TextureImporter
/processor:TextureProcessor
/processorParam:GenerateMipmaps=True
/processorParam:PremultiplyAlpha=True
/processorParam:TextureFormat=Color
/build:Assets/Props/crate_texture.png
```

Модели собираются собственным компилятором, но текстуры по-прежнему собираются MGCB.

## 5. Пересобрать

```powershell
dotnet build
```

В выводе должна появиться строка:

```text
Compiling crate...
```

После сборки проверьте файлы:

```text
Content/Models/crate.3dmodel
bin/Debug/net8.0/Content/Models/crate.3dmodel
```

## 6. Загрузить статическую модель

```csharp
using _3DLight;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

public sealed class Crate
{
    private CompiledModel model = null!;

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

    public void Draw(
        Vector3 position,
        Matrix view,
        Matrix projection)
    {
        Matrix world = Matrix.CreateTranslation(position);
        model.Draw(world, view, projection);
    }
}
```

Строка `"crate"` должна совпадать с `name` в `models.json`.

## 7. Подключить объект к `LightGame`

Поле:

```csharp
private readonly Crate crate = new();
```

В `LoadContent`:

```csharp
crate.LoadContent(GraphicsDevice, Content);
```

В `Draw`:

```csharp
crate.Draw(
    new Vector3(845f, 18f, 45f),
    camera.View,
    camera.Projection);
```

# Новый анимированный персонаж

Ниже добавляется Orc.

## 8. Подготовить FBX

```text
Content/Assets/Orc/OrcTPose.fbx
Content/Assets/Orc/OrcIdle.fbx
Content/Assets/Orc/OrcRun.fbx
Content/Assets/Orc/OrcAttack.fbx
Content/Assets/Orc/orc_texture.png
```

Требования:

- `OrcTPose.fbx` содержит основной меш и эталонный скелет.
- Все клипы используют тот же armature.
- Имена костей совпадают во всех файлах.
- Scale и axis export settings одинаковы.

## 9. Зарегистрировать персонажа

В `Assets/models.json`:

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

Меш берётся только из `source`. Из элементов `clips` берутся animation channels.

## 10. Добавить текстуру

Добавьте в MGCB:

```text
Assets/Orc/orc_texture.png
```

В коде текстура загружается без расширения:

```csharp
Texture2D texture = content.Load<Texture2D>(
    "Assets/Orc/orc_texture");
```

## 11. Загрузить персонажа

```csharp
private CompiledModel orcModel = null!;
private float animationTime;

public void LoadContent(
    GraphicsDevice graphicsDevice,
    ContentManager content)
{
    Texture2D texture = content.Load<Texture2D>(
        "Assets/Orc/orc_texture");

    Effect toonEffect = content.Load<Effect>(
        "ToonShader");

    orcModel = CompiledModel.Load(
        graphicsDevice,
        "orc",
        texture,
        toonEffect);
}
```

## 12. Обновлять время анимации

```csharp
public void Update(GameTime gameTime)
{
    animationTime +=
        (float)gameTime.ElapsedGameTime.TotalSeconds;
}
```

При переключении на другой клип `animationTime` нужно сбрасывать в `0f`.

## 13. Нарисовать клип

```csharp
public void Draw(
    Matrix view,
    Matrix projection)
{
    Matrix world =
        Matrix.CreateScale(1f) *
        Matrix.CreateRotationY(rotationY) *
        Matrix.CreateTranslation(position);

    orcModel.Draw(
        "Run",
        animationTime,
        loop: true,
        world,
        view,
        projection);
}
```

Одноразовая атака:

```csharp
orcModel.Draw(
    "Attack",
    animationTime,
    loop: false,
    world,
    view,
    projection);
```

# Новый клип существующего персонажа

## 14. Добавить клип Rogue

Допустим, добавляется перекат.

Добавьте файл:

```text
Content/Assets/Player/RogueRoll.fbx
```

В секцию `player.clips` файла `Assets/models.json`:

```json
"Roll": "../Content/Assets/Player/RogueRoll.fbx"
```

В словарь `playerAnims` файла `Player.cs`:

```csharp
{ "Roll", "RogueRoll.fbx" }
```

В логике игрока:

```csharp
character.Play(
    "Roll",
    loop: false,
    deltaTime);
```

После этого выполните `dotnet build`.

## 15. Добавить клип Skeleton

Добавьте:

```text
Content/Assets/Skeleton/SkeletonBlock.fbx
```

В `skeleton.clips`:

```json
"Block": "../Content/Assets/Skeleton/SkeletonBlock.fbx"
```

В словарь `monsterAnims` файла `Skeleton.cs`:

```csharp
{ "Block", "SkeletonBlock.fbx" }
```

В логике Skeleton:

```csharp
animation.Play(
    "Block",
    loop: false,
    deltaTime);
```

# Использование `CharacterAnimator`

## 16. Ограничение текущего выбора моделей

Текущий `CharacterAnimator` выбирает модель по папке:

```csharp
string assetName = directoryName == "Skeleton"
    ? "skeleton"
    : "player";
```

Поэтому для совершенно нового типа персонажа есть два варианта:

1. Загружать `CompiledModel` напрямую, как показано для Orc.
2. Улучшить API `CharacterAnimator.LoadContent` и передавать `assetName` отдельным аргументом.

Рекомендуемый будущий вариант:

```csharp
animator.LoadContent(
    graphicsDevice,
    assetName: "orc",
    animations,
    texture,
    toonEffect);
```

Так `CharacterAnimator` перестанет зависеть от имени папки.

# Диагностика

## 17. Модель не компилируется

Проверьте:

- путь `source` относительно `Assets/models.json`;
- запятые и кавычки JSON;
- существование FBX;
- что один меш использует не больше 72 костей;
- уникальность имён узлов.

## 18. Клип не проигрывается

Проверьте, что имя одинаково записано:

- в `models.json`;
- в словаре персонажа;
- в вызове `Play` или `Draw`.

Также убедитесь, что после добавления FBX выполнен `dotnet build`.

## 19. Модель белая

Текстура не добавлена в MGCB либо путь `Content.Load<Texture2D>` не совпадает с asset name.

## 20. Анимация ломает тело

Причина почти всегда в несовместимом скелете:

- другие имена костей;
- другая иерархия;
- другой root transform;
- клип экспортирован с другим armature.

Экспортируйте все клипы из одного и того же rig.

## 21. Модель имеет неправильный размер

Сначала исправляйте scale при экспорте. Временное исправление в игре:

```csharp
Matrix world =
    Matrix.CreateScale(modelScale) *
    Matrix.CreateTranslation(position);
```

Коллизии, скорость, камера и attack range должны соответствовать визуальному масштабу.
