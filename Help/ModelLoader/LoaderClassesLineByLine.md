# Классы загрузчика: подробный разбор

Этот документ объясняет все основные классы и структуры загрузчика, кто их создаёт и как данные переходят между ними.

# 1. Карта классов

```text
ModelManifest
 └── ModelEntry
      ↓ ModelCompiler
ModelData
 ├── NodeData
 ├── MeshData
 │    ├── VertexData
 │    └── BoneData
 └── ClipData
      └── ChannelData
           ├── VectorKey
           └── QuaternionKey
      ↓ ModelDataIo.Write
*.3dmodel
      ↓ ModelDataIo.Read
CompiledModel
 ├── RuntimeMesh
 │    ├── RuntimeVertex
 │    ├── VertexBuffer
 │    ├── IndexBuffer
 │    └── Effect
 └── CharacterAnimator
```

# Часть 1. Классы manifest

## 2. `ModelManifest`

Определён в `Tools/ModelCompiler/Program.cs`:

```csharp
internal sealed class ModelManifest
{
    public List<ModelEntry> Models { get; set; } = [];
}
```

Построчно:

```csharp
internal sealed class ModelManifest
```

- `internal` — класс нужен только внутри ModelCompiler.
- `sealed` — наследование не требуется.
- Класс представляет корневой JSON-объект `models.json`.

```csharp
public List<ModelEntry> Models { get; set; } = [];
```

- Имя `Models` соответствует JSON-полю `models`.
- `List<ModelEntry>` хранит все описания моделей.
- `= []` создаёт пустой список, если JSON пока не заполнен.

JSON:

```json
{
  "models": []
}
```

## 3. `ModelEntry`

```csharp
internal sealed class ModelEntry
{
    public string Name { get; set; } = "";
    public string Source { get; set; } = "";
    public string Output { get; set; } = "";
    public Dictionary<string, string>? Clips { get; set; }
}
```

`Name`:

```csharp
public string Name { get; set; } = "";
```

Runtime-имя ресурса: `level`, `player`, `skeleton`, `crate`.

`Source`:

```csharp
public string Source { get; set; } = "";
```

Путь к основному FBX. Для персонажа этот файл задаёт mesh, skeleton и bind pose.

`Output`:

```csharp
public string Output { get; set; } = "";
```

Путь готового `.3dmodel`.

`Clips`:

```csharp
public Dictionary<string, string>? Clips { get; set; }
```

Словарь `имя клипа → путь к FBX`. `?` означает, что у статической модели словаря может не быть.

# Часть 2. CPU-структуры модели

## 4. `CompiledModelFormat`

```csharp
internal static class CompiledModelFormat
{
    public const uint Magic = 0x4D4C4433;
    public const int Version = 1;
    public const int MaximumArrayLength = 100_000_000;
}
```

Статический класс не создаётся через `new`. Он только группирует константы формата.

`Magic` проверяет тип файла. `Version` проверяет совместимость. `MaximumArrayLength` защищает чтение повреждённых данных.

## 5. `ModelData`

```csharp
internal sealed class ModelData
{
    public List<NodeData> Nodes { get; } = [];
    public List<MeshData> Meshes { get; } = [];
    public List<ClipData> Clips { get; } = [];
}
```

Это полный CPU-объект одной модели.

`Nodes` хранит иерархию сцены и скелета.

`Meshes` хранит геометрию.

`Clips` хранит анимации.

ModelCompiler заполняет этот объект, `ModelDataIo.Write` сохраняет, а `ModelDataIo.Read` восстанавливает.

## 6. `NodeData`

```csharp
internal sealed record NodeData(
    string Name,
    int Parent,
    Matrix4x4 Bind);
```

`record` автоматически создаёт конструктор и свойства.

`Name` — имя Assimp/Blender-узла.

`Parent` — индекс родителя в `ModelData.Nodes`. Значение `-1` означает root.

`Bind` — локальная трансформация узла в исходной позе.

Пример:

```text
Index 0: Root,  Parent -1
Index 1: Hips,  Parent 0
Index 2: Spine, Parent 1
Index 3: Head,  Parent 2
```

## 7. `BoneData`

```csharp
internal sealed record BoneData(
    int Node,
    Matrix4x4 Offset);
```

`Node` — индекс соответствующего узла.

`Offset` — inverse bind matrix кости.

Runtime рассчитывает:

```csharp
Offset * GlobalNode * InverseRoot
```

## 8. `VertexData`

```csharp
internal readonly record struct VertexData(
    Vector3 Position,
    Vector3 Normal,
    Vector2 Uv,
    byte B0,
    byte B1,
    byte B2,
    byte B3,
    Vector4 Weights);
```

`readonly struct` хранится как значение и не изменяется после создания.

`Position` — координата вершины.

`Normal` — направление поверхности для освещения.

`Uv` — координата текстуры.

`B0`–`B3` — четыре индекса костей.

`Weights` — четыре коэффициента влияния костей.

Сумма компонентов `Weights` должна быть около 1.

## 9. `MeshData`

```csharp
internal sealed class MeshData
{
    public string Name { get; init; } = "Mesh";
    public int Node { get; init; }
    public VertexData[] Vertices { get; init; } = [];
    public int[] Indices { get; init; } = [];
    public BoneData[] Bones { get; init; } = [];
}
```

`Name` нужен для диагностики.

`Node` указывает владельца меша.

`Vertices` — уникальные вершины.

`Indices` — порядок сборки треугольников.

`Bones` — кости именно этого меша. Пустой массив означает статический меш.

`init` разрешает установить значение при создании объекта, но не менять позже обычным присваиванием.

## 10. `VectorKey`

```csharp
internal readonly record struct VectorKey(
    double Time,
    Vector3 Value);
```

Используется для position и scale animation keys.

`Time` хранится в animation ticks.

`Value` — позиция или масштаб в этот момент.

## 11. `QuaternionKey`

```csharp
internal readonly record struct QuaternionKey(
    double Time,
    Quaternion Value);
```

Хранит rotation key. Quaternion используется вместо Euler angles, чтобы избежать неправильного вращения и gimbal lock.

## 12. `ChannelData`

```csharp
internal sealed class ChannelData
{
    public int Node { get; init; }
    public VectorKey[] Positions { get; init; } = [];
    public QuaternionKey[] Rotations { get; init; } = [];
    public VectorKey[] Scales { get; init; } = [];
}
```

Один channel управляет одним node.

`Positions` перемещает узел.

`Rotations` вращает.

`Scales` масштабирует.

Если массив пустой, runtime использует соответствующее значение bind pose.

## 13. `ClipData`

```csharp
internal sealed class ClipData
{
    public string Name { get; init; } = "Clip";
    public double Duration { get; init; }
    public double TicksPerSecond { get; init; }
    public List<ChannelData> Channels { get; } = [];
}
```

`Name` — `Idle`, `Run`, `Attack`.

`Duration` — длительность в ticks.

`TicksPerSecond` переводит секунды игры в ticks.

`Channels` — изменения узлов во времени.

# Часть 3. Сериализация

## 14. `ModelDataIo`

```csharp
internal static class ModelDataIo
```

Класс не хранит состояние. Он предоставляет методы записи и чтения.

## 15. `Write`

```csharp
public static void Write(string path, ModelData model)
```

Метод создаёт папку, открывает `FileStream`, создаёт `BinaryWriter`, записывает header, nodes, meshes и clips.

Порядок секций является частью формата.

## 16. `Read`

```csharp
public static ModelData Read(Stream stream)
```

Создаёт `BinaryReader`, проверяет header, создаёт пустой `ModelData`, затем заполняет его секции.

## 17. Парные методы

Каждому writer соответствует reader:

```text
WriteNodes          ↔ ReadNodes
WriteMeshes         ↔ ReadMeshes
WriteVertices       ↔ ReadVertices
WriteIndices        ↔ ReadIndices
WriteBones          ↔ ReadBones
WriteClips          ↔ ReadClips
WriteVectorKeys     ↔ ReadVectorKeys
WriteQuaternionKeys ↔ ReadQuaternionKeys
WriteMatrix         ↔ ReadMatrix
```

Они должны использовать одинаковый порядок полей.

# Часть 4. Вспомогательные классы компилятора

## 18. `VertexInfluence`

```csharp
internal readonly record struct VertexInfluence(
    int BoneIndex,
    float Weight);
```

Временная структура ModelCompiler. Она нужна только во время импорта FBX.

`BoneIndex` указывает кость в меше.

`Weight` задаёт силу влияния.

Перед сохранением влияния сортируются, выбираются четыре сильнейших и нормализуются.

# Часть 5. Runtime-классы

## 19. `CompiledModel`

```csharp
internal sealed class CompiledModel : IDisposable
```

Главный runtime-класс.

Он:

- читает `.3dmodel`;
- создаёт GPU-буферы;
- хранит clips;
- вычисляет pose;
- строит bone matrices;
- передаёт параметры в ToonShader;
- выполняет draw calls;
- освобождает GPU-ресурсы.

## 20. Поля `CompiledModel`

`graphicsDevice` — доступ к GPU.

`modelData` — CPU-описание.

`runtimeMeshes` — GPU-меши.

`clipsByName` — быстрый поиск клипа.

`localTransforms` — локальные матрицы текущего кадра.

`globalTransforms` — глобальные матрицы текущего кадра.

`bindPoseGlobalTransforms` — глобальная исходная поза.

`inverseRootTransform` — компенсация root transform.

## 21. `RuntimeMesh`

```csharp
private sealed record RuntimeMesh(
    MeshData Source,
    VertexBuffer VertexBuffer,
    IndexBuffer IndexBuffer,
    Effect Effect,
    Matrix[] BoneTransforms,
    Texture2D Texture);
```

`Source` хранит CPU metadata.

`VertexBuffer` хранит вершины на GPU.

`IndexBuffer` хранит triangle indices на GPU.

`Effect` — clone ToonShader для этого меша.

`BoneTransforms` — массив из 72 matrices.

`Texture` — текстура меша.

## 22. `RuntimeVertex`

```csharp
internal readonly struct RuntimeVertex : IVertexType
```

Это формат одной вершины, отправляемой на GPU.

Поля:

```csharp
Vector3 position;
Vector3 normal;
Vector2 textureCoordinate;
Byte4 boneIndices;
Vector4 boneWeights;
```

`VertexDeclaration` объясняет GPU offsets и semantics этих полей.

`IVertexType.VertexDeclaration` возвращает declaration MonoGame.

# Часть 6. Игровой аниматор

## 23. `CharacterAnimator`

```csharp
public sealed class CharacterAnimator
```

Это адаптер между игровой логикой и `CompiledModel`.

Он не рассчитывает кости самостоятельно. Он выбирает клип, хранит время и вызывает правильную перегрузку `CompiledModel.Draw`.

## 24. Поля `CharacterAnimator`

```csharp
private CompiledModel model = null!;
private float stateTime;
private bool isLooping = true;
public string CurrentClip { get; private set; } = "";
```

`model` — загруженный player/skeleton.

`stateTime` — секунды текущего клипа.

`isLooping` — повторять ли клип.

`CurrentClip` — текущее логическое имя.

## 25. `LoadContent`

Метод выбирает `player` или `skeleton`, загружает `.3dmodel`, texture и ToonShader, затем выбирает первый клип как начальный.

## 26. `Play`

Если имя изменилось, `stateTime` сбрасывается в 0. Если каждый кадр запрашивается то же имя, время не сбрасывается и анимация продолжается.

## 27. `Update`

```csharp
stateTime += deltaTime;
```

Продвигает текущий клип на число секунд кадра.

## 28. `Draw`

Передаёт в `CompiledModel` имя клипа, время, loop flag и matrices камеры/мира.

# Часть 7. Жизненный цикл

## 29. Полная последовательность

Во время build:

```text
ModelManifest
→ ModelEntry
→ Assimp Scene
→ ModelData
→ ModelDataIo.Write
→ .3dmodel
```

Во время запуска:

```text
.3dmodel
→ ModelDataIo.Read
→ ModelData
→ CompiledModel
→ RuntimeMesh/RuntimeVertex
→ GPU
```

Во время кадра:

```text
CharacterAnimator.Play/Update
→ CompiledModel.Draw
→ ClipData/ChannelData
→ globalTransforms
→ BoneTransforms
→ ToonShader
→ DrawIndexedPrimitives
```

## 30. Владение ресурсами

`ContentManager` владеет `Texture2D` и исходным `Effect`.

`CompiledModel` владеет `VertexBuffer`, `IndexBuffer` и clones эффекта.

Поэтому `CompiledModel.Dispose` освобождает GPU-буферы и clones, но не текстуру.

# Часть 8. Методы загрузчика — подробно

Ниже описан не только результат работы методов, но и зачем существует каждый шаг. Это особенно важно при изменении бинарного формата: запись и чтение обязаны оставаться зеркальными.

## 31. `ModelDataIo.Write`

Сигнатура:

```csharp
public static void Write(string path, ModelData model)
```

- `path` — полный путь будущего файла `.3dmodel`.
- `model` — уже собранное CPU-представление модели.
- Метод ничего не возвращает: результатом является файл на диске.

Внутри метод выполняет шаги в строгом порядке:

1. Получает папку из `path`.
2. Создаёт её, если она отсутствует.
3. Открывает `FileStream` для записи.
4. Оборачивает поток в `BinaryWriter`.
5. Записывает `Magic` и `Version`.
6. Записывает узлы.
7. Записывает меши.
8. Записывает анимационные клипы.

Почему порядок нельзя менять произвольно: `Read` ожидает секции в точно таком же порядке. Если добавить новое поле в `WriteMeshes`, такое же поле в той же позиции нужно прочитать в `ReadMeshes`.

## 32. `ModelDataIo.Read`

```csharp
public static ModelData Read(Stream stream)
```

- `stream` обычно открыт на файле `Content/Models/*.3dmodel`.
- `BinaryReader` читает примитивы без текстового парсинга.
- `ValidateHeader` сразу отсеивает чужой или устаревший файл.
- Затем создаётся пустой `ModelData`.
- `ReadNodes`, `ReadMeshes` и `ReadClips` заполняют его коллекции.
- Готовый объект возвращается в `CompiledModel.Load`.

Файл во время выполнения не держится открытым: поток закрывается после чтения, а модель остаётся в памяти как обычные объекты C# и GPU-буферы.

## 33. `ValidateHeader`

Метод читает первые два значения файла:

```text
uint Magic
int Version
```

Если `Magic` не совпал, передан не `.3dmodel` этого проекта либо файл повреждён. Если не совпала `Version`, компилятор моделей и runtime используют разные версии формата. Правильное исправление — пересобрать ассеты, а не отключать проверку.

## 34. `WriteNodes` и `ReadNodes`

Для списка узлов сначала записывается количество. Затем для каждого узла идут:

```text
Name
ParentIndex
LocalTransform
```

`ParentIndex == -1` обозначает корень. Остальные индексы указывают на элемент того же массива `Nodes`.

Матрица записывается как 16 чисел `float` в фиксированном порядке. `ReadMatrix` собирает их обратно в `Matrix4x4`.

## 35. `WriteMeshes` и `ReadMeshes`

Каждый меш хранит:

```text
Name
NodeIndex
Vertices
Indices
Bones
```

`NodeIndex` связывает меш с узлом сцены. `Vertices` и `Indices` формируют геометрию. `Bones` нужны для skinning. У статической модели массив костей может быть пустым.

## 36. `WriteVertices` и `ReadVertices`

Одна вершина сериализуется в следующем порядке:

```text
Position.X/Y/Z
Normal.X/Y/Z
TextureCoordinate.X/Y
BoneIndices.X/Y/Z/W
BoneWeights.X/Y/Z/W
```

`ReadVertices` читает тот же набор и создаёт `VertexData`. После чтения `CompiledModel.ConvertVertices` превращает эти данные в `RuntimeVertex`, формат которого совпадает с входом vertex shader.

Четыре индекса и четыре веса означают, что одна вершина поддерживает максимум четыре влияющие кости. Это распространённый компромисс между качеством и стоимостью skinning.

## 37. `WriteIndices` и `ReadIndices`

Индексы определяют, какие вершины образуют треугольники. Каждые три последовательных значения — один треугольник.

Перед массивом записывается длина. При чтении длина проходит через `ReadArrayLength`, чтобы повреждённый файл не заставил программу выделить гигантский массив.

## 38. `WriteBones` и `ReadBones`

Для каждой кости сохраняются:

```text
NodeIndex
OffsetMatrix
```

`NodeIndex` находит анимируемый узел. `OffsetMatrix` переводит вершину из пространства меша в пространство кости в bind pose. Во время кадра эта матрица объединяется с текущей глобальной матрицей узла.

## 39. `WriteClips` и `ReadClips`

Клип содержит:

```text
Name
DurationTicks
TicksPerSecond
Channels
```

Канал относится к одному узлу и содержит три независимые дорожки: position, rotation, scale. Это позволяет хранить только те ключи, которые реально экспортированы.

## 40. Методы ключей

`WriteVectorKeys`/`ReadVectorKeys` используются и для позиции, и для масштаба. В каждом ключе находятся `Time` и `Vector3 Value`.

`WriteQuaternionKeys`/`ReadQuaternionKeys` обслуживают вращение. В ключе находятся `Time` и `Quaternion Value`.

Время хранится в ticks клипа, не в секундах игры. Перевод выполняет `CalculateAnimationTick`.

## 41. `ReadArrayLength`

Это защитный метод. Он отвергает:

- отрицательную длину;
- длину больше `MaximumArrayLength`;
- явно повреждённый файл до выделения массива.

Параметр `valueName` нужен для понятного текста исключения: из сообщения видно, массив какого типа оказался неверным.

## 42. `CompiledModel.Load`

```csharp
public static CompiledModel Load(
    GraphicsDevice graphicsDevice,
    string path,
    Effect shader,
    Texture2D texture)
```

Это главная точка входа runtime-загрузчика.

1. Открывает `.3dmodel` только для чтения.
2. Вызывает `ModelDataIo.Read`.
3. Передаёт прочитанный `ModelData` в закрытый конструктор.
4. Конструктор создаёт lookup клипов и массивы трансформаций.
5. Для каждого `MeshData` вызывает `CreateRuntimeMesh`.
6. Возвращает полностью готовый объект.

`Effect` и `Texture2D` уже загружены MonoGame ContentManager. Сам `.3dmodel` ContentManager не разбирает — это делает наш код.

## 43. Закрытый конструктор `CompiledModel`

Конструктор закрыт, чтобы нельзя было случайно создать наполовину заполненную модель. В него попадают только проверенные данные из `Load`.

Он создаёт:

- словарь `clipsByName`, чтобы не искать клип полным перебором каждый кадр;
- `localTransforms` для текущих локальных матриц;
- `globalTransforms` для матриц с учётом родителей;
- `bindPoseGlobalTransforms` для исходной позы;
- массив `runtimeMeshes` с GPU-ресурсами.

## 44. `GetClipDuration`

Метод находит клип по имени и переводит его длительность из ticks в секунды:

```text
seconds = DurationTicks / TicksPerSecond
```

Если экспортёр записал нулевой `TicksPerSecond`, код использует безопасное значение, чтобы не делить на ноль.

## 45. Две перегрузки `Draw`

Первая перегрузка предназначена для статической модели:

```csharp
Draw(world, view, projection)
```

Она возвращает узлы в bind pose, рассчитывает глобальные матрицы и рисует меши.

Вторая перегрузка предназначена для анимированной модели:

```csharp
Draw(world, view, projection, clipName, time, loop)
```

Она:

1. возвращает исходные локальные матрицы;
2. находит клип;
3. вычисляет текущий tick;
4. применяет каналы клипа;
5. пересчитывает глобальные матрицы;
6. рисует все меши.

Возврат в bind pose перед применением клипа обязателен. Иначе значения прошлого кадра или прошлого клипа могли бы остаться в узлах, для которых новый клип не содержит каналов.

## 46. `CalculateAnimationTick`

Метод получает секунды игрового времени и переводит их в ticks:

```text
tick = seconds * TicksPerSecond
```

При `loop == true` используется остаток от длительности, поэтому клип начинается заново. При `loop == false` значение ограничивается концом клипа.

## 47. `ApplyAnimationChannels`

Для каждого канала метод:

1. получает индекс узла;
2. интерполирует position;
3. интерполирует rotation;
4. интерполирует scale;
5. строит новую локальную матрицу узла.

Типичный порядок преобразований:

```text
Scale × Rotation × Translation
```

Канал меняет только один узел. Движение автоматически передаётся детям позже, в `CalculateGlobalTransforms`.

## 48. Интерполяция ключей

`FindKeyIndex` бинарным поиском находит два соседних ключа. Это быстрее полного перебора длинной дорожки.

`CalculateInterpolationAmount` вычисляет долю между ними в диапазоне `0..1`.

`InterpolateVectorKeys` применяет линейную интерполяцию к position/scale.

`InterpolateQuaternionKeys` применяет quaternion interpolation к rotation, чтобы вращение не ломалось как обычный набор четырёх чисел.

Если ключ только один, возвращается его значение. Если ключей нет, сохраняется компонент bind pose.

## 49. `CalculateGlobalTransforms`

Локальная матрица описывает узел относительно родителя. Глобальная описывает его относительно корня модели.

Для корня:

```text
global = local
```

Для дочернего узла:

```text
global = local × parentGlobal
```

Поэтому бедро двигает голень, а голень — стопу, даже если в клипе есть ключи только для бедра.

## 50. `CreateRuntimeMesh`

Этот метод переводит CPU-описание одного меша в объекты MonoGame:

1. вызывает `ConvertVertices`;
2. создаёт `VertexBuffer`;
3. копирует в него вершины;
4. создаёт `IndexBuffer`;
5. копирует индексы;
6. клонирует shader effect;
7. создаёт массив bone matrices;
8. возвращает `RuntimeMesh`.

Эта работа выполняется один раз при загрузке, а не каждый кадр.

## 51. `ConvertVertices`

Метод переносит значения из сериализуемого `VertexData` в GPU-совместимый `RuntimeVertex`.

Индексы костей упаковываются в `Byte4`, потому что shader ждёт `BLENDINDICES0`. Веса остаются `Vector4` и приходят как `BLENDWEIGHT0`.

Если номер кости больше 255, он не помещается в один byte. В текущем shader предел ещё строже: `MAX_BONES = 72`, поэтому компилятор и runtime не должны создавать меш с большим числом используемых костей.

## 52. `PrepareMeshTransforms`

Для статического меша метод возвращает его обычную world matrix.

Для skinned-меша он заполняет `BoneTransforms`. Общий смысл матрицы одной кости:

```text
OffsetMatrix × CurrentBoneGlobalTransform × mesh/root compensation
```

Полученный массив отправляется в параметр shader `Bones`. Неиспользуемые элементы остаются identity matrices.

## 53. `ApplyEffectParameters`

Метод передаёт в shader:

- `World`;
- `View`;
- `Projection`;
- `CameraPosition`;
- `LightDirection`;
- `ModelTexture`;
- `Bones`.

Имена должны точно совпадать с переменными в `ToonShader.fx`. Оператор `?.` позволяет пропустить необязательный параметр, но для обязательного skinning-параметра опечатка приведёт к неправильной отрисовке.

## 54. `DrawMesh` и `DrawMeshes`

`DrawMeshes` проходит по всем runtime-мешам.

`DrawMesh`:

1. устанавливает vertex buffer;
2. устанавливает index buffer;
3. готовит world/bone matrices;
4. задаёт параметры эффекта;
5. проходит по pass техники;
6. вызывает `DrawIndexedPrimitives`.

Один вызов `DrawIndexedPrimitives` отправляет на GPU треугольники одного меша.

## 55. `BuildPlatforms`

Этот метод используется только моделью уровня. Он ищет узлы с именами, считающимися проходимыми, вычисляет bounding box соответствующего меша и создаёт игровые `Level.Platform`.

То есть геометрия уровня одновременно служит визуальной моделью и источником простых коллизий. Персонажи и враги этот метод не вызывают.

## 56. `Dispose`

Метод освобождает то, что `CompiledModel` создал сам:

- каждый `VertexBuffer`;
- каждый `IndexBuffer`;
- каждый clone `Effect`.

Текстура и исходный effect не уничтожаются, потому что ими владеет `ContentManager`. Двойное освобождение общих ресурсов могло бы сломать другие модели.

# Часть 9. `CharacterAnimator` построчно

## 57. Назначение класса

`CharacterAnimator` — маленький фасад между игровой логикой и `CompiledModel`. Игровому персонажу не нужно знать про ticks, каналы, GPU-буферы и кости. Он говорит: «проигрывай Run и нарисуй модель».

## 58. Поля

```csharp
private CompiledModel model = null!;
private float stateTime;
private bool loop = true;
public string CurrentClip { get; private set; } = "";
```

- `model` — загруженная runtime-модель;
- `stateTime` — сколько секунд проигрывается текущее состояние;
- `loop` — нужно ли зацикливать клип;
- `CurrentClip` — имя текущей анимации, доступное для чтения снаружи.

`null!` сообщает компилятору C#, что поле будет заполнено в `LoadContent`. Вызывать `Play` или `Draw` раньше загрузки нельзя.

## 59. `LoadContent`

Метод принимает `GraphicsDevice`, папку/путь модели, shader и texture, затем вызывает `CompiledModel.Load`. После этого аниматор готов к работе.

Важно: сюда передаётся готовый `.3dmodel`, а не FBX. FBX используется только отдельным проектом ModelCompiler во время сборки.

## 60. `Play`

```csharp
public void Play(string clipName, bool loop = true)
```

Если уже играет тот же клип с тем же режимом loop, время не сбрасывается. Это защищает анимацию от постоянного старта с первого кадра, когда игровая логика каждый `Update` снова вызывает `Play("Run")`.

При реальной смене клипа:

```text
CurrentClip = clipName
stateTime = 0
this.loop = loop
```

## 61. `Update`

```csharp
stateTime += deltaTime;
```

`deltaTime` — длительность последнего игрового кадра в секундах. Поэтому скорость анимации не зависит от FPS.

## 62. `Draw`

Метод передаёт в `CompiledModel.Draw` world/view/projection, текущее имя клипа, накопленное время и флаг loop.

Сам `CharacterAnimator` не интерполирует кости. Он только хранит состояние воспроизведения; всю математику выполняет `CompiledModel`.

# Часть 10. Где менять код

## 63. Добавляется новое поле в `.3dmodel`

Нужно синхронно изменить:

1. соответствующий `*Data` класс;
2. writer в `ModelDataIo`;
3. reader в `ModelDataIo`;
4. код заполнения поля в ModelCompiler;
5. использование поля в `CompiledModel`;
6. `CompiledModelFormat.Version`.

После этого удалить старые результаты либо просто выполнить обычную сборку проекта: ModelCompiler заново создаст `.3dmodel`.

## 64. Добавляется новая модель или анимация без изменения формата

Классы загрузчика менять не требуется. Нужно изменить только `Assets/models.json`, положить исходные FBX в указанное место и собрать проект. Полная пошаговая инструкция находится в `AddingModelsAndAnimations.md` и `AddingModelsLineByLine.md`.
