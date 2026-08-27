"""Hot reload уровня Ars3D после сохранения .blend.

Запуск:
1. Blender -> Scripting -> Open -> этот файл.
2. Нажать Run Script один раз.
3. Запустить игру.
4. После каждого Ctrl+S Blender создаст новую версию уровня.

Старые FBX и .3dmodel не удаляются и не перезаписываются.
"""

import json
import os
import subprocess
import time
from datetime import datetime, timezone
from pathlib import Path

import bpy
from bpy.app.handlers import persistent


# Настройки
PROJECT_FOLDER = Path(r"C:\CsharpProjects\Ars3D")
FBX_HISTORY_FOLDER = PROJECT_FOLDER / "Assets" / "LevelVersions"
MODEL_HISTORY_FOLDER = PROJECT_FOLDER / "Content" / "Models" / "LevelVersions"
LATEST_POINTER = MODEL_HISTORY_FOLDER / "latest.json"
MODEL_COMPILER_PROJECT = PROJECT_FOLDER / "Tools" / "ModelCompiler" / "ModelCompiler.csproj"

HANDLER_MARKER = "ars3d_level_hot_reload_handler"
IGNORED_MESH_NAMES = {"__Ars3D_LevelBounds"}
hot_reload_is_running = False


def make_version_name():
    """Например: 20260827_173512_042Z."""
    now = datetime.now(timezone.utc)
    milliseconds = now.microsecond // 1000
    return now.strftime("%Y%m%d_%H%M%S_") + f"{milliseconds:03d}Z"


def get_export_objects():
    """Экспортируем видимые меши и Empty из текущего View Layer."""
    return [
        obj
        for obj in bpy.context.view_layer.objects
        if obj.type in {"MESH", "EMPTY"}
        and obj.name not in IGNORED_MESH_NAMES
        and obj.visible_get()
    ]


def export_versioned_fbx(target_file):
    """Экспортирует сцену, после чего восстанавливает выбор пользователя."""
    export_objects = get_export_objects()

    if not any(obj.type == "MESH" for obj in export_objects):
        raise RuntimeError("В сцене нет видимых Mesh-объектов")

    selected_before = list(bpy.context.selected_objects)
    active_before = bpy.context.view_layer.objects.active
    mode_before = active_before.mode if active_before else "OBJECT"

    try:
        if active_before and mode_before != "OBJECT":
            bpy.ops.object.mode_set(mode="OBJECT")

        bpy.ops.object.select_all(action="DESELECT")

        for obj in export_objects:
            obj.select_set(True)

        bpy.context.view_layer.objects.active = export_objects[0]

        bpy.ops.export_scene.fbx(
            filepath=str(target_file),
            use_selection=True,
            object_types={"MESH", "EMPTY"},
            use_mesh_modifiers=True,
            bake_anim=False,
            add_leaf_bones=False,
            apply_unit_scale=True,
            bake_space_transform=True,
            axis_forward="-Z",
            axis_up="Y",
        )
    finally:
        bpy.ops.object.select_all(action="DESELECT")

        for obj in selected_before:
            if obj.name in bpy.context.view_layer.objects:
                obj.select_set(True)

        if active_before and active_before.name in bpy.context.view_layer.objects:
            bpy.context.view_layer.objects.active = active_before

            if mode_before != "OBJECT":
                bpy.ops.object.mode_set(mode=mode_before)


def compile_level(fbx_file, model_file):
    """Компилирует только одну новую версию уровня."""
    command = [
        "dotnet",
        "run",
        "--project",
        str(MODEL_COMPILER_PROJECT),
        "--",
        "--single",
        str(fbx_file),
        str(model_file),
    ]

    creation_flags = subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0
    result = subprocess.run(
        command,
        cwd=str(PROJECT_FOLDER),
        capture_output=True,
        text=True,
        errors="replace",
        creationflags=creation_flags,
    )

    if result.stdout:
        print(result.stdout)

    if result.returncode != 0:
        raise RuntimeError(
            "ModelCompiler завершился с ошибкой:\n" + result.stderr
        )


def publish_latest_version(version, fbx_file, model_file):
    """Атомарно сообщает игре, что модель полностью готова."""
    pointer_data = {
        "version": version,
        "modelFile": model_file.name,
        "sourceFile": fbx_file.name,
        "createdUtc": datetime.now(timezone.utc).isoformat(),
    }
    temporary_pointer = LATEST_POINTER.with_suffix(".json.tmp")
    temporary_pointer.write_text(
        json.dumps(pointer_data, ensure_ascii=False, indent=2),
        encoding="utf-8",
    )

    # File.ReadAllText в игре держит файл лишь доли миллисекунды, но Windows
    # иногда успевает заблокировать замену. Несколько коротких повторов решают
    # эту гонку, не публикуя частично записанный JSON.
    for attempt in range(10):
        try:
            os.replace(temporary_pointer, LATEST_POINTER)
            return
        except PermissionError:
            if attempt == 9:
                raise

            time.sleep(0.05)


def build_new_level_version():
    """Полный цикл: FBX -> .3dmodel -> latest.json."""
    FBX_HISTORY_FOLDER.mkdir(parents=True, exist_ok=True)
    MODEL_HISTORY_FOLDER.mkdir(parents=True, exist_ok=True)

    version = make_version_name()
    fbx_file = FBX_HISTORY_FOLDER / f"LevelOne_{version}.fbx"
    model_file = MODEL_HISTORY_FOLDER / f"level_one_{version}.3dmodel"

    print(f"Ars3D hot reload: exporting {fbx_file.name}...")
    export_versioned_fbx(fbx_file)

    print(f"Ars3D hot reload: compiling {model_file.name}...")
    compile_level(fbx_file, model_file)

    publish_latest_version(version, fbx_file, model_file)
    print(f"Ars3D hot reload ready: {version}")


@persistent
def ars3d_export_on_save(_blend_file):
    """Blender вызывает обработчик после Ctrl+S."""
    global hot_reload_is_running

    if hot_reload_is_running:
        return

    hot_reload_is_running = True

    try:
        build_new_level_version()
    except Exception as error:
        print(f"Ars3D hot reload failed: {error}")
    finally:
        hot_reload_is_running = False


def register():
    """Добавляет один обработчик, даже если Run Script нажали повторно."""
    for handler in list(bpy.app.handlers.save_post):
        if getattr(handler, "ars3d_marker", None) == HANDLER_MARKER:
            bpy.app.handlers.save_post.remove(handler)

    ars3d_export_on_save.ars3d_marker = HANDLER_MARKER
    bpy.app.handlers.save_post.append(ars3d_export_on_save)
    print("Ars3D hot reload enabled. Save the .blend file to create a version.")


register()
