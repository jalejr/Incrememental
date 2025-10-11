extends Node
class_name GridBase

## exports
@export var grid_cell_size: float = 5.0

## public methods
func world_to_grid(position: Vector3) -> Vector2i:
	return Vector2i(
		int(floor(position.x / grid_cell_size)),
		int(floor(position.z / grid_cell_size))
	)


func grid_to_world(grid_pos: Vector2i, centered: bool = true) -> Vector3:
	var offset = grid_cell_size * 0.5 if centered else 0.0
	return Vector3(
		grid_pos.x * grid_cell_size + offset,
		0,
		grid_pos.y * grid_cell_size + offset
	)


func get_cells_in_radius(center: Vector2i, radius: int) -> Array[Vector2i]:
	var cells: Array[Vector2i] = []
	for x in range(-radius, radius + 1):
		for y in range(-radius, radius + 1):
			cells.append(center + Vector2i(x, y))
	return cells


func get_cells_for_area(grid_pos: Vector2i, size: Vector2i) -> Array[Vector2i]:
	var cells: Array[Vector2i] = []
	for x in range(size.x):
		for y in range(size.y):
			cells.append(grid_pos + Vector2i(x, y))
	return cells
