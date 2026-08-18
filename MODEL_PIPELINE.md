# Модели Ars3D

## Схема

`FBX -> Tools/ModelCompiler (Assimp) -> .3dmodel -> CompiledModel -> GPU`

Игра не читает FBX. Перед каждой сборкой отдельный компилятор создаёт:

- `level.3dmodel` — уровень и геометрия платформ;
- `player.3dmodel` — Rogue и все его клипы;
- `skeleton.3dmodel` — Skeleton и все его клипы.

MGCB собирает только текстуры и шрифт. В runtime нет зависимости Assimp.

## Новая статическая модель

Добавьте FBX и запись в `Assets/models.json`:

```json
{
  "name": "crate",
  "source": "../Content/Assets/Props/crate.fbx",
  "output": "../Content/Models/crate.3dmodel"
}
```

После `dotnet build` загрузите её через:

```csharp
CompiledModel model = CompiledModel.Load(graphicsDevice, "crate", texture);
model.Draw(world, view, projection);
```

## Новый персонаж

`source` должен содержать основной меш и эталонный скелет. В `clips` указываются FBX того же скелета:

```json
{
  "name": "enemy",
  "source": "../Content/Assets/Enemy/EnemyTPose.fbx",
  "output": "../Content/Models/enemy.3dmodel",
  "clips": {
    "Idle": "../Content/Assets/Enemy/EnemyIdle.fbx",
    "Run": "../Content/Assets/Enemy/EnemyRun.fbx"
  }
}
```

Имена костей должны совпадать. Лимиты MonoGame `SkinnedEffect`: 72 кости на меш и четыре веса на вершину.

## Новый клип Rogue или Skeleton

1. Положите FBX в папку персонажа.
2. Добавьте клип в `clips` соответствующей модели в `Assets/models.json`.
3. Добавьте такое же имя в словарь `Player.cs` или `Skeleton.cs`.
4. Выполните `dotnet build`.
5. В логике вызовите `Play("Имя", loop, deltaTime)`.

Ручная компиляция без полной сборки:

```powershell
dotnet run --project Tools\ModelCompiler\ModelCompiler.csproj -- Assets\models.json
```
