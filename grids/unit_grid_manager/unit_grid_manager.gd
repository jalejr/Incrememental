extends GridManagerBase
class_name SpatialGridManager
## enums
## consts
## exports
## public vars
var spatial_grid: Dictionary = {}
var registered_units: Array[UnitGridData] = []
## private vars
var _next_unit_id: int = 0
## onready vars

## built-in override methods
func _ready() -> void:
	print("UnitGridManager initialized")


## public methods
func register_unit(position: Vector3, manager: Node, manager_index: int, team_id: int = 0) -> UnitGridData:
	var data = UnitGridData.new()
	data.position = position
	data.manager = manager
	data.manager_index = manager_index
	data.team_id = team_id
	data.grid_cell = world_to_grid(position)
	data.unit_id = _next_unit_id
	_next_unit_id += 1
	
	registered_units.append(data)
	_add_to_grid(data)
	
	return data


func unregister_unit(unit_data: UnitGridData):
	_remove_from_grid(unit_data)
	registered_units.erase(unit_data)


func update_unit_position(unit_data: UnitGridData, new_position: Vector3):
	var new_cell = world_to_grid(new_position)
	
	if new_cell != unit_data.grid_cell:
		_remove_from_grid(unit_data)
		unit_data.grid_cell = new_cell
		_add_to_grid(unit_data)
	
	unit_data.position = new_position


func get_nearby_units(position: Vector3, radius: float, exclude_team: int = -1) -> Array[UnitGridData]:
	var nearby_units: Array[UnitGridData] = []
	var center_cell = world_to_grid(position)
	
	var cell_distance_to_check = int(ceil(radius / grid_cell_size)) + 1
	var radius_squared = radius * radius
	
	var cells_to_check = get_cells_in_radius(center_cell, cell_distance_to_check)
	for cell_to_check in cells_to_check:
		if not spatial_grid.has(cell_to_check):
			continue
		
		var cell_units = spatial_grid[cell_to_check]
		for unit_data in cell_units.values():
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
		var units: Array[UnitGridData] = []
		units.assign(spatial_grid[cell].values())
		return units
	
	return []


func get_grid_stats() -> Dictionary:
	return {
		"total_units": registered_units.size(),
		"occupied_cells": spatial_grid.size(),
		"cell_size": grid_cell_size
	}

func print_stats():
	var stats = get_grid_stats()
	print("=== Grid Stats ===")
	print("Total units: ", stats.total_units)
	print("Occupied cells: ", stats.occupied_cells)
	print("Avg units per cell: ", float(stats.total_units) / max(stats.occupied_cells, 1))


## private methods
func _add_to_grid(unit_data: UnitGridData):
	if not spatial_grid.has(unit_data.grid_cell):
		spatial_grid[unit_data.grid_cell] = {}
	spatial_grid[unit_data.grid_cell][unit_data.unit_id] = unit_data


func _remove_from_grid(unit_data: UnitGridData):
	if not spatial_grid.has(unit_data.grid_cell):
		return
	
	var cell_dict = spatial_grid[unit_data.grid_cell]
	cell_dict.erase(unit_data.unit_id)
	
	if cell_dict.is_empty():
		spatial_grid.erase(unit_data.grid_cell)
