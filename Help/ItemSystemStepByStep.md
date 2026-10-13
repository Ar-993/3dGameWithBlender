# Предметы Ars3D: пошаговое подключение и добавление новых вещей

Цель: предмет появляется в уровне, падает и вращается как одно твёрдое тело, подбирается по `E` в один невидимый слот и выбрасывается следующим нажатием `E`. Возле доступного предмета показывается короткая пиксельная подсказка. При удержании предмета подсказка исчезает.

В текущем проекте подключение уже выполнено. Шаги про C# и `.csproj` нужны для понимания устройства системы и для переноса в другой проект. Если указанная строка уже есть, второй раз её добавлять не нужно. Для нового предмета обычно меняются каталог и файлы модели, а не физические классы.

Каждый шаг имеет четыре части: **что делаем → куда вставляем → код → что должно получиться**. Команды выполняются в PowerShell из `C:\CsharpProjects\Ars3D`.

## Шаг 0. Разобраться, где библиотека, где игра, а где данные

**Действие.** Открой решение `3DLight.slnx` и сравни три части проекта:

```text
Libraries/Ars3D.Items/       самостоятельная библиотека: предметы, слот, физика
Items/                     адаптер уровня, каталог и отрисовка предметов в игре
Models/                    загрузка и нормализация модели для MonoGame
Content/                   готовые модели, текстуры и каталог предметов
Content/Player/player.json  дистанция подбора и скорость броска игрока
LightGame.cs               связывает библиотеку с игровым циклом
GameUi.cs                  показывает короткую подсказку
Ui/PsxPixelFont.cs          рисует буквы подсказки пикселями
Help/ItemSystemStepByStep.md эта инструкция
```

Библиотека использует `.NET 8` и `System.Numerics`. Она не открывает окно, не читает клавиатуру и не знает о `GraphicsDevice`, FBX, Gum или MonoGame. Игра передаёт ей положение игрока, направление взгляда, факт нажатия кнопки и данные о столкновениях с уровнем.

| Класс или файл | За что отвечает | Когда открывать |
|---|---|---|
| [ItemDefinition](../Libraries/Ars3D.Items/ItemDefinition.cs) | Общие характеристики типа: ID, название, размер, физика, начальное вращение | Чтобы понять, что описывает запись каталога |
| [ItemInstance](../Libraries/Ars3D.Items/ItemInstance.cs) | Один экземпляр: своё положение, вращение, скорость и ID | Чтобы разобраться в состоянии конкретной вещи |
| [SingleSlotInventory](../Libraries/Ars3D.Items/SingleSlotInventory.cs) | Хранит единственный подобранный экземпляр | Для поведения одного невидимого слота |
| [ItemInteractionController](../Libraries/Ars3D.Items/ItemInteractionController.cs) | Обновляет вещи, выбирает ближайшую, реагирует на новое нажатие | Для общей логики подбора и броска |
| [ItemInteractionSettings](../Libraries/Ars3D.Items/ItemInteractionSettings.cs) | Дистанция подбора и скорость броска игрока | Для настройки возможностей игрока |
| [ItemCarrySettings](../Libraries/Ars3D.Items/ItemCarrySettings.cs) | Положение в руке, подъём и вращение при броске | Для настройки конкретного типа предмета |
| [RigidBody](../Libraries/Ars3D.Items/Physics/RigidBody.cs) | Публичная точка входа в физику одного твёрдого тела | Чтобы читать или менять положение тела через API |
| [RigidBodySettings](../Libraries/Ars3D.Items/Physics/RigidBodySettings.cs) | Гравитация, трение, отскок и параметры покоя | Для общей настройки физики |
| [LevelItemWorld](../Items/LevelItemWorld.cs) | Переводит запросы библиотеки в столкновения с текущим уровнем | При переносе в другой движок или другой уровень |
| [ItemModelRenderer](../Items/ItemModelRenderer.cs) | Рисует модель в положении, которое рассчитала библиотека | Для отрисовки в MonoGame |
| [ItemMath](../Items/ItemMath.cs) | Переводит векторы и матрицы между MonoGame и `System.Numerics` | Если подключается ещё один графический движок |
| [ItemCatalog](../Items/ItemCatalog.cs) | Читает JSON, создаёт экземпляры, хранит общие GPU-модели | Для загрузки новых типов и управления ресурсами |
| [ItemSpawner](../ItemSpawner.cs) | Находит маркер уровня и создаёт предмет из каталога | Для появления вещей в уровне |
| [GameUi](../GameUi.cs) | Показывает пиксельную подсказку только для доступного подбора | Для внешнего вида подсказки |

Внутренняя физика тоже разделена. [RoundedBoxShape](../Libraries/Ars3D.Items/Physics/RoundedBoxShape.cs) описывает форму, [BodyMotion](../Libraries/Ars3D.Items/Physics/BodyMotion.cs) — движение и инерцию, [BodyCollision](../Libraries/Ars3D.Items/Physics/BodyCollision.cs) — поиск контактов, [ContactSolver](../Libraries/Ars3D.Items/Physics/ContactSolver.cs) — реакцию на контакт, [BodyStepper](../Libraries/Ars3D.Items/Physics/BodyStepper.cs) — маленькие шаги времени, [RestingState](../Libraries/Ars3D.Items/Physics/RestingState.cs) — успокоение и пробуждение. Для добавления второй модели эти файлы не меняются.

`ItemDefinition` — описание типа, например «Санта высотой 0.5 метра». `ItemInstance` — одна конкретная игрушка этого типа. У десяти Сант одна общая дефиниция, но десять разных состояний и `InstanceId`. Поэтому не нужны классы `SantaItem`, `SecondSantaItem` и `ThirdSantaItem`.

**Результат.** Новый проект может использовать те же предметы и физику, написав свой адаптер уровня и свою отрисовку. Менять библиотеку ради другой текстуры или другого русского названия не требуется.

## Шаг 1. Подключить проект библиотеки один раз

**Действие.** Открой `3DLight.csproj`. В существующем `ItemGroup`, где находится ссылка на `Shared\ModelFormat`, должна быть ещё одна ссылка:

```xml
<ProjectReference Include="Libraries\Ars3D.Items\Ars3D.Items.csproj" />
```

Не создавай второй `ProjectReference` на тот же файл. `ProjectReference` означает: сначала собрать библиотеку, затем дать игре использовать её публичные классы.

**Куда вставлять.** В существующий `ItemGroup` с `Compile Remove` добавь:

```xml
<Compile Remove="Libraries\**\*.cs" />
```

Эта строка нужна, потому что SDK автоматически собирает C# из подпапок. Исходники библиотеки должны собираться её собственным `.csproj`, а не второй раз внутри игры.

**Действие.** Открой `3DLight.slnx`. Внутри корневого `<Solution>` добавь следующий полный блок, если его ещё нет:

```xml
<Folder Name="/Libraries/">
  <Project Path="Libraries/Ars3D.Items/Ars3D.Items.csproj" />
</Folder>
```

**Проверка.** Для отдельной сборки библиотеки команда не запускает графическое окно:

```powershell
dotnet build Libraries\Ars3D.Items\Ars3D.Items.csproj
```

**Результат.** В решении виден самостоятельный проект `Ars3D.Items`. Игра ссылается на него; библиотека не ссылается на игру. В другом проекте достаточно подключить этот `.csproj` или собранную DLL и реализовать адаптер окружения.

## Шаг 2. Подготовить модель предмета

**Действие.** Для первого примера используем Деда Мороза из уже распакованного Christmas-ассета. Для второго — уже имеющийся Colt. Новые файлы скачивать не нужно.

```text
Assets/Christmas/Models/SantaClaus.fbx
Assets/Christmas/Textures/SantaClaus.png
Assets/Christmas/Textures/Christmas_hat.png
Content/Assets/Christmas/SantaClaus.png
Content/Assets/Christmas/Christmas_hat.png
Content/Assets/Gun/Pistols_Colt_01_A.fbx
Content/Assets/Gun/T_PRP_Pistol_Colt_A_BaseColor.png
```

FBX нужен во время сборки. Игра загружает готовый файл `.3dmodel`, а материал берёт из папки с изображениями.

**Куда вставлять.** Открой `Assets/models.json`. В массиве `models` уже должны присутствовать следующие два объекта. Это отдельные полные записи; проверяй их наличие по `name`, а не добавляй повторно:

```json
{
  "name": "christmas_santa",
  "source": "Christmas/Models/SantaClaus.fbx",
  "output": "../Content/Models/christmas_santa.3dmodel"
}
```

```json
{
  "name": "gun",
  "source": "../Content/Assets/Gun/Pistols_Colt_01_A.fbx",
  "output": "../Content/Models/gun.3dmodel"
}
```

Пути `source` и `output` считаются относительно папки `Assets`, в которой лежит этот JSON. Между объектами массива нужна запятая. После последнего объекта запятой быть не должно. В JSON нельзя писать комментарии.

**Проверка.** Перекомпилируй модели:

```powershell
dotnet run --project Tools\ModelCompiler\ModelCompiler.csproj -- Assets\models.json
```

**Результат.** Появляются или обновляются `Content/Models/christmas_santa.3dmodel` и `Content/Models/gun.3dmodel`. Обычная сборка игры также запускает этот компилятор через цель `CompileModels`.

У Санты в исходном FBX есть кости. Предмет использует исходную неподвижную позу модели: голова, руки и ноги движутся вместе. Вращение предмета не включает персонажную анимацию и не превращает его в гибкую куклу.

## Шаг 3. Обеспечить загрузку настоящих текстур

**Действие.** Убедись, что `SantaClaus.png` и `Christmas_hat.png` находятся в `Content/Assets/Christmas`. У Санты два материала: тело и шапка. Скопируй обе текстуры из `Assets/Christmas/Textures`, сохранив исходные имена: загрузчик ищет изображения по материалам модели.

**Куда вставлять.** В `ItemGroup` с файлами для копирования в `3DLight.csproj` должны быть обе строки:

```xml
<None Update="Content\Assets\Christmas\*.png" CopyToOutputDirectory="PreserveNewest" />
<None Update="Content\Assets\Gun\*.png" CopyToOutputDirectory="PreserveNewest" />
```

Если строки уже есть, ничего добавлять не надо. `PreserveNewest` копирует файл, если исходник новее версии в выходной папке.

**Проверка.** После сборки найди файлы:

```text
bin/Debug/net8.0/Content/Assets/Christmas/SantaClaus.png
bin/Debug/net8.0/Content/Assets/Christmas/Christmas_hat.png
bin/Debug/net8.0/Content/Assets/Gun/T_PRP_Pistol_Colt_A_BaseColor.png
```

**Результат.** Загрузчик модели читает изображения из папки `Content/Assets` рядом с запускаемой программой. Для этих PNG-материалов отдельная запись в MGCB не нужна: их читает `ModelAssetLoader`. MGCB по-прежнему собирает шейдеры и другие Content-ресурсы игры.

Если модель видна, но стала одноцветной, сначала проверь имя PNG, папку текстур и наличие PNG в выходной папке. В консоли загрузчик сообщает, когда заменяет отсутствующий материал резервной текстурой.

## Шаг 4. Указать место появления через Blender Empty

**Действие.** Открой исходный уровень `Assets/GasStation/GasStationLevel.blend` в Blender. Если переносишь систему в проект без `.blend`, импортируй его FBX. Для Санты нужен пустой объект с именем `ItemSpawn`; в текущем уровне он уже есть, повторно создавать его не нужно:

1. Нажми `Shift+A` → `Empty` → `Plain Axes`.
2. В `Object Properties` или Outliner задай имя `ItemSpawn` без пробелов.
3. Поставь Empty над свободным участком пола. Маркер может находиться высоко: предмет после появления упадёт под действием гравитации.
4. При экспорте FBX включи типы объектов `Mesh` и `Empty`. При включённом `Selected Objects` выдели также Empty.
5. Сохрани FBX по тому же пути `Assets/GasStation/GasStationLevel.fbx`.
6. Сохрани выбранные настройки осей экспортера для следующих экспортов. В игре вертикальная ось — `Y`; итоговое положение проверяется после преобразования FBX, а не по одной цифре из Blender.

**Код для проверки маркера.** Этот полный фрагмент можно временно вставить в `LightGame.LoadContent()` сразу после `level.LoadContent(Content, ActiveLevelModel)`:

```csharp
if (level.TryGetMarkerPosition("ItemSpawn", out Vector3 itemSpawn))
    Console.WriteLine($"ItemSpawn в игре: {itemSpawn}");
else
    Console.WriteLine("ItemSpawn отсутствует в экспортированном уровне.");
```

**Проверка.** Перекомпилируй модели командой из шага 2. В существующем GasStation маркер Санты находится примерно на высоте `Y = 5` в координатах игры.

**Результат.** Положение предмета берётся из узла уровня с именем `ItemSpawn`. Empty не рисуется и не становится препятствием. Его имя служит ключом поиска.

Для разных экземпляров можно создать `ItemSpawn_02`, `ItemSpawn_03` и другие имена. Эти маркеры добавляются в записи появления в каталоге. Один и тот же тип предмета может использовать много разных маркеров.

## Шаг 5. Описать два типа предметов в каталоге

**Действие → место.** Открой [Content/Items/items.json](../Content/Items/items.json). Ниже полный текущий файл: две дефиниции и одно появление Санты. При переносе в старую копию проекта создай этот файл; в текущей копии сравни содержимое, не дописывай второй массив `definitions`.

```json
{
  "definitions": [
    {
      "id": "santa",
      "displayName": "Дед Мороз",
      "model": "christmas_santa",
      "textureFolder": "Christmas",
      "height": 0.5,
      "radius": 0.12,
      "spawnSpin": [0.9, 0.35, 0.55],
      "heldYawDegrees": 0,
      "interaction": {
        "heldOffset": [0.38, -0.55, -0.9],
        "throwLift": 1.7,
        "throwSpin": [7.0, 2.0, 0.75]
      },
      "physics": { "gravity": 18.0, "friction": 0.65 }
    },
    {
      "id": "colt",
      "displayName": "Пистолет",
      "model": "gun",
      "textureFolder": "Gun",
      "height": 0.22,
      "radius": 0.035,
      "spawnSpin": [0.2, 0.3, 0.1],
      "heldYawDegrees": 0,
      "interaction": {
        "heldOffset": [0.38, -0.55, -0.9],
        "throwLift": 1.7,
        "throwSpin": [7.0, 2.0, 0.75]
      }
    }
  ],
  "spawns": [
    { "itemId": "santa", "marker": "ItemSpawn", "fallback": [8.0, 5.0, 15.0] }
  ]
}
```

| Поле | Что означает |
|---|---|
| `id` | Уникальный ключ типа; именно его указывают в `itemId` и `catalog.Create` |
| `displayName` | Имя для интерфейса; не имя FBX и не имя Blender Empty |
| `model` | Имя `.3dmodel` без расширения: `gun` загружает `gun.3dmodel` |
| `textureFolder` | Папка внутри выходной `Content/Assets` |
| `height` | Высота модели в метрах после нормализации |
| `radius` | Базовый радиус; фактические размеры тела каталог вычисляет по нормализованной модели |
| `spawnSpin` | Начальная угловая скорость `[X, Y, Z]` в радианах в секунду |
| `heldYawDegrees` | Поворот в руке относительно камеры, в градусах |
| `physics` | Необязательные настройки физики; отсутствующие поля получают стандартные значения |
| `marker` / `fallback` | Blender Empty / запасное положение `[X, Y, Z]`, если маркера нет |

**Куда вставлять.** В `ItemGroup` с `None Update` в `3DLight.csproj` должна быть строка:

```xml
<None Update="Content\Items\*.json" CopyToOutputDirectory="PreserveNewest" />
<None Update="Content\Player\*.json" CopyToOutputDirectory="PreserveNewest" />
```

**Результат / проверка.** Каталог знает типы `santa` и `colt`; появится только `santa`, потому что только он записан в `spawns`. После сборки проверь `bin/Debug/net8.0/Content/Items/items.json`. Изменяй исходный JSON в `Content/Items`, а не скопированную версию в `bin`.

У каждого типа должен быть уникальный `id`. Векторы содержат ровно три числа. Неверное имя свойства, неизвестный `itemId` или дубликат ID вызывают ошибку загрузки, поэтому не переименовывай ключи JSON произвольно. Название, модель и маркер — разные поля с разными задачами.

## Шаг 6. Загрузить каталог и создать экземпляры

**Действие → место.** В начале `LightGame.cs` добавь импорт, если его ещё нет:

```csharp
using _3DLight.Items;
```

В полях класса `LightGame` замени старое поле `private readonly ItemInteraction itemInteraction = new();` следующими двумя полями. Отдельное поле `santaModel` больше не нужно: моделью управляет каталог.

```csharp
private ItemInteraction itemInteraction = null!;
private ItemCatalog? itemCatalog;
```

**Куда вставлять.** В `LoadContent()`, после загрузки уровня и появления игрока, найди блок от объявления `Texture2D fallbackTexture` до создания предметов. В старой копии он заканчивается добавлением результата `ItemSpawner.CreateSanta` в `itemInteraction`; в текущей — вызовом `ItemSpawner.SpawnConfigured`. Замени весь этот участок следующим блоком. Если он уже совпадает, ничего не добавляй:

```csharp
Texture2D fallbackTexture = Content.Load<Texture2D>("Assets/Gun/T_PRP_Pistol_Colt_A_BaseColor");
Effect toonEffect = Content.Load<Effect>("ToonShader");
gunModel = ModelAssetLoader.Load(GraphicsDevice, "gun", fallbackTexture, toonEffect, "Gun");
bulletModel = ModelAssetLoader.Load(GraphicsDevice, "bullet", fallbackTexture, toonEffect, "Gun");
itemCatalog = ItemCatalog.Load(GraphicsDevice, fallbackTexture, toonEffect);
itemInteraction = new ItemInteraction(player.Definition.Interaction);
ItemSpawner.SpawnConfigured(level, itemCatalog, itemInteraction, player.Position);
```

Первая строка даёт резервную текстуру; вторая — исходный шейдер. `ItemCatalog.Load` читает каталог предметов. `ItemInteraction` получает настройки игрока из `player.Definition.Interaction`. `SpawnConfigured` проходит по `spawns`, находит маркеры и регистрирует экземпляры для физики и подбора.

**Определение игрока → место → код.** [PlayerDefinition](../PlayerDefinition.cs) сам читает `Content/Player/player.json` в `Load()`. В `Player.cs` добавляется `public PlayerDefinition Definition { get; }`; конструктор получает `PlayerDefinition definition` четвёртым аргументом и сохраняет его через `Definition = definition`. В конце `LightGame.CreatePlayer()` замени создание игрока на:

```csharp
return new Player(physics, stats, attack, PlayerDefinition.Load());
```

Отдельный загрузчик не требуется: готовое определение передаётся в игрока один раз, затем его `Interaction` передаётся контроллеру предметов.

**Результат / проверка.** Для каждой записи `spawns` создаётся своё состояние. Модель загружается при первом запросе нужного типа и используется повторно. Даже сто экземпляров одного типа не требуют ста загрузок одного FBX или ста новых классов.

## Шаг 7. Обновлять физику и обрабатывать E ровно один раз за кадр

**Действие → место.** В `LightGame.Update(GameTime gameTime)` камера должна сначала получить новое положение игрока. В текущем коде это уже делает `camera.UpdateMatrices`. Переменная `deltaTime` уже вычислена из `ElapsedGameTime.TotalSeconds`; не объявляй её второй раз.

Перед `keyboard = UpdateDebugKeys(keyboard)` вставляется или проверяется следующий блок:

```csharp
if (!mouseCaptured || capturedThisFrame || justActivated)
    itemInteraction.SuppressInput(keyboard.IsKeyDown(Keys.E));
itemInteraction.Update(level, deltaTime, player.Position,
    camera.Position, camera.Forward, keyboard.IsKeyDown(Keys.E),
    canInteract: !player.IsDead);
```

`Update` одновременно обновляет тела и обрабатывает действие. Не добавляй отдельный цикл `item.UpdatePhysics` в том же кадре: это ускорит падение вдвое. Передавать нужно секунды, а не миллисекунды; при 60 FPS один шаг составляет примерно `0.0167` секунды.

**Пауза → место → код.** В ветке `if (!IsActive)`, перед её `return`, и в ветке `if (pause.IsPaused)`, перед её `return`, должна быть одна и та же строка:

```csharp
itemInteraction.SuppressInput(keyboard.IsKeyDown(Keys.E));
```

На паузе вызывается синхронизация кнопки, а обычный `Update` предметов пропускается благодаря существующему `return`. Иначе `E`, зажатая в меню, считалась бы новым нажатием при возвращении. `SuppressInput` не двигает предметы.

**Результат / проверка.** Подойди к Санте и нажми `E`: предмет займёт слот. Продолжай держать `E`: он остаётся в руке. Отпусти и нажми ещё раз: предмет полетит вперёд с вращением. Когда персонаж не может взаимодействовать, контроллер выпускает удерживаемую вещь без броска.

## Шаг 8. Нарисовать вещь и оставить только подсказку подбора

**Действие → место.** В обычной ветке `LightGame.Draw`, где рисуется уровень и скелет, должен быть один вызов:

```csharp
itemInteraction.Draw(camera.View, camera.Projection, lighting);
```

Контроллер рисует свободные экземпляры в мировых позах, затем удерживаемый предмет в позе камеры. Он берёт одну и ту же позу для удержания и последующего выпуска, поэтому начало броска соответствует видимой вещи.

**Куда вставлять.** В конце `Draw`, сразу после вызова `gameUi.SetHudText`, заменяется одиночный `gameUi.Draw()` следующим полным блоком:

```csharp
WorldItem? interactionItem = itemInteraction.HeldItem ?? itemInteraction.NearbyItem;
gameUi.SetInteractionText(interactionItem?.Name,
    itemInteraction.HeldItem is not null,
    IsActive && mouseCaptured && !pause.IsPaused && !player.IsDead);
gameUi.Draw();
```

**Результат / проверка.** Рядом с доступным Санта-предметом появляется только `[E] ВЗЯТЬ ДЕДА МОРОЗА`. После подбора строка исчезает. Подсказки броска, карточки инвентаря и дополнительных инструкций управления нет. Пауза, потеря фокуса и смерть скрывают слой взаимодействия.

Шрифт находится в [Ui/PsxPixelFont.cs](../Ui/PsxPixelFont.cs): буква занимает `5×7` пикселей, масштаб `2` даёт высоту `14` пикселей. [GameUi.cs](../GameUi.cs) рисует тень со смещением на два пикселя. Системный шрифт и скачивание шрифта не нужны.

## Шаг 9. Освободить общие модели при завершении

**Действие → место.** В `LightGame.OnExiting`, после освобождения `bulletModel`, должна быть строка:

```csharp
itemCatalog?.Dispose();
```

Если переносишь код со старой копии, удали строку `santaModel?.Dispose()` вместе с самим старым полем. В текущей копии освобождение уже переключено на каталог.

**Проверка владельцев.** `ItemCatalog` владеет загруженными для предметов моделями и их собственными GPU-ресурсами. `WorldItem` и `ItemModelRenderer` используют эти модели совместно. Не вызывай `Dispose()` общей модели из отдельного экземпляра: остальные экземпляры продолжат обращаться к ней.

`fallbackTexture` и исходный `toonEffect` получены из `Content`; их жизнью управляет `ContentManager`. `GameUi.Dispose()` освобождает свой `SpriteBatch` и белую пиксельную текстуру. Состояния библиотеки не владеют GPU-ресурсами.

**Результат.** Закрытие игры освобождает каждую модель каталога один раз. Число экземпляров не увеличивает число владельцев одной и той же модели.

## Шаг 10. Добавить много экземпляров без новых классов

**Действие → место.** В `Content/Items/items.json` замени только значение поля `spawns` следующим полным массивом. Массив `definitions` из шага 5 оставь:

```json
[
  { "itemId": "santa", "marker": "ItemSpawn", "fallback": [8.0, 5.0, 15.0] },
  { "itemId": "santa", "marker": "ItemSpawn_02", "fallback": [9.0, 3.0, 15.0] },
  { "itemId": "colt", "marker": "ColtSpawn", "fallback": [10.0, 2.0, 15.0] }
]
```

**Результат / проверка.** После сборки и нового запуска появятся две независимые Санты и один Colt. Если дополнительные Empty ещё не созданы, используются указанные `fallback`; если созданы, выигрывают координаты Empty. Слот всё ещё один: наличие нескольких вещей в мире не создаёт несколько слотов.

**Появление из C#.** Если предмет появляется по игровому событию, следующую полную вставку можно использовать в `LoadContent()` после `SpawnConfigured`, вместо добавления этой же вещи в JSON:

```csharp
WorldItem extraSanta = itemCatalog.Create("santa", player.Position + new Vector3(0.8f, 2f, -1.2f));
itemInteraction.Add(extraSanta);
```

В обработчике события выполняются те же две операции: `Create` создаёт экземпляр, `Add` включает его в контроллер. Без `Add` контроллер не обновляет, не рисует и не подбирает новый экземпляр. Не выполняй такую вставку каждый кадр, если тебе не нужен непрерывный поток новых вещей.

## Шаг 11. Настроить размер, удержание, бросок и физику

**Действие → место.** Основные правки делаются в `Content/Items/items.json`. `height` меняет размер модели целиком. `spawnSpin` меняет начальное кувыркание. `heldYawDegrees: 0` показывает лицо Санты к игроку; `180` развернёт спиной. Глобального `WorldItem.HeldYaw` в этой версии нет.

**Настройки игрока → место → код.** Файл [Content/Player/player.json](../Content/Player/player.json) содержит:

```json
{
  "interaction": {
    "pickupDistance": 2.0,
    "throwSpeed": 7.5
  }
}
```

`heldOffset`, `throwLift` и `throwSpin` находятся в блоке `interaction` внутри каждого объекта `definitions` файла `items.json` (см. полный пример в шаге 5). `heldOffset` задан в координатах камеры: `X` — вправо, `Y` — вверх, отрицательное `Z` — вперёд. Стандартное `[0.38, -0.55, -0.9]` располагает вещь справа и ниже взгляда. `throwLift` добавляет скорость вверх. `throwSpin` задаёт скорости вращения вокруг направлений камеры: вправо, вверх и вперёд. Эти параметры хранятся в `ItemDefinition.Interaction`; при броске они объединяются со скоростью игрока. `pickupDistance` и `throwSpeed` меняются в `player.json`.

**Код.** Для более слабого отскока замени только значение `physics` у нужной дефиниции следующим полным объектом:

```json
{
  "gravity": 18.0,
  "friction": 0.65,
  "restitution": 0.05,
  "bounceSpeed": 1.2,
  "sleepDelay": 0.6
}
```

**Что меняется.** `gravity` — положительная величина ускорения вниз. `friction` уменьшает скольжение. `restitution` задаёт долю отскока. `bounceSpeed` не даёт слабым контактам постоянно подбрасывать вещь. `sleepDelay` определяет, сколько тело должно быть спокойным перед переходом в покой. Все параметры и проверки допустимых значений находятся в [RigidBodySettings.cs](../Libraries/Ars3D.Items/Physics/RigidBodySettings.cs).

**Результат / проверка.** Пересобери и перезапусти игру. Изменяется целое твёрдое тело, а не положение отдельных костей. Покоящийся предмет сохраняет боковой наклон; система не выпрямляет его автоматически. Коллайдер приближает форму набором сфер, неподвижных относительно одного жёсткого тела. Он не повторяет каждую вершину модели.

Для расследования положения используй `Center` и `Bounds.Min.Y`. `Position` — повёрнутый нижний pivot модели: после падения на бок он не обязан совпадать с уровнем пола. `Orientation` — поворот целого тела, `Velocity` — линейная скорость, `AngularVelocity` — скорость вращения.

## Шаг 12. Проверить подключение и увидеть результат

**Действие → код.** Собери игру и запусти её из корня проекта:

```powershell
dotnet build 3DLight.csproj
dotnet run --project 3DLight.csproj --no-build
```

**Проверка руками.** Дождись падения Санты, подойди к нему, посмотри на единственную строку подбора, нажми и отпусти `E`, затем нажми снова. Проверь падение на бок, лицо при удержании и вращение в полёте. Нажатие `E` за стеной не должно подбирать вещь сквозь препятствие.

**Автоматическая проверка.** Существующий набор проверяет библиотеку, взаимодействие и геометрию уровня:

```powershell
dotnet run --project Libraries\Ars3D.Items.Tests\Ars3D.Items.Tests.csproj
dotnet build Tools\CollisionTests\CollisionTests.csproj
dotnet Tools\CollisionTests\bin\Debug\net8.0\CollisionTests.dll Content\Models\gas_station.3dmodel
dotnet Tools\CollisionTests\bin\Debug\net8.0\CollisionTests.dll --graphics Content\Models\gas_station.3dmodel
```

**Результат.** `--graphics` создаёт конечные графические проверки и изображения в `artifacts/first-person`: `falling-item.png`, `fallen-side.png`, `nearby-pickup.png`, `held-item.png`, `thrown-item.png`. Процесс проверки выходит сам. Список `FAIL` или ненулевой код завершения означает, что конкретную проверку надо исправить; подробности запуска находятся в [Tools/CollisionTests/README.md](../Tools/CollisionTests/README.md).

## Шаг 13. Перенести библиотеку в другой проект

**Действие.** Скопируй папку `Libraries/Ars3D.Items` в новый проект и добавь ссылку из шага 1 с подходящим относительным путём. В библиотеке все координаты — `System.Numerics.Vector3`, повороты — `System.Numerics.Quaternion`.

Для нового движка реализуй интерфейс [IItemInteractionWorld](../Libraries/Ars3D.Items/IItemInteractionWorld.cs). Он наследует [ICollisionWorld](../Libraries/Ars3D.Items/Physics/Collision/ICollisionWorld.cs): два метода дают поиск контактов и непрерывное столкновение сферы, ещё два — проверку видимости и безопасное размещение выпускаемого предмета. Готовый пример адаптера для Ars3D — [LevelItemWorld.cs](../Items/LevelItemWorld.cs).

**Код данных.** Этот полный C#-фрагмент работает внутри метода с подключённой ссылкой на библиотеку; он создаёт два самостоятельных экземпляра без MonoGame:

```csharp
var definition = new Ars3D.Items.ItemDefinition(
    id: "toy",
    displayName: "Игрушка",
    radius: 0.12f,
    height: 0.5f,
    spawnAngularVelocity: new System.Numerics.Vector3(0.9f, 0.35f, 0.55f));
var first = new Ars3D.Items.ItemInstance(definition, new System.Numerics.Vector3(0f, 3f, 0f));
var second = new Ars3D.Items.ItemInstance(definition, new System.Numerics.Vector3(1f, 4f, 0f));
var controller = new Ars3D.Items.ItemInteractionController();
controller.Add(first);
controller.Add(second);
```

**Что ещё подключить.** Игровой цикл передаёт своему `controller.Update` реализацию интерфейса мира, время и состояние выбранной кнопки. Рендерер рисует свободные вещи по `WorldPose.ToMatrix()`; для удерживаемой вещи использует `GetHeldPose`, предварительно проверив `HeldItem is not null`. Во время меню вызывается `SyncInput`. Окно, материалы, звуки и текст подсказки создаёт новый проект.

**Код обновления в другом проекте.** Следующий полный метод добавляется внутрь класса игрового цикла. Его аргумент `world` — твоя реализация интерфейса, а `actionDown` — состояние выбранной кнопки. Во время меню метод не вызывается; вместо него вызывается `controller.SyncInput(actionDown)`.

```csharp
private static void UpdateItems(
    Ars3D.Items.ItemInteractionController controller,
    Ars3D.Items.IItemInteractionWorld world,
    float seconds,
    System.Numerics.Vector3 playerFeet,
    System.Numerics.Vector3 eye,
    System.Numerics.Vector3 forward,
    bool actionDown)
{
    controller.Update(world, seconds, playerFeet, eye, forward, actionDown);
}
```

**Результат.** Физика, состояние экземпляров и один слот работают в другой программе через публичные классы. `ItemCatalog` и `WorldItem` остаются адаптерами конкретной MonoGame-игры; их можно перенести отдельно, если следующий проект тоже использует тот же загрузчик моделей.

## Частые вопросы

**Добавил дефиницию, но вещи нет.** Добавь её `itemId` в `spawns` либо вызови `Create` и `Add`. Дефиниция сама по себе не является экземпляром.

**Изменил JSON, а игра осталась прежней.** Каталог читается при загрузке. Пересобери проект, чтобы обновить файл в `bin`, и начни новый запуск. Уже работающий процесс не перечитывает этот JSON автоматически.

**Почему я не могу подобрать вторую вещь?** Единственный слот занят. Новое нажатие `E` сначала выбрасывает удерживаемый предмет; затем можно подобрать следующий.

**Почему пропала подсказка?** Она появляется только для доступного ближайшего подбора и пустого слота. Проверь расстояние, препятствия, фокус окна, паузу и состояние персонажа. Удержание не имеет собственной подсказки.

**Модель не того размера или проваливается.** Проверь `height`, исходную позу FBX и нормализацию в [ItemModelTransform.cs](../Models/ItemModelTransform.cs). Для высоты контакта смотри `Bounds.Min.Y`, а не `Position.Y` повёрнутой модели.

**Нужно ли создавать отдельный C#-класс для меча, кружки или новой Санты?** Для физического предмета с тем же поведением достаточно новой дефиниции и новой модели. Собственные игровые эффекты можно связывать с `Definition.Id` или `InstanceId` в игре.

**Предметы сталкиваются друг с другом?** Сейчас библиотека спрашивает адаптер о геометрии уровня. Контакты предмет–предмет и инвентарь с несколькими слотами в этот пример не добавлены.

**Где читать полный исходник вместо пересказа формул?** Начни с [ItemInteractionController](../Libraries/Ars3D.Items/ItemInteractionController.cs), затем [ItemInstance](../Libraries/Ars3D.Items/ItemInstance.cs) и [RigidBody](../Libraries/Ars3D.Items/Physics/RigidBody.cs). Для моделей — [ItemCatalog](../Items/ItemCatalog.cs), для входа в игру — [LightGame.cs](../LightGame.cs). Таблица в шаге 0 показывает остальные небольшие классы.
