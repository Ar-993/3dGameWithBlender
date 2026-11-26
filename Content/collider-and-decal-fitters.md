 # Автоматический коллайдер предмета и декали в координатах стены

Этот гайд обновляет проект, в котором уже есть физика составных капсул и загрузка моделей уровня. Код обоих фиттеров и новой загрузки декалей приведён целиком. Если в проекте ещё нет составных капсул или прозрачного прохода рендера, их базовое подключение описано в первом и четвёртом разделах [общего гайда](<текущие штуки.md>). Для конфигурации предметов и декалей используй примеры ниже.

## 1. Collider fitter для всех предметов

### Было

В каталоге `collider` из JSON мог переопределить форму. У физического тела без готовой формы `RigidBodyShape` строил сетку сфер. Для нового предмета приходилось вручную подбирать координаты капсул, если сетка получалась слишком плотной.

```csharp
var collider = data.Collider?.ToCollider() ?? ItemColliderFitter.Fit(center, halfExtents);
```

### Стало

При первой загрузке типа каталог читает габариты модели из импортированного FBX. Для скелетного меша используются вершины в исходной позе с применёнными трансформациями костей. `height` из каталога задаёт высоту модели в игре. Остальные оси масштабируются с тем же коэффициентом. `ItemDefinition` хранит рассчитанную форму, а каждый экземпляр предмета использует её для своего физического тела.

Предметы, созданные напрямую через C#, проходят через тот же фиттер. Для тела без модели размеры берутся из переданных `halfExtents`, либо из уже имеющихся `radius` и `height`.

### Шаг 1. Добавь `Libraries/Ars3D.Items/Physics/ItemColliderFitter.cs`

Если предыдущая версия фиттера есть в `Items`, перенеси его в библиотеку и используй содержимое ниже. Фиттер работает с `System.Numerics.Vector3`, поэтому его могут использовать и каталог игры, и библиотека физики.

```csharp
using System.Numerics;

namespace Ars3D.Items.Physics;

internal static class ItemColliderFitter
{
    public static ItemCollider Fit(Vector3 center, Vector3 halfExtents)
    {
        (Vector3 Direction, float Extent)[] axes =
        [
            (Vector3.UnitX, halfExtents.X),
            (Vector3.UnitY, halfExtents.Y),
            (Vector3.UnitZ, halfExtents.Z)
        ];
        Array.Sort(axes, (first, second) => second.Extent.CompareTo(first.Extent));
        var length = axes[0];
        var width = axes[1];
        float thickness = axes[2].Extent;

        // Одной капсулы достаточно, когда поперечные размеры близки.
        if (width.Extent <= thickness * 1.25f)
        {
            Vector3 offset = length.Direction * (length.Extent - thickness);
            return new ItemCollider([CreateCapsule(center, offset, thickness)]);
        }

        // Две соседние капсулы перекрываются и вместе покрывают ширину модели.
        float radius = MathF.Max(thickness, width.Extent * 0.5f);
        Vector3 endOffset = length.Direction * (length.Extent - radius);
        Vector3 sideOffset = width.Direction * (width.Extent - radius);
        return new ItemCollider(
        [
            CreateCapsule(center - sideOffset, endOffset, radius),
            CreateCapsule(center + sideOffset, endOffset, radius)
        ]);
    }

    private static ColliderCapsule CreateCapsule(Vector3 center, Vector3 endOffset, float radius) =>
        new(center - endOffset, center + endOffset, radius);
}
```

Одна капсула идёт вдоль самой длинной оси, когда два поперечных размера близки. Удлинённая часть заканчивается за один радиус до границы, остальное покрывают закруглённые концы. У широкой формы фиттер создаёт две капсулы со сдвигом по средней оси. В формуле `width.Extent * 0.5f` используется половина полуширины, чтобы капсулы соприкасались или перекрывались. Коллайдер приближённый. Для очень плоской формы выбранный радиус может увеличить толщину, а углы и мелкие выступы могут выходить за капсулы.

### Шаг 2. Передай масштабированные габариты в определение предмета

В `Models/ItemModelTransform.cs` замени старый `FitToHeight` этими двумя перегрузками. Сохрани существующие методы `GetBindPoseBounds`, `SkinPosition` и преобразования матриц.

```csharp
    public static Matrix FitToHeight(
        ModelData data,
        ModelHierarchy hierarchy,
        float height) => FitToHeight(data, hierarchy, height, out _);

    public static Matrix FitToHeight(
        ModelData data,
        ModelHierarchy hierarchy,
        float height,
        out BoundingBox normalizedBounds)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(hierarchy);
        if (!float.IsFinite(height) || height <= 0f)
            throw new ArgumentOutOfRangeException(nameof(height), "Высота предмета должна быть положительной.");

        BoundingBox bounds = GetBindPoseBounds(data, hierarchy);
        float sourceHeight = bounds.Max.Y - bounds.Min.Y;
        if (sourceHeight <= 0f)
            throw new InvalidDataException("Модель предмета должна содержать геометрию с ненулевой высотой.");

        var bottomCenter = new Vector3(
            (bounds.Min.X + bounds.Max.X) * 0.5f,
            bounds.Min.Y,
            (bounds.Min.Z + bounds.Max.Z) * 0.5f);
        float scale = height / sourceHeight;
        var halfExtents = (bounds.Max - bounds.Min) * (scale * 0.5f);
        normalizedBounds = new BoundingBox(
            new Vector3(-halfExtents.X, 0f, -halfExtents.Z),
            new Vector3(halfExtents.X, height, halfExtents.Z));
        return Matrix.CreateTranslation(-bottomCenter) * Matrix.CreateScale(scale);
    }
```

В `Items/ItemCatalog.cs`, внутри `GetOrLoadItemType`, замени строки расчёта масштаба и создания определения.

```csharp
CompiledModel model = GetOrLoadModel(definitionData);
Matrix normalization = ItemModelTransform.FitToHeight(
    model.Data, model.Hierarchy, definitionData.Height, out BoundingBox bounds);
ItemDefinition definition = CreateDefinition(definitionData, bounds);
```

Там же замени метод `CreateDefinition`. Он передаёт габариты в библиотеку. Для плоских осей добавляется небольшая положительная толщина, которую требует физическое тело.

```csharp
    private static ItemDefinition CreateDefinition(ItemDefinitionData data, BoundingBox bounds)
    {
        Vector3 halfExtents = (bounds.Max - bounds.Min) * 0.5f;
        // У плоской модели тоже должна быть положительная толщина для расчёта физики.
        halfExtents = Vector3.Max(halfExtents, new Vector3(halfExtents.Y * 0.001f));

        return new ItemDefinition(
            id: data.Id,
            displayName: data.DisplayName,
            radius: data.Radius,
            height: data.Height,
            halfExtents: ItemMath.ToLibrary(halfExtents),
            spawnAngularVelocity: ItemCatalogVectors.Read(data.SpawnSpin, "spawnSpin"),
            heldYaw: MathHelper.ToRadians(data.HeldYawDegrees),
            bodySettings: data.Physics,
            interaction: data.Interaction?.ToSettings());
    }
```

### Шаг 3. Прими старый JSON и создавай форму через `ItemDefinition`

В `Items/ItemCatalogData.cs` добавь два импорта. Замени старое свойство `Collider` в `ItemDefinitionData` на свойство ниже. Удали классы `ItemColliderData` и `ColliderCapsuleData`. Старое поле по-прежнему принимается, даже при строгой проверке имён JSON. Его содержимое не влияет на форму.

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
```

```csharp
// Внутри ItemDefinitionData.
[JsonPropertyName("collider")]
public JsonElement LegacyCollider { get; init; }
```

В `Libraries/Ars3D.Items/ItemDefinition.cs` замени объявление свойства и присваивание в конце конструктора. Параметр конструктора `ItemCollider? collider = null` остаётся доступным для явной формы, заданной из C#.

```csharp
// Было
public ItemCollider? Collider { get; }
// В конструкторе было
Collider = collider;
```

```csharp
// Стало
public ItemCollider Collider { get; }
// В конструкторе, после расчёта dimensions
Collider = collider ?? ItemColliderFitter.Fit(Vector3.UnitY * (height * 0.5f), dimensions);
```

### Шаг 4. Убери сетку сфер у тел, созданных напрямую

Замени `Libraries/Ars3D.Items/Physics/RigidBodyShape.cs` целиком. Капсулы хранятся относительно центра физического тела. Перед расчётом контакта физика поворачивает их вместе с предметом. Ограничивающий радиус вычисляется по настоящим концам капсул и нужен, в частности, для безопасного выпуска предмета рядом со стеной.

```csharp
using System;
using System.Numerics;
using Ars3D.Items.Geometry;

namespace Ars3D.Items.Physics;

// Все части коллайдера движутся вокруг общего центра массы.
internal sealed class RigidBodyShape
{
    public Vector3 HalfExtents { get; }
    public Vector3 LocalCenter { get; }
    public float BoundingRadius { get; }
    public ColliderCapsule[] Parts { get; }

    public RigidBodyShape(float radius, float height, Vector3? halfExtents, ItemCollider? collider)
    {
        if (!float.IsFinite(radius) || radius <= 0f)
            throw new ArgumentOutOfRangeException(nameof(radius));
        if (!float.IsFinite(height) || height <= 0f)
            throw new ArgumentOutOfRangeException(nameof(height));
        HalfExtents = halfExtents ?? new Vector3(radius, height * 0.5f, radius);
        if (!RigidBody.Finite(HalfExtents) || HalfExtents.X <= 0f || HalfExtents.Y <= 0f || HalfExtents.Z <= 0f)
            throw new ArgumentOutOfRangeException(nameof(halfExtents));
        LocalCenter = Vector3.UnitY * (height * 0.5f);
        collider ??= ItemColliderFitter.Fit(LocalCenter, HalfExtents);
        Parts = collider.Capsules.Select(part => part with
        {
            Start = part.Start - LocalCenter,
            End = part.End - LocalCenter
        }).ToArray();
        BoundingRadius = Parts.Max(part => MathF.Max(part.Start.Length(), part.End.Length()) + part.Radius);
    }

    public Aabb GetBounds(Vector3 center, Quaternion orientation)
    {
        Vector3 minimum = new(float.MaxValue);
        Vector3 maximum = new(float.MinValue);
        foreach (ColliderCapsule part in Parts)
        {
            Vector3 extent = new(part.Radius);
            Vector3 start = center + Vector3.Transform(part.Start, orientation);
            Vector3 end = center + Vector3.Transform(part.End, orientation);
            minimum = Vector3.Min(minimum, Vector3.Min(start, end) - extent);
            maximum = Vector3.Max(maximum, Vector3.Max(start, end) + extent);
        }
        return new(minimum, maximum);
    }
}
```

В `Items/WorldItem.cs` замени `EnableRigidBody`. Когда метод получает новые габариты, он создаёт новое определение с соответствующей формой.

```csharp
    internal void EnableRigidBody(Vector3 halfExtents, Vector3 initialSpin)
    {
        if (isRegistered)
            throw new InvalidOperationException("Configure the item before registering it for interaction.");
        ItemDefinition previous = State.Definition;
        var definition = new ItemDefinition(previous.Id, previous.DisplayName, Radius, Height,
            ItemMath.ToLibrary(halfExtents), ItemMath.ToLibrary(initialSpin), previous.HeldYaw,
            previous.BodySettings, previous.Interaction);
        State = new ItemInstance(definition, State.Position, State.Orientation, State.Velocity,
            ItemMath.ToLibrary(initialSpin), State.InstanceId);
    }
```

В новых записях `items.json` оставляй обычные свойства предмета. Например, минимальная запись для Санты выглядит так. Настройки броска, спавна и физики можно оставить в своих записях.

```json
{
  "id": "santa",
  "displayName": "Дед Мороз",
  "model": "christmas_santa",
  "textureFolder": "Christmas",
  "height": 0.5
}
```

Запусти уровень и нажми **O**. Дебаг берёт капсулы из самого физического тела. У одной или двух капсул видны круглые окончания и линии между ними.

## 2. Decal fitter с координатами по стене

### Было

Для каждой картинки требовались мировая точка `position` и вектор `normal`. Игра бросала луч в заданном направлении, чтобы найти стену.

```json
{
  "node": "6twelve",
  "texture": "sigil.png",
  "position": [-2.11, 1.75, -17.0],
  "normal": [-1, 0, 0],
  "size": [1.25, 1.62],
  "whiteBackground": true
}
```

### Стало

`wall` выбирает узел модели. `side` выбирает его сторону по направлению наружу в координатах уровня. Поддерживаются `-x`, `+x`, `-z`, `+z` для вертикальных стен. `x` и `y` задают **центр** изображения в игровых единицах от нижнего левого угла выбранной стены, если смотреть на неё снаружи. У противоположных стен направление вправо различается.

Например, при `x: 2` и `y: 1.5` центр окажется на две игровые единицы правее левого края и на полторы выше нижнего края. `size` задаёт ширину и высоту изображения в тех же единицах.

### Шаг 1. Добавь `Levels/DecalFitter.cs`

```csharp
using Ars3D.ModelFormat;
using Microsoft.Xna.Framework;
using NumericsVector2 = System.Numerics.Vector2;
using NumericsVector3 = System.Numerics.Vector3;

namespace _3DLight;

internal static class DecalFitter
{
    private const float PlaneTolerance = 0.025f;
    private const float SurfaceOffset = 0.003f;

    public static MeshData? Fit(IEnumerable<Level.TriangleCollider> triangles, Vector3 normal,
        Vector2 position, Vector2 size, string name, float opacity)
    {
        WallPlane? wall = FindWall(triangles, normal);
        if (wall is null)
            return null;

        WallFrame frame = GetFrame(wall, normal, position);
        var vertices = new List<VertexData>();
        var indices = new List<int>();
        foreach (Level.TriangleCollider triangle in wall.Triangles)
        {
            List<Vector3> polygon = ClipTriangle(triangle, frame, size * 0.5f);
            AppendPolygon(polygon, frame, size, vertices, indices);
        }

        return indices.Count == 0 ? null : new MeshData
        {
            Name = name,
            Vertices = vertices.ToArray(),
            Indices = indices.ToArray(),
            MaterialOpacity = opacity
        };
    }

    private static WallPlane? FindWall(IEnumerable<Level.TriangleCollider> triangles, Vector3 normal)
    {
        var faces = triangles
            .Where(triangle => MathF.Abs(Vector3.Dot(triangle.Normal, normal)) >= 0.95f)
            .Select(triangle => (
                Triangle: triangle,
                Depth: Vector3.Dot((triangle.A + triangle.B + triangle.C) / 3f, normal)))
            .OrderBy(face => face.Depth);

        // Треугольники почти на одной глубине образуют одну плоскость стены.
        var planes = new List<WallPlane>();
        foreach (var face in faces)
        {
            if (planes.Count == 0 || face.Depth - planes[^1].Depth > PlaneTolerance)
                planes.Add(new WallPlane(face.Depth));
            planes[^1].Add(face.Triangle);
        }

        if (planes.Count == 0)
            return null;

        // Сторона задаёт внешнюю половину узла. Площадь отличает стену от наличников и карниза.
        float middle = (planes[0].Depth + planes[^1].Depth) * 0.5f;
        return planes.Where(plane => plane.Depth >= middle)
            .OrderByDescending(plane => plane.Area)
            .ThenByDescending(plane => plane.Depth)
            .First();
    }

    private static WallFrame GetFrame(WallPlane wall, Vector3 normal, Vector2 position)
    {
        Vector3 right = Vector3.Cross(Vector3.Up, normal);
        float left = float.PositiveInfinity;
        float bottom = float.PositiveInfinity;
        foreach (Level.TriangleCollider triangle in wall.Triangles)
        {
            left = MathF.Min(left, MathF.Min(Vector3.Dot(triangle.A, right),
                MathF.Min(Vector3.Dot(triangle.B, right), Vector3.Dot(triangle.C, right))));
            bottom = MathF.Min(bottom, triangle.Bounds.Min.Y);
        }

        Vector3 center = normal * wall.Depth +
            right * (left + position.X) + Vector3.Up * (bottom + position.Y);
        return new WallFrame(center, normal, right);
    }

    private static List<Vector3> ClipTriangle(Level.TriangleCollider triangle, WallFrame frame, Vector2 half)
    {
        List<Vector3> polygon = [frame.ToLocal(triangle.A), frame.ToLocal(triangle.B), frame.ToLocal(triangle.C)];
        polygon = Clip(polygon, Vector3.Right, half.X);
        polygon = Clip(polygon, Vector3.Left, half.X);
        polygon = Clip(polygon, Vector3.Up, half.Y);
        return Clip(polygon, Vector3.Down, half.Y);
    }

    private static List<Vector3> Clip(List<Vector3> polygon, Vector3 axis, float limit)
    {
        if (polygon.Count == 0)
            return polygon;

        var result = new List<Vector3>(polygon.Count + 1);
        Vector3 previous = polygon[^1];
        float previousDistance = limit - Vector3.Dot(previous, axis);
        foreach (Vector3 current in polygon)
        {
            float distance = limit - Vector3.Dot(current, axis);
            if ((distance >= 0f) != (previousDistance >= 0f))
                result.Add(Vector3.Lerp(previous, current, previousDistance / (previousDistance - distance)));
            if (distance >= 0f)
                result.Add(current);
            previous = current;
            previousDistance = distance;
        }
        return result;
    }

    private static void AppendPolygon(List<Vector3> polygon, WallFrame frame, Vector2 size,
        List<VertexData> vertices, List<int> indices)
    {
        if (polygon.Count < 3)
            return;

        int first = vertices.Count;
        foreach (Vector3 point in polygon)
        {
            Vector3 world = frame.ToWorld(point) + frame.Normal * SurfaceOffset;
            var uv = new NumericsVector2(point.X / size.X + 0.5f, 0.5f - point.Y / size.Y);
            vertices.Add(new VertexData(ToNumerics(world), ToNumerics(frame.Normal), uv,
                0, 0, 0, 0, System.Numerics.Vector4.UnitX));
        }
        for (int i = 1; i + 1 < polygon.Count; i++)
        {
            indices.Add(first);
            indices.Add(first + i);
            indices.Add(first + i + 1);
        }
    }

    private static NumericsVector3 ToNumerics(Vector3 value) => new(value.X, value.Y, value.Z);

    private readonly record struct WallFrame(Vector3 Center, Vector3 Normal, Vector3 Right)
    {
        public Vector3 ToLocal(Vector3 point)
        {
            Vector3 relative = point - Center;
            return new Vector3(Vector3.Dot(relative, Right), relative.Y, Vector3.Dot(relative, Normal));
        }

        public Vector3 ToWorld(Vector3 point) => Center + point.X * Right + point.Y * Vector3.Up + point.Z * Normal;
    }

    private sealed class WallPlane(float depth)
    {
        public float Depth { get; } = depth;
        public float Area { get; private set; }
        public List<Level.TriangleCollider> Triangles { get; } = [];

        public void Add(Level.TriangleCollider triangle)
        {
            Triangles.Add(triangle);
            Area += Vector3.Cross(triangle.B - triangle.A, triangle.C - triangle.A).Length() * 0.5f;
        }
    }
}
```

Фиттер группирует треугольники узла по глубине вдоль выбранной оси. Из половины узла со стороны фасада он выбирает плоскость с наибольшей суммарной площадью. Это позволяет найти широкую стену магазина рядом с выступающим карнизом. У сложного здания с несколькими отдельными фасадами на одной стороне лучше дать нужной стене своё имя узла, чтобы выбор области был однозначным.

Далее фиттер находит минимальные координаты вдоль стены и по высоте, прибавляет `x` и `y`, обрезает треугольники по рамке картинки и вычисляет UV. Проёмы и края сохраняются, потому что меш строится только из участков стены. Отступ `0.003` по нормали уменьшает мерцание при рисовании поверх стены.

### Шаг 2. Замени `Levels/LevelDecals.cs`

Этот класс читает JSON уровня, находит меши нужных узлов и передаёт их фиттеру. Текстура из `Content/Decals` загружается один раз на файл и режим фона. `whiteBackground: true` превращает яркость чёрно-белого PNG в прозрачность, сохраняя чёрный рисунок.

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using _3DLight.Collision;
using Ars3D.ModelFormat;
using Ars3D.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace _3DLight;

internal sealed class LevelDecals : IDisposable
{
    private readonly List<CompiledModel> models = [];
    private readonly Dictionary<(string File, bool WhiteBackground), Texture2D> textures = [];

    public static LevelDecals? Load(GraphicsDevice graphics, Effect effect, string levelName,
        ModelData data, ModelHierarchy hierarchy)
    {
        string content = Path.Combine(AppContext.BaseDirectory, "Content");
        string path = Path.Combine(content, "Levels", levelName + ".decals.json");
        if (!File.Exists(path))
            return null;

        DecalDefinition[] entries = ReadDefinitions(path);
        ILookup<string, Level.TriangleCollider> receivers = FindReceivers(entries, data, hierarchy);
        string textureDirectory = Path.Combine(content, "Decals");
        var decals = new LevelDecals();
        try
        {
            foreach (DecalDefinition entry in entries)
                decals.AddDecal(graphics, effect, textureDirectory, entry, receivers[entry.Wall]);
            return decals;
        }
        catch
        {
            decals.Dispose();
            throw;
        }
    }

    private void AddDecal(GraphicsDevice graphics, Effect effect, string textureDirectory,
        DecalDefinition entry, IEnumerable<Level.TriangleCollider> triangles)
    {
        MeshData? mesh = DecalFitter.Fit(triangles, entry.WallNormal,
            new Vector2(entry.X, entry.Y), new Vector2(entry.Size[0], entry.Size[1]),
            entry.Texture, entry.Opacity);
        if (mesh is null)
        {
            Console.WriteLine($"Декаль '{entry.Texture}': стена '{entry.Wall}' ({entry.Side}), x={entry.X}, y={entry.Y}: поверхность не найдена.");
            return;
        }

        Texture2D texture = GetTexture(graphics, textureDirectory, entry);
        models.Add(ModelAssetLoader.CreateDecal(graphics, mesh, texture, effect));
    }

    private static DecalDefinition[] ReadDefinitions(string path)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };
        var entries = JsonSerializer.Deserialize<DecalDefinition[]>(File.ReadAllText(path), options)
            ?? throw new InvalidDataException($"Пустая конфигурация декалей: {path}");

        foreach (DecalDefinition entry in entries)
        {
            if (entry is null)
                throw new InvalidDataException($"Пустая запись в {path}.");
            entry.Validate();
        }
        return entries;
    }

    private static ILookup<string, Level.TriangleCollider> FindReceivers(
        DecalDefinition[] entries, ModelData data, ModelHierarchy hierarchy)
    {
        var nodes = entries.Select(entry => entry.Wall).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var meshIds = Enumerable.Range(0, data.Meshes.Count)
            .Where(index => nodes.Contains(data.Nodes[data.Meshes[index].Node].Name)).ToHashSet();
        // Берём треугольники указанных узлов в исходной позе уровня.
        return LevelGeometryBuilder.BuildTriangleColliders(data, hierarchy.BindPoseGlobalTransforms, meshIds)
            .ToLookup(triangle => triangle.Name, StringComparer.OrdinalIgnoreCase);
    }

    private Texture2D GetTexture(GraphicsDevice graphics, string folder, DecalDefinition entry)
    {
        var key = (entry.Texture, entry.WhiteBackground);
        if (textures.TryGetValue(key, out Texture2D? texture))
            return texture;

        using FileStream stream = File.OpenRead(Path.Combine(folder, entry.Texture));
        texture = Texture2D.FromStream(graphics, stream);
        try
        {
            if (entry.WhiteBackground)
                ApplyInkMask(texture);
            textures.Add(key, texture);
            return texture;
        }
        catch
        {
            texture.Dispose();
            throw;
        }
    }

    private static void ApplyInkMask(Texture2D texture)
    {
        // Для одноцветного рисунка яркость фона превращаем в прозрачность.
        var pixels = new Color[texture.Width * texture.Height];
        texture.GetData(pixels);
        for (int i = 0; i < pixels.Length; i++)
        {
            Color source = pixels[i];
            int brightness = (source.R * 54 + source.G * 183 + source.B * 19) / 256;
            pixels[i] = new Color(0, 0, 0, (255 - brightness) * source.A / 255);
        }
        texture.SetData(pixels);
    }

    public void Draw(Matrix view, Matrix projection, SceneLighting lighting, SceneRenderQueue? queue)
    {
        foreach (CompiledModel model in models)
            model.Draw(Matrix.Identity, view, projection, lighting, queue);
    }

    public void Dispose()
    {
        // Модели ссылаются на общие текстуры, поэтому освобождаем текстуры после моделей.
        foreach (CompiledModel model in models)
            model.Dispose();
        foreach (Texture2D texture in textures.Values)
            texture.Dispose();
    }

    private sealed class DecalDefinition
    {
        public string Wall { get; init; } = "";
        public string Side { get; init; } = "";
        public string Texture { get; init; } = "";
        public float X { get; init; } = float.NaN;
        public float Y { get; init; } = float.NaN;
        public float[] Size { get; init; } = [];
        public float Opacity { get; init; } = 1f;
        public bool WhiteBackground { get; init; }

        public Vector3 WallNormal => Side switch
        {
            "+x" => Vector3.UnitX,
            "-x" => -Vector3.UnitX,
            "+z" => Vector3.UnitZ,
            "-z" => -Vector3.UnitZ,
            _ => Vector3.Zero
        };

        public void Validate()
        {
            bool validTexture = !string.IsNullOrWhiteSpace(Texture) && Path.GetFileName(Texture) == Texture &&
                Texture.EndsWith(".png", StringComparison.OrdinalIgnoreCase);
            bool validPlacement = float.IsFinite(X) && X >= 0f && float.IsFinite(Y) && Y >= 0f &&
                Size is { Length: 2 } && Size.All(value => float.IsFinite(value) && value > 0f);

            if (string.IsNullOrWhiteSpace(Wall) || WallNormal == Vector3.Zero ||
                !validTexture || !validPlacement || Opacity is not (>= 0f and <= 1f))
            {
                throw new InvalidDataException($"Некорректная декаль '{Texture}' для стены '{Wall}' ({Side}).");
            }
        }
    }
}
```

Уровень вызывает загрузку при замене модели. Если эта связка уже есть, переходи к JSON. Если её ещё нет, добавь в `Level` поле и вызов в `ReplaceModel`. Ниже полный метод замены. При выгрузке уровня вызывай `decals?.Dispose()` и обнуляй поле. В конце `Level.Draw`, после отрисовки модели и дверей, поставь вызов отрисовки декалей.

```csharp
private LevelDecals? decals;

        private void ReplaceModel(CompiledModel replacement, IReadOnlyList<DoorDefinition> doors,
            GraphicsDevice graphics, Effect toonEffect, string modelName)
        {
            LevelDecals? replacementDecals = null;
            try
            {
                // Строим декали на новой геометрии до замены текущей модели уровня.
                replacementDecals = LevelDecals.Load(graphics, toonEffect, modelName,
                    replacement.Data, replacement.Hierarchy);
                InitializeGeometry(replacement.Data, replacement.Hierarchy, doors);
            }
            catch
            {
                replacementDecals?.Dispose();
                replacement.Dispose();
                throw;
            }

            CompiledModel? previousModel = model;
            model = replacement;
            decals?.Dispose();
            decals = replacementDecals;
            previousModel?.Dispose();
        }
```

```csharp
decals?.Draw(view, projection, lighting, renderQueue);
```

Для проекта с уже настроенным прозрачным проходом метод создания меша декали в `ModelAssetLoader` выглядит так.

```csharp
    public static CompiledModel CreateDecal(GraphicsDevice graphicsDevice, MeshData mesh,
        Texture2D texture, Effect toonEffect)
    {
        var data = new ModelData();
        data.Nodes.Add(new NodeData(mesh.Name, -1, System.Numerics.Matrix4x4.Identity));
        data.Meshes.Add(mesh);
        return new CompiledModel(graphicsDevice, data, [texture], [], toonEffect, alphaBlend: true);
    }
```

### Шаг 3. Укажи две стены в JSON

Положи изображение в `Content/Decals/sigil.png`. Замени `Content/Levels/gas_station.decals.json` этим содержимым.

```json
[
  {
    "wall": "6twelve",
    "side": "-x",
    "texture": "sigil.png",
    "x": 11.674,
    "y": 1.625,
    "size": [1.25, 1.62],
    "whiteBackground": true
  },
  {
    "wall": "6twelve",
    "side": "+x",
    "texture": "sigil.png",
    "x": 11.612,
    "y": 1.625,
    "size": [1.25, 1.62],
    "whiteBackground": true
  }
]
```

Убедись, что в `3DLight.csproj` в `ItemGroup` есть копирование JSON и PNG в выходную папку. Пересобери проект и перезагрузи сцену.

```xml
<None Update="Content\Levels\*.json" CopyToOutputDirectory="PreserveNewest" />
<None Update="Content\Decals\*.png" CopyToOutputDirectory="PreserveNewest" />
```

Для перемещения сигиля меняй `x` и `y`. Для переноса на другую сторону меняй `side` и подбирай отступы по её собственным краям. Если имя узла ошибочное или прямоугольник полностью вышел за стену, при загрузке в консоли будет указано, какую запись не удалось разместить.
