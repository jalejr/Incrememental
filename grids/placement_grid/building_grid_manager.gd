extends GridBase
class_name BuildingGridManager

## exports
# Absolute limit of world
@export var grid_world_size: Vector2 = Vector2(512.0, 512.0)

## private vars
var _unlocked_cells: Dictionary = {}
var _occupied_cells: Dictionary = {}
var _buildings: Array[BuildingGridData] = []
## onready vars
## built-in override methods
func _ready() -> void:
	print("BuildingGridManager initalized - Cell size: ", grid_cell_size)
	# TODO Have the unlock happen through some other way like the level starting event
	unlock_starting_area(Vector3(0,0,0), 5)

## public methods
func unlock_starting_area(starting_position: Vector3, radius: int = 2):
	var center_cell = world_to_grid(starting_position)
	_unlock_cells_around(center_cell, radius)


func is_cell_unlocked(cell: Vector2i) -> bool:
	return _unlocked_cells.has(cell)


func is_cell_occupied(cell: Vector2i) -> bool:
	return _occupied_cells.has(cell)


func can_place_building(grid_pos: Vector2i, building_size: Vector2i) -> bool:
	var cells_to_check = get_cells_for_area(grid_pos, building_size)
	
	for cell in cells_to_check:
		if not _is_within_bounds(cell):
			return false
		
		if not is_cell_unlocked(cell):
			return false
		
		if is_cell_occupied(cell):
			return false
	
	return true


func place_building(building_node: Node3D, grid_pos: Vector2i, building_size: Vector2i, 
		unlock_radius: int) -> BuildingGridData:
	if not can_place_building(grid_pos, building_size):
		return null
	
	var building_data = BuildingGridData.new()
	building_data.building_node = building_node
	building_data.grid_position = grid_pos
	building_data.grid_size = building_size
	building_data.unlock_radius = unlock_radius
	
	var occupied_cells_list = _get_building_occupied_cells(building_data)
	for cell in occupied_cells_list:
		_occupied_cells[cell] = building_data
	
	_buildings.append(building_data)
	
	@warning_ignore("integer_division")
	var center_cell = grid_pos + Vector2i(building_size.x / 2, building_size.y / 2)
	_unlock_cells_around(center_cell, unlock_radius)
	
	EventBus.building_placed.emit(building_node, grid_pos)
	
	return building_data


func remove_building(building_data: BuildingGridData):
	var occupied_cells_list = _get_building_occupied_cells(building_data)
	
	for cell in occupied_cells_list:
		_occupied_cells.erase(cell)
	
	_buildings.erase(building_data)


func get_building_at_cell(cell: Vector2i) -> BuildingGridData:
	return _occupied_cells.get(cell, null)


func get_unlocked_cells() -> Array[Vector2i]:
	var cells: Array[Vector2i] = []
	cells.assign(_unlocked_cells.keys())
	return cells


func get_placement_preview_position(world_pos: Vector3, building_size: Vector2i) -> Vector3:
	var grid_pos = world_to_grid(world_pos)
	var corner = grid_to_world(grid_pos, false)
	
	return corner + Vector3(
		building_size.x * grid_cell_size* 0.5,
		0,
		building_size.y * grid_cell_size * 0.5
	)

## private methods
func _get_building_occupied_cells(building_data: BuildingGridData) -> Array[Vector2i]:
	return get_cells_for_area(building_data.grid_position, building_data.grid_size)


func _unlock_cells_around(center: Vector2i, radius: int) -> void:
	var cells_to_unlock = get_cells_in_radius(center, radius)
	
	for cell in cells_to_unlock:
		if not _is_within_bounds(cell):
			return
		
		if not _unlocked_cells.has(cell):
			_unlocked_cells[cell] = true


func _is_within_bounds(cell: Vector2i) -> bool:
	var world_pos = grid_to_world(cell, false)
	return (world_pos.x >= -grid_world_size.x and world_pos.x <= grid_world_size.x and
			world_pos.z >= -grid_world_size.y and world_pos.z <= grid_world_size.y)
