# Разбор `Tools/ModelCompiler/Program.cs`

Это отдельное консольное приложение. Только оно зависит от Assimp и читает FBX.

## 1. Точка входа

Программа требует путь к `models.json`, вычисляет его абсолютную папку, читает `ModelManifest` и вызывает `CompileManifestEntry` для каждой модели.

## 2. `ReadManifest`

JSON читается и десериализуется без учёта регистра имён полей. `ModelEntry` содержит `Name`, `Source`, `Output` и необязательный словарь `Clips`.

## 3. `CompileManifestEntry`

Относительные source/output paths переводятся в абсолютные. `CompileModel` создаёт `ModelData`, а `ModelDataIo.Write` сохраняет его.

## 4. `CompileModel`

Порядок:

1. Создать `AssimpContext`.
2. Импортировать основной FBX.
3. Выбрать matrix layout.
4. Построить дерево узлов.
5. Найти владельцев мешей.
6. Конвертировать меши, вершины и кости.
7. Импортировать animation clips.
8. Вернуть `ModelData`.

## 5. Import flags

- `Triangulate` создаёт треугольники.
- `JoinIdenticalVertices` объединяет одинаковые вершины.
- `GenerateSmoothNormals` создаёт нормали.
- `FlipUVs` согласует UV.
- `LimitBoneWeights` ограничивает влияния.
- `FlipWindingOrder` согласует winding с MonoGame.

## 6. `AddNodeHierarchy`

Метод рекурсивно проходит `Scene.RootNode`. Узлу назначается индекс, сохраняются имя, parent index и bind transform.

Имена проверяются на уникальность, иначе animation channel нельзя однозначно связать с узлом.

## 7. `FindMeshOwnerNodes`

Assimp хранит меши отдельно от дерева. Метод создаёт массив соответствий `mesh index → owner node index`.

## 8. `ConvertMesh`

Проверяется лимит 72 костей, создаются списки влияний, конвертируются кости и вершины, читаются triangle indices, формируется `MeshData`.

## 9. `ConvertBones`

Для каждой кости ищется node index и сохраняется offset matrix. Каждый `VertexWeight` добавляется к нужной вершине.

## 10. `ConvertVertices`

Влияния сортируются по весу. Берутся четыре сильнейших и нормализуются до суммы 1.

Читаются position, normal и UV. При отсутствии normal используется `Vector3.UnitY`, при отсутствии UV — `Vector3.Zero`.

## 11. Bone helpers

`GetBoneIndex` и `GetBoneWeight` безопасно возвращают индекс и нормализованный вес. Для вершины без костей первая identity-кость получает вес 1.

## 12. `AddAnimationClips`

Метод проходит `clips` из manifest. Если clip path совпадает с source, сцена переиспользуется; остальные FBX импортируются отдельно.

## 13. `ConvertClip`

Берётся первая Assimp animation. Сохраняются name, duration и ticks per second.

`NodeAnimationChannel` связывается с node index по имени. Position/scaling keys превращаются в `VectorKey`, rotation keys — в `QuaternionKey`.

## 14. `ChooseMatrixLayout`

Проверяются direct и transposed matrices. `CalculateBindPoseError` строит skin matrices в bind pose; правильный вариант ближе к identity.

Для текущего статического Blender-уровня используется transposed layout.

## 15. `CalculateIdentityError`

Суммируются отклонения диагонали от 1 и остальных элементов от 0. Меньшая ошибка означает более подходящий layout.

## 16. Результат

Компилятор печатает число meshes, nodes, clips и выбранный layout, затем сохраняет `.3dmodel`.

## 17. Новая модель

Добавьте запись в `Assets/models.json`, положите FBX по указанному пути и выполните `dotnet build`.

## 18. Новый клип

Добавьте FBX в `clips` нужной модели и такое же логическое имя в словарь игрового персонажа. Скелет и имена костей должны совпадать.

## 19. Типичные ошибки

- `Assimp не смог импортировать` — неверный путь или повреждённый FBX.
- `Кость отсутствует в иерархии` — несовместимый mesh/skeleton.
- `максимум 72` — слишком много костей в одном меше.
- `нет платформ` — имена объектов уровня не начинаются с `Ground/Bridge/Trail/Cylinder` и не содержат `Platform`.
