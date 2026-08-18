# Разбор `CompiledModel.cs`

`CompiledModel.cs` отвечает за runtime-часть: чтение готовых данных, создание GPU-буферов, расчёт анимации, передачу параметров в ToonShader и отрисовку.

## 1. Поля класса

```csharp
private readonly GraphicsDevice graphicsDevice;
private readonly ModelData modelData;
private readonly RuntimeMesh[] runtimeMeshes;
private readonly Dictionary<string, ClipData> clipsByName;
```

- `graphicsDevice` создаёт GPU-буферы и выполняет draw calls.
- `modelData` хранит CPU-описание модели.
- `runtimeMeshes` содержит подготовленные для GPU меши.
- `clipsByName` быстро получает клип по имени.

Массивы трансформаций:

```csharp
private readonly Matrix[] localTransforms;
private readonly Matrix[] globalTransforms;
private readonly Matrix[] bindPoseGlobalTransforms;
private readonly Matrix inverseRootTransform;
```

- `localTransforms` — матрицы относительно родителей.
- `globalTransforms` — итоговые матрицы всей иерархии.
- `bindPoseGlobalTransforms` — исходная поза скелета.
- `inverseRootTransform` убирает root transform при скиннинге.

## 2. Конструктор

Конструктор закрытый и вызывается из `Load`. Клипы превращаются в словарь без учёта регистра. Bind-матрицы преобразуются из `System.Numerics` в MonoGame. Рабочие массивы создаются один раз и переиспользуются каждый кадр.

Каждый `MeshData` преобразуется в `RuntimeMesh` с GPU-буферами и отдельным clone ToonShader.

## 3. `Load`

```csharp
public static CompiledModel Load(
    GraphicsDevice graphicsDevice,
    string modelName,
    Texture2D texture,
    Effect toonEffect)
```

Для имени `player` строится путь `Content/Models/player.3dmodel`. `ModelDataIo.Read` восстанавливает структуры, после чего конструктор создаёт GPU-ресурсы.

## 4. `GetClipDuration`

Клип ищется в `clipsByName`. Продолжительность переводится из ticks в секунды:

```csharp
seconds = clip.Duration / clip.TicksPerSecond;
```

Неизвестное имя вызывает понятное исключение.

## 5. Статический `Draw`

```csharp
public void Draw(Matrix world, Matrix view, Matrix projection)
```

Метод копирует глобальную bind pose в рабочий массив и вызывает `DrawMeshes`. Используется уровнем и статическими объектами.

## 6. Анимированный `Draw`

Порядок работы:

1. Найти клип.
2. Сбросить узлы в bind pose.
3. Перевести секунды в animation ticks.
4. Применить каналы позиции, вращения и масштаба.
5. Построить глобальные матрицы.
6. Рассчитать bone transforms.
7. Нарисовать меши.

## 7. `CalculateAnimationTick`

```csharp
double animationTick = elapsedSeconds * clip.TicksPerSecond;
```

Loop использует `animationTick % clip.Duration`. Одноразовый клип ограничивается через `Math.Min` и остаётся на последнем кадре.

## 8. `ApplyAnimationChannels`

Каждый канал относится к одному узлу. Bind-матрица разбирается через `Matrix.Decompose`. Позиция и масштаб интерполируются через `Vector3.Lerp`, вращение — через `Quaternion.Slerp`.

Матрица собирается обратно:

```csharp
Matrix.CreateScale(scale) *
Matrix.CreateFromQuaternion(rotation) *
Matrix.CreateTranslation(position)
```

Если ключей нет, используется значение bind pose.

## 9. Иерархия

`CalculateGlobalTransforms` рассчитывает:

```csharp
global = parentIndex < 0
    ? local
    : local * parentGlobal;
```

Поэтому движение `Hips` влияет на `Spine`, `Chest`, руки и голову.

## 10. Поиск ключей

`FindKeyIndex` использует бинарный поиск соседнего ключа. Это быстрее полного прохода по всем animation keys.

## 11. `CreateRuntimeMesh`

Создаются:

```text
RuntimeVertex[]
VertexBuffer
IndexBuffer
Effect.Clone()
Matrix[72]
```

Shader клонируется для каждого меша, чтобы параметры мешей не конфликтовали.

## 12. `RuntimeVertex`

```text
Position           Vector3
Normal             Vector3
TextureCoordinate  Vector2
BoneIndices        Byte4
BoneWeights        Vector4
```

Offsets в `VertexDeclaration` совпадают с semantics `ToonShader.fx`.

## 13. Skin matrices

`PrepareMeshTransforms` рассчитывает:

```csharp
bone.Offset *
globalTransforms[bone.Node] *
inverseRootTransform
```

Статический меш использует identity-кость, а node transform включается в `World`.

## 14. Параметры ToonShader

`ApplyEffectParameters` устанавливает `World`, `View`, `Projection`, `CameraPosition`, `LightDirection`, `ModelTexture` и `Bones`.

Позиция камеры извлекается из `Matrix.Invert(view).Translation`.

## 15. Draw call

Выбираются vertex/index buffers, применяется shader pass и вызывается `DrawIndexedPrimitives`. Число треугольников равно `indices.Length / 3`.

## 16. Платформы

`BuildPlatforms` выбирает меши по именам `Ground`, `Bridge`, `Trail`, `Cylinder` и `Platform`. Для их вершин строится `BoundingBox`. Маленькие декоративные меши пропускаются.

## 17. `Dispose`

Освобождаются `VertexBuffer`, `IndexBuffer` и clone эффекта каждого меша. Текстура принадлежит `ContentManager`, поэтому здесь не освобождается.
