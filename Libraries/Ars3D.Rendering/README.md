# Ars3D.Rendering

Библиотека рендера MonoGame DesktopGL. Зависит от MonoGame и `Shared/ModelFormat`;
не ссылается на игру, `Ars3D.Items` или Assimp.

## Карта кода

| Каталог | Ответственность |
| --- | --- |
| `Models/CompiledModel.cs` | GPU-буферы, эффекты мешей, skinning, проходы отрисовки и освобождение ресурсов. |
| `Models/ModelAssetLoader.cs` | Чтение `.3dmodel` из файла или массива байтов, загрузка текстур и создание модели. |
| `Models/ModelHierarchy.cs` | Иерархия узлов, преобразования и поиск маркеров. |
| `Animation/` | Поза узла `BonePose` и выборка клипов через `AnimationSampler`. |
| `Lighting/` | Настройки света и тумана, передача параметров в эффект. |
| `Materials/` | Существующие правила отсечения альфы растительности. |

## Загрузка и отрисовка

```csharp
using Ars3D.Rendering;

using CompiledModel model = ModelAssetLoader.Load(
    graphicsDevice,
    modelPath: modelFile,
    texture: fallbackTexture,
    toonEffect: effect,
    textureDirectory: materialTextureDirectory);

model.Draw(world, view, projection, lighting);
```

Игра передаёт путь к модели, каталог текстур и готовый `Effect`. Библиотека не
задаёт структуру каталогов приложения и не создаёт игровые объекты. Для hot reload
есть `LoadFromBytes`; для анимированной модели — `DrawPose`.

`CompiledModel` освобождает созданные GPU-буферы, копии эффектов и загруженные
материальные текстуры. Переданные fallback-текстура и исходный эффект остаются
у вызывающего кода. При неудачной загрузке созданные ресурсы освобождаются.

Игровые адаптеры `GameplayRenderer`, `ProjectileRenderer` и `ItemModelRenderer`
определяют, какие объекты рисовать и с какими преобразованиями. Они находятся
в игре и используют публичный API библиотеки.

## Сборка

```powershell
dotnet build Libraries/Ars3D.Rendering/Ars3D.Rendering.csproj
```

Сборка библиотеки не запускает MGCB и компилятор FBX. Игра собирает свои шейдеры
через MGCB и передаёт полученные эффекты в загрузчик.
