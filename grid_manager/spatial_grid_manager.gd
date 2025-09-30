extends Node
## enums
## consts
## exports
@export var grid_cell_size: float = 5.0
@export var updated_units_per_frame: int = 100
## public vars
var spatial_grid: Dictionary = {}
var registered_units: Array[UnitGridData] = []
var update_index: int = 0
## private vars
## onready vars
## built-in override methods


func _ready() -> void:
	print("SpatialGridManager initialized")


func _process(_delta: float) -> void:
	_update_grid()


## public methods
func register_unit(position: Vector3, manager: Node, manager_index: int, team_id: int = 0) -> UnitGridData:
	var data = UnitGridData.new()
	data.position = position
	data.manager = manager
	data.manager_index = manager_index
	data.team_id = team_id
	data.grid_cell = _world_to_grid(position)
	
	registered_units.append(data)
	_add_to_grid(data)
	
	return data


func unregister_unit(unit_data: UnitGridData):
	_remove_from_grid(unit_data)
	registered_units.erase(unit_data)


func update_unit_position(unit_data: UnitGridData, new_position: Vector3):
	var new_cell = _world_to_grid(new_position)
	
	if new_cell != unit_data.grid_cell:
		_remove_from_grid(unit_data)
		unit_data.grid_cell = new_cell
		_add_to_grid(unit_data)
	
	unit_data.position = new_position


func get_nearby_units(position: Vector3, radius: float, exclude_team: int = -1) -> Array[UnitGridData]:
	var nearby_units: Array[UnitGridData] = []
	var center_cell = _world_to_grid(position)
	
	var cell_distance_to_check = int(ceil(radius / grid_cell_size)) + 1
	var radius_squared = radius * radius
	
	for x in range(-cell_distance_to_check, cell_distance_to_check + 1):
		for z in range(-cell_distance_to_check, cell_distance_to_check + 1):
			var cell_to_check = center_cell + Vector2i(x, z)
			
			if not spatial_grid.has(cell_to_check):
				continue
			
			for unit_data in spatial_grid[cell_to_check]:
				if exclude_team >= 0 and unit_data.team_id == exclude_team:
					continue
				
				var distance_squared = position.distance_squared_to(unit_data.position)
				if distance_squared <= radius_squared:
					nearby_units.append(unit_data)
	
	return nearby_units 


func get_nearest_unit(position: Vector3, radius: float, exclude_team: int = -1) -> UnitGridData:
	var nearby_units = get_nearby_units(position, radius, exclude_team)
	
	if nearby_units.is_empty():
		return null
	
	var nearest_unit: UnitGridData = null
	var nearest_unit_distance_squared = INF
	
	for unit_data in nearby_units:
		var distance_squared = position.distance_squared_to(unit_data.position)
		
		if distance_squared < nearest_unit_distance_squared:
			nearest_unit_distance_squared = distance_squared
			nearest_unit = unit_data
	
	return nearest_unit


func get_units_in_cell(cell: Vector2i) -> Array[UnitGridData]:
	if spatial_grid.has(cell):
		return spatial_grid[cell].duplicate()
	
	return []


func get_units_of_team(team_id: int) -> Array[UnitGridData]:
	var team_units: Array[UnitGridData] = []
	
	for unit_data in registered_units:
		if unit_data.team_id == team_id:
			team_units.append(unit_data)
	
	return team_units


func get_grid_stats() -> Dictionary:
	return {
		"total_units": registered_units.size(),
		"occupied_cells": spatial_grid.size(),
		"cell_size": grid_cell_size,
		"updates_per_frame": updated_units_per_frame
	}

func print_stats():
	var stats = get_grid_stats()
	print("=== Grid Stats ===")
	print("Total units: ", stats.total_units)
	print("Occupied cells: ", stats.occupied_cells)
	print("Avg units per cell: ", float(stats.total_units) / max(stats.occupied_cells, 1))


## private methods
func _update_grid():
	if registered_units.is_empty():
		return
	
	for i in range(updated_units_per_frame):
		var index = (update_index + i) % registered_units.size()
		var unit_data = registered_units[index]
		
		# Could optimize this with direct class instead of checking for method
		if unit_data.manager and unit_data.manager.has_method("get_unit_position"):
			var fresh_position = unit_data.manager.get_unit_position(unit_data.manager_index)
			if fresh_position != Vector3.ZERO:  # Valid position
				update_unit_position(unit_data, fresh_position)
	
	update_index = (update_index + updated_units_per_frame) % max(registered_units.size(), 1)
	
	
func _add_to_grid(unit_data: UnitGridData):
	if not spatial_grid.has(unit_data.grid_cell):
		spatial_grid[unit_data.grid_cell] = []
	spatial_grid[unit_data.grid_cell].append(unit_data)


func _remove_from_grid(unit_data: UnitGridData):
	if spatial_grid.has(unit_data.grid_cell):
		spatial_grid[unit_data.grid_cell].erase(unit_data)
		
		if spatial_grid[unit_data.grid_cell].is_empty():
			spatial_grid.erase(unit_data.grid_cell)


func _world_to_grid(position: Vector3) -> Vector2i:
	return Vector2i(
		int(floor(position.x / grid_cell_size)),
		int(floor(position.z / grid_cell_size))
	)
