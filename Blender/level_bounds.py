"""Размеры и координаты уровня Ars3D прямо в 3D Viewport.

Blender -> Scripting -> Open -> этот файл -> Run Script.
"""

import blf
import bpy
import gpu
from bpy_extras.view3d_utils import location_3d_to_region_2d
from gpu_extras.batch import batch_for_shader
from mathutils import Vector


OVERLAY_HANDLER_KEY = "ars3d_level_overlay"
LINE_SHADER = gpu.shader.from_builtin("UNIFORM_COLOR")
IGNORED_MESH_NAMES = {"__Ars3D_LevelBounds"}

# Рёбра прямоугольника. Числа — индексы углов из make_corners().
BOX_EDGES = (
    (0, 1), (1, 3), (3, 2), (2, 0),
    (4, 5), (5, 7), (7, 6), (6, 4),
    (0, 4), (1, 5), (2, 6), (3, 7),
)


def get_level_meshes():
    """Все видимые меши считаются частью уровня."""
    return [
        obj
        for obj in bpy.context.scene.objects
        if obj.type == "MESH"
        and obj.name not in IGNORED_MESH_NAMES
        and obj.visible_get()
    ]


def calculate_bounds(meshes):
    """Находим минимальные и максимальные мировые координаты уровня."""
    if not meshes:
        raise RuntimeError("В сцене нет видимых Mesh-объектов")

    minimum = Vector((float("inf"), float("inf"), float("inf")))
    maximum = Vector((float("-inf"), float("-inf"), float("-inf")))
    depsgraph = bpy.context.evaluated_depsgraph_get()

    for obj in meshes:
        evaluated_object = obj.evaluated_get(depsgraph)

        for local_corner in evaluated_object.bound_box:
            corner = evaluated_object.matrix_world @ Vector(local_corner)

            minimum.x = min(minimum.x, corner.x)
            minimum.y = min(minimum.y, corner.y)
            minimum.z = min(minimum.z, corner.z)

            maximum.x = max(maximum.x, corner.x)
            maximum.y = max(maximum.y, corner.y)
            maximum.z = max(maximum.z, corner.z)

    return minimum, maximum


def make_corners(minimum, maximum):
    """Создаём восемь углов общего bounding box."""
    x0, y0, z0 = minimum
    x1, y1, z1 = maximum

    return [
        Vector((x0, y0, z0)),
        Vector((x1, y0, z0)),
        Vector((x0, y1, z0)),
        Vector((x1, y1, z0)),
        Vector((x0, y0, z1)),
        Vector((x1, y0, z1)),
        Vector((x0, y1, z1)),
        Vector((x1, y1, z1)),
    ]


def to_monogame(blender_position):
    """Blender (X, Y, Z) -> MonoGame (X, высота Y, глубина Z)."""
    return Vector((
        blender_position.x,
        blender_position.z,
        -blender_position.y,
    ))


def project_to_screen(world_position):
    """Переводит 3D-точку сцены в пиксели текущего окна."""
    return location_3d_to_region_2d(
        bpy.context.region,
        bpy.context.region_data,
        world_position,
    )


def draw_lines(world_points, connections, color, width=1.0):
    """Рисует линии между указанными 3D-точками."""
    screen_points = [project_to_screen(point) for point in world_points]
    line_vertices = []

    for start_index, end_index in connections:
        start = screen_points[start_index]
        end = screen_points[end_index]

        # None означает, что точка сейчас находится за камерой.
        if start is not None and end is not None:
            line_vertices.extend((start, end))

    if not line_vertices:
        return

    batch = batch_for_shader(LINE_SHADER, "LINES", {"pos": line_vertices})
    gpu.state.blend_set("ALPHA")
    gpu.state.line_width_set(width)
    LINE_SHADER.bind()
    LINE_SHADER.uniform_float("color", color)
    batch.draw(LINE_SHADER)
    gpu.state.line_width_set(1.0)
    gpu.state.blend_set("NONE")


def draw_text(x, y, text, color=(1.0, 1.0, 1.0, 1.0), size=13):
    """Рисует читаемый текст в пикселях окна."""
    font = 0
    blf.size(font, size)
    blf.color(font, *color)
    blf.enable(font, blf.SHADOW)
    blf.shadow(font, 3, 0.0, 0.0, 0.0, 0.9)
    blf.shadow_offset(font, 1, -1)
    blf.position(font, x, y, 0)
    blf.draw(font, text)


def draw_text_at(world_position, text, color=(1.0, 1.0, 1.0, 1.0)):
    """Ставит текст рядом с заданной 3D-точкой."""
    screen_position = project_to_screen(world_position)

    if screen_position is not None:
        draw_text(
            screen_position.x + 6,
            screen_position.y + 6,
            text,
            color,
        )


def draw_overlay():
    """Blender вызывает это при каждой перерисовке 3D Viewport."""
    try:
        minimum, maximum = calculate_bounds(get_level_meshes())
    except RuntimeError:
        return

    corners = make_corners(minimum, maximum)
    size = maximum - minimum

    # Оранжевая рамка вокруг всего уровня.
    draw_lines(corners, BOX_EDGES, (1.0, 0.35, 0.05, 0.8), 1.5)

    # Размерные линии: X, игровая высота Y и игровая глубина Z.
    draw_lines(corners, ((0, 1),), (1.0, 0.15, 0.15, 1.0), 3.0)
    draw_lines(corners, ((0, 4),), (0.15, 1.0, 0.25, 1.0), 3.0)
    draw_lines(corners, ((0, 2),), (0.2, 0.55, 1.0, 1.0), 3.0)

    # Координаты MonoGame на каждом из восьми углов.
    for corner in corners:
        game_position = to_monogame(corner)
        draw_text_at(
            corner,
            f"({game_position.x:.1f}, {game_position.y:.1f}, "
            f"{game_position.z:.1f})",
        )

    # Каждая длина подписывается только один раз.
    draw_text_at((corners[0] + corners[1]) / 2, f"X = {size.x:.2f}", (1, 0.3, 0.3, 1))
    draw_text_at((corners[0] + corners[4]) / 2, f"Y = {size.z:.2f}", (0.3, 1, 0.4, 1))
    draw_text_at((corners[0] + corners[2]) / 2, f"Z = {size.y:.2f}", (0.3, 0.65, 1, 1))

    # Общий размер всегда виден в левом верхнем углу.
    draw_text(
        20,
        bpy.context.region.height - 35,
        f"MonoGame level: X={size.x:.2f}  Y={size.z:.2f}  Z={size.y:.2f}",
        (1.0, 0.8, 0.2, 1.0),
        16,
    )


def install_overlay():
    """Включает разметку без дубликатов при повторном запуске."""
    handlers = bpy.app.driver_namespace
    old_handler = handlers.get(OVERLAY_HANDLER_KEY)

    if old_handler is not None:
        try:
            bpy.types.SpaceView3D.draw_handler_remove(old_handler, "WINDOW")
        except ValueError:
            pass

    new_handler = bpy.types.SpaceView3D.draw_handler_add(
        draw_overlay,
        (),
        "WINDOW",
        "POST_PIXEL",
    )
    handlers[OVERLAY_HANDLER_KEY] = new_handler

    for area in bpy.context.screen.areas:
        if area.type == "VIEW_3D":
            area.tag_redraw()


def main():
    if bpy.context.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")

    meshes = get_level_meshes()
    minimum, maximum = calculate_bounds(meshes)
    size = maximum - minimum

    install_overlay()

    print(
        f"Ars3D MonoGame size: "
        f"X={size.x:.2f}, Y={size.z:.2f}, Z={size.y:.2f}"
    )


main()
