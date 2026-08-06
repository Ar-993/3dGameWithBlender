# 3DLight с нуля

Минимальное desktop-приложение на C# и MonoGame, которое:

- загружает уровень `FBX` из Blender;
- отображает материалы и текстуру;
- создаёт свободную камеру с управлением WASD и мышью;
- автоматически ставит камеру перед центром уровня.

Здесь нет персонажа, физики и редактора уровня. Blender используется как редактор, а MonoGame — как средство запуска и отображения готовой сцены.

## 1. Что установить

Понадобятся:

1. [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).
2. Blender.
3. Любой редактор C#, например Visual Studio, Rider или VS Code.

Проверка .NET:

```powershell
dotnet --version
```

## 2. Создание проекта

В PowerShell:

```powershell
mkdir C:\CsharpProjects\3DLight
cd C:\CsharpProjects\3DLight
dotnet new console --framework net8.0
```

Файл `3DLight.csproj` должен подключать desktop-версию MonoGame и сборщик контента:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <MonoGamePlatform>DesktopGL</MonoGamePlatform>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MonoGame.Framework.DesktopGL" Version="3.8.4.1" />
    <PackageReference Include="MonoGame.Content.Builder.Task" Version="3.8.4.1" />
  </ItemGroup>
</Project>
```

## 3. Инструмент MGCB

FBX нельзя напрямую передать видеокарте. Сначала MonoGame Content Builder превращает его в бинарный файл `XNB`.

Создайте `.config/dotnet-tools.json`:

```json
{
  "version": 1,
  "isRoot": true,
  "tools": {
    "dotnet-mgcb": {
      "version": "3.8.4.1",
      "commands": ["mgcb"],
      "rollForward": false
    },
    "dotnet-mgcb-editor": {
      "version": "3.8.4.1",
      "commands": ["mgcb-editor"],
      "rollForward": false
    }
  }
}
```

Восстановите инструмент:

```powershell
dotnet tool restore
```

### Открытие графического MGCB Editor

Дважды щёлкните `Open-MGCB-Editor.cmd` в корне проекта. Он откроет `Content/Content.mgcb` в отдельном графическом редакторе, как в стандартном шаблоне MonoGame.

То же самое из PowerShell:

```powershell
dotnet mgcb-editor "Content\Content.mgcb"
```

`dotnet mgcb` — консольный сборщик без окна. `dotnet mgcb-editor` — отдельный графический редактор списка контента.

## 4. Структура папок

Рабочая структура проекта:

```text
3DLight/
├── .config/
│   └── dotnet-tools.json
├── Assets/
│   ├── level.fbx
│   ├── palette.png
│   └── level.fbm/
│       └── palette.png
├── Content/
│   └── Content.mgcb
├── 3DLight.csproj
├── Program.cs
└── LightGame.cs
```

`Assets/level.fbx` — исходный уровень. Папка `level.fbm` содержит рабочую копию изображения, которую ожидает импортёр MonoGame.

## 5. Подготовка уровня в Blender

### Геометрия

Платформы можно делать обычными кубами:

1. `Shift+A → Mesh → Cube`.
2. Измените положение и размер.
3. Перед экспортом выделите объекты и примените трансформации: `Ctrl+A → All Transforms`.

Blender Origin `(0, 0, 0)` сохраняется в FBX, но камера C# является отдельным объектом. В этом проекте её старт вычисляется по границам всей геометрии.

### Материал и текстура

В рабочей области `Shading` материал должен выглядеть так:

```text
Image Texture (palette.png): Color
                ↓
Principled BSDF: Base Color
                ↓
Material Output: Surface
```

Не используйте `Diffuse BSDF` для FBX: `Principled BSDF` надёжнее распознаётся экспортёром.

### UV-развёртка

Текстура не знает, куда накладываться без UV:

1. Выберите Mesh.
2. Нажмите `Tab`, чтобы войти в Edit Mode.
3. Нажмите `A`, чтобы выделить всю геометрию.
4. Нажмите `U → Unwrap` или `U → Smart UV Project`.
5. В `UV Editing` расположите UV-полигоны на нужных цветах `palette.png`.
6. Проверьте результат в режиме `Material Preview`.

## 6. Экспорт FBX

Откройте `File → Export → FBX` и установите:

- `Object Types` — `Mesh`;
- `Apply Transform` — включено;
- `Apply Modifiers` — включено;
- `Path Mode` — `Copy`;
- `Embed Textures` — нажата кнопка справа от `Path Mode`;
- `Selected Objects` — включите, если выбран только готовый уровень.

Сохраните с заменой:

```text
C:\CsharpProjects\3DLight\Assets\level.fbx
```

Также скопируйте текстуру сюда:

```text
C:\CsharpProjects\3DLight\Assets\level.fbm\palette.png
```

Даже при `Embed Textures` MonoGame/Assimp может запросить sidecar-файл из папки `.fbm`, поэтому в проекте хранятся обе копии `palette.png`.

## 7. Настройка Content Pipeline

Создайте `Content/Content.mgcb`:

```text
/outputDir:bin/$(Platform)
/intermediateDir:obj/$(Platform)
/platform:DesktopGL
/profile:HiDef
/compress:False

# FBX-модель
#begin ../Assets/level.fbx
/importer:FbxImporter
/processor:ModelProcessor
/processorParam:DefaultEffect=BasicEffect
/processorParam:GenerateMipmaps=True
/processorParam:PremultiplyTextureAlpha=True
/processorParam:TextureFormat=Color
/build:../Assets/level.fbx;level.fbx

# Текстура, на которую ссылается собранная модель
#begin ../Assets/level.fbm/palette.png
/importer:TextureImporter
/processor:TextureProcessor
/processorParam:GenerateMipmaps=True
/processorParam:PremultiplyAlpha=True
/processorParam:TextureFormat=Color
/build:../Assets/level.fbm/palette.png;Assets/level.fbm/palette_0.png
```

Суффикс `_0` важен: именно имя `palette_0.xnb` записано импортёром в собранную модель.

## 8. Минимальный запуск MonoGame

`Program.cs`:

```csharp
using var game = new LightGame();
game.Run();
```

В конструкторе игры укажите папку собранного контента:

```csharp
public LightGame()
{
    graphics = new GraphicsDeviceManager(this);
    Content.RootDirectory = "Content";
    IsMouseVisible = false;
}
```

## 9. Загрузка FBX в C#

Content Pipeline создаёт `Model`, внутри которого находятся меши, материалы и ссылки на текстуры:

```csharp
private Model level = null!;
private Matrix[] boneTransforms = [];

protected override void LoadContent()
{
    level = Content.Load<Model>("level");

    boneTransforms = new Matrix[level.Bones.Count];
    level.CopyAbsoluteBoneTransformsTo(boneTransforms);
}
```

Расширение не указывается: `Content.Load<Model>("level")` загружает собранный `Content/level.xnb`, а не исходный FBX.

## 10. Отрисовка модели с текстурами

`ModelProcessor` создаёт отдельный `BasicEffect` для материалов FBX. Не заменяйте `meshEffect.Texture`, иначе потеряется импортированная текстура.

```csharp
var view = Matrix.CreateLookAt(
    cameraPosition,
    cameraPosition + Forward(),
    Vector3.Up);

var projection = Matrix.CreatePerspectiveFieldOfView(
    MathHelper.ToRadians(70),
    GraphicsDevice.Viewport.AspectRatio,
    0.05f,
    farPlane);

foreach (var mesh in level.Meshes)
{
    foreach (BasicEffect meshEffect in mesh.Effects)
    {
        meshEffect.World = boneTransforms[mesh.ParentBone.Index];
        meshEffect.View = view;
        meshEffect.Projection = projection;

        meshEffect.LightingEnabled = true;
        meshEffect.AmbientLightColor = new Vector3(0.35f);
        meshEffect.DirectionalLight0.Enabled = true;
        meshEffect.DirectionalLight0.Direction =
            Vector3.Normalize(new Vector3(-0.7f, -1f, -0.4f));
    }

    mesh.Draw();
}
```

## 11. Камера WASD и мышь

Направление камеры вычисляется из двух углов:

```csharp
private Vector3 Forward() => Vector3.Normalize(new Vector3(
    MathF.Sin(yaw) * MathF.Cos(pitch),
    MathF.Sin(pitch),
    MathF.Cos(yaw) * MathF.Cos(pitch)));
```

Поворот мышью:

```csharp
var center = new Point(
    Window.ClientBounds.Width / 2,
    Window.ClientBounds.Height / 2);

var mouse = Mouse.GetState();
yaw -= (mouse.X - center.X) * 0.004f;
pitch -= (mouse.Y - center.Y) * 0.004f;
pitch = MathHelper.Clamp(pitch, -1.54f, 1.54f);
Mouse.SetPosition(center.X, center.Y);
```

Движение с независимостью от частоты кадров:

```csharp
var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
var forward = Forward();
var flatForward = Vector3.Normalize(new Vector3(forward.X, 0, forward.Z));
var right = Vector3.Normalize(Vector3.Cross(flatForward, Vector3.Up));
var movement = Vector3.Zero;

if (keyboard.IsKeyDown(Keys.W)) movement += flatForward;
if (keyboard.IsKeyDown(Keys.S)) movement -= flatForward;
if (keyboard.IsKeyDown(Keys.D)) movement += right;
if (keyboard.IsKeyDown(Keys.A)) movement -= right;
if (keyboard.IsKeyDown(Keys.Space)) movement += Vector3.Up;
if (keyboard.IsKeyDown(Keys.LeftControl)) movement -= Vector3.Up;

if (movement != Vector3.Zero)
{
    movement.Normalize();
    var speed = keyboard.IsKeyDown(Keys.LeftShift)
        ? movementSpeed * 4f
        : movementSpeed;
    cameraPosition += movement * speed * dt;
}
```

## 12. Автоматическое центрирование камеры

Камеру нельзя просто поставить в `(0, 0, 0)`: она может оказаться внутри платформы. Проект объединяет границы всех мешей, ставит камеру перед ними и направляет её в центр:

```csharp
BoundingSphere? levelBounds = null;

foreach (var mesh in level.Meshes)
{
    var meshBounds = mesh.BoundingSphere.Transform(
        boneTransforms[mesh.ParentBone.Index]);

    levelBounds = levelBounds.HasValue
        ? BoundingSphere.CreateMerged(levelBounds.Value, meshBounds)
        : meshBounds;
}

var bounds = levelBounds ?? new BoundingSphere(Vector3.Zero, 10f);
var radius = MathF.Max(bounds.Radius, 1f);

movementSpeed = MathF.Max(radius * 0.35f, 10f);
cameraPosition = bounds.Center + new Vector3(0, radius * 0.25f, radius * 1.35f);

var direction = Vector3.Normalize(bounds.Center - cameraPosition);
yaw = MathF.Atan2(direction.X, direction.Z);
pitch = MathF.Asin(direction.Y);
```

## 13. Сборка и запуск

Из корня проекта:

```powershell
dotnet tool restore
dotnet build
dotnet run --no-build
```

После изменения и повторного экспорта FBX остановите игру и снова выполните:

```powershell
dotnet run
```

FBX компилируется во время сборки, поэтому простое копирование нового файла в уже запущенную игру его не обновит.

## 14. Управление

- `WASD` — горизонтальное движение;
- мышь — обзор;
- `Space` — вверх;
- `Ctrl` или `C` — вниз;
- `Shift` — ускорение в четыре раза;
- `Home` — снова показать весь уровень;
- `Esc` — освободить или захватить мышь.

Окно запускается в фиксированном оконном режиме `1280×720` и автоматически располагается по центру основного монитора. `app.manifest` включает Per-Monitor DPI Awareness, поэтому Windows не растягивает 1280×720 при масштабе экрана 125–200%.

## 15. Частые ошибки

### Модель отображается без текстуры

Проверьте:

1. `Image Texture` подключена к `Principled BSDF: Base Color`.
2. У меша есть UV-развёртка.
3. В Blender включён `Material Preview` и текстура видна до экспорта.
4. `palette.png` находится в `Assets/level.fbm/`.
5. Текстура добавлена отдельной записью в `Content.mgcb`.

### Ошибка `palette_0.xnb was not found`

MGCB собрал модель, но не собрал внешнюю текстурную ссылку. Проверьте эту строку:

```text
/build:../Assets/level.fbm/palette.png;Assets/level.fbm/palette_0.png
```

### Ошибка `dotnet-mgcb does not exist`

Нет `.config/dotnet-tools.json` или инструмент ещё не восстановлен:

```powershell
dotnet tool restore
```

### После экспорта ничего не изменилось

Убедитесь, что Blender перезаписал именно:

```text
C:\CsharpProjects\3DLight\Assets\level.fbx
```

Затем остановите приложение и запустите `dotnet run`, чтобы пересобрать XNB.

### Камера потерялась

Нажмите `Home`. Камера снова вычислит границы FBX и покажет весь уровень.

## 16. Где лежат готовые файлы

После Debug-сборки:

```text
bin/Debug/net8.0/3DLight.exe
bin/Debug/net8.0/Content/level.xnb
bin/Debug/net8.0/Content/Assets/level.fbm/palette_0.xnb
```

Исходники Blender остаются в `Assets`, а приложение загружает только собранные XNB-файлы из `bin`.

Старый `Assets/level.obj` проекту не нужен: его можно удалить. Текущая версия использует только `level.fbx`, `level.fbm/palette.png` и собранные из них XNB-файлы.
