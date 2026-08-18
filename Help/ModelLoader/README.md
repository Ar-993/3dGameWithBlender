# Документация загрузчика моделей Ars3D

Загрузчик разделён на три крупные части:

1. [`ModelCompiler.md`](ModelCompiler.md) — как Assimp читает FBX и превращает его в данные игры.
2. [`CompiledModelData.md`](CompiledModelData.md) — как устроен бинарный формат `.3dmodel`.
3. [`CompiledModel.md`](CompiledModel.md) — как игра загружает, анимирует и рисует модель.
4. [`AddingModelsAndAnimations.md`](AddingModelsAndAnimations.md) — практическая инструкция по добавлению моделей и клипов.
5. [`AddingModelsLineByLine.md`](AddingModelsLineByLine.md) — учебный пример с построчным разбором кода.
6. [`LoaderClassesLineByLine.md`](LoaderClassesLineByLine.md) — подробный разбор всех классов загрузчика и связей между ними.

Общий путь данных:

```text
Assets/models.json
        ↓
Tools/ModelCompiler/Program.cs
        ↓ Assimp
Content/Models/*.3dmodel
        ↓
CompiledModelData.cs
        ↓
CompiledModel.cs
        ↓
VertexBuffer + IndexBuffer + ToonShader
        ↓
GPU
```

## Команды

Обычная сборка автоматически пересобирает модели:

```powershell
dotnet build
```

Только компиляция моделей:

```powershell
dotnet run --project Tools\ModelCompiler\ModelCompiler.csproj -- Assets\models.json
```

Готовые файлы создаются в `Content/Models` и копируются в выходную папку игры.

## Статическая модель

```json
{
  "name": "crate",
  "source": "../Content/Assets/Props/Crate.fbx",
  "output": "../Content/Models/crate.3dmodel"
}
```

## Анимированная модель

```json
{
  "name": "orc",
  "source": "../Content/Assets/Orc/OrcTPose.fbx",
  "output": "../Content/Models/orc.3dmodel",
  "clips": {
    "Idle": "../Content/Assets/Orc/OrcIdle.fbx",
    "Run": "../Content/Assets/Orc/OrcRun.fbx"
  }
}
```

`source` содержит основной меш, bind pose и эталонный скелет. `clips` содержит дополнительные анимации того же скелета.

## Ограничения

- Максимум 72 кости на один меш.
- Максимум четыре влияния костей на вершину.
- Имена костей во всех клипах должны совпадать с основным скелетом.
- Текстуры добавляются через MGCB.
- Runtime игры не читает FBX и не зависит от Assimp.
