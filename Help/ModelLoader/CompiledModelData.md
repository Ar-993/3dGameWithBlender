# Разбор `CompiledModelData.cs`

Файл описывает содержимое `.3dmodel` и симметричные операции записи и чтения.

## 1. Заголовок

```csharp
public const uint Magic = 0x4D4C4433;
public const int Version = 1;
```

`Magic` отличает модель от случайного файла. `Version` позволяет отклонить несовместимый формат. `MaximumArrayLength` защищает от повреждённого файла с невозможным размером массива.

## 2. `ModelData`

Корневой объект содержит `Nodes`, `Meshes` и `Clips`. В этом же порядке секции записываются в бинарник.

## 3. `NodeData`

```csharp
NodeData(string Name, int Parent, Matrix4x4 Bind)
```

- `Name` нужен для диагностики и платформ.
- `Parent` — индекс родителя, `-1` означает root.
- `Bind` — локальная матрица исходной позы.

## 4. `BoneData`

`Node` связывает кость с узлом. `Offset` хранит inverse bind matrix.

## 5. `VertexData`

Вершина хранит position, normal, UV, четыре индекса костей и четыре веса. Индексы — байты, потому что лимит меньше 256 костей.

## 6. `MeshData`

```text
Name
Node
Vertices
Indices
Bones
```

Пустой массив `Bones` означает статический меш.

## 7. Анимационные типы

- `VectorKey` — время и `Vector3` позиции/масштаба.
- `QuaternionKey` — время и quaternion вращения.
- `ChannelData` — ключи одного узла.
- `ClipData` — имя, длительность, ticks per second и каналы.

## 8. `Write`

Метод создаёт папку и файл, затем записывает:

1. Magic.
2. Version.
3. Nodes.
4. Meshes.
5. Clips.

`BinaryWriter` создаёт компактный файл без JSON-разметки.

## 9. `Read`

Создаётся `BinaryReader`, проверяется заголовок и читаются Nodes, Meshes, Clips в том же порядке.

Главное правило: изменение порядка в `Write` требует такого же изменения в `Read` и увеличения `Version`.

## 10. Узлы

`WriteNodes` записывает количество, имя, parent index и 16 компонентов bind matrix. `ReadNodes` выполняет обратную операцию.

## 11. Меши

Для каждого меша записываются имя, node index, вершины, индексы и кости. Код разделён на `WriteVertices`, `WriteIndices`, `WriteBones` и парные методы чтения.

## 12. Порядок полей вершины

```text
Position X Y Z
Normal X Y Z
UV X Y
Bone0 Bone1 Bone2 Bone3
Weight X Y Z W
```

`ReadVertices` читает значения строго в этом порядке.

## 13. Индексы

Сначала записывается длина, затем каждый `Int32`. Каждые три индекса образуют треугольник.

## 14. Кости

Для кости записываются node index и offset matrix. Имя не дублируется: оно доступно через `Nodes[nodeIndex]`.

## 15. Клипы

Для клипа записываются name, duration, ticks per second и channel count. Для канала — node index, position keys, rotation keys и scale keys.

## 16. Ключи

Векторный ключ содержит `double Time` и три `float`. Quaternion-ключ содержит `double Time` и четыре `float`.

`double` сохраняет точность времени FBX.

## 17. Защита размеров

`ReadArrayLength` отклоняет отрицательный или чрезмерный размер. Это предотвращает выделение огромной памяти при повреждённом файле.

## 18. Матрицы

`WriteMatrix` записывает `M11`–`M44`. `ReadMatrix` читает те же 16 чисел в том же порядке.

## 19. Изменение формата

Чтобы добавить материал или bounds:

1. Добавить поле в data structure.
2. Записать его в `Write`.
3. Прочитать в том же месте в `Read`.
4. Увеличить `Version`.
5. Пересобрать все `.3dmodel`.
