extends GridBase
class_name SpatialGridManager

## private vars
var _unit_grid: Dictionary = {}
var _building_grid: Dictionary = {}

var _registered_units: Array[SpatialGridUnit] = []
var _registered_buildings: Array[SpatialGridBuilding]
var _next_unit_id: int = 0
var _next_building_id: int = 0

## built-in override methods
func _ready() -> void:
	print("UnitGridManager initialized")


## public methods
func register_unit(position: Vector3, radius: float, manager: Node, manager_index: int, team_id: int = 0) -> SpatialGridUnit:
	var data = SpatialGridUnit.new()
	data.position = position
	data.radius = radius
	data.manager = manager
	data.manager_index = manager_index
	data.team_id = team_id
	data.grid_cell = world_to_grid(position)
	data.entity_id = _next_unit_id
	_next_unit_id += 1
	
	_registered_units.append(data)
	_add_to_grid(data)
	
	return data


func register_building(position: Vector3, building: Node3D, team_id: int, attack_radius: float) -> SpatialGridBuilding:
	var data = BuildingGridData.new()
	data.position = position
	data.building = building
	data.team_id = team_id
	data.grid_cell = world_to_grid(position)
	data.entity_id = _next_building_id
	data.attack_radius = attack_radius
	_next_building_id += 1

	_registered_buildings.append(data)
	_add_building_to_cell(data)

	return data


func unregister_unit(unit_data: SpatialGridUnit):
	_remove_from_grid(unit_data)
	_registered_units.erase(unit_data)


func unregister_building(building_data: SpatialGridBuilding):
	_remove_building_from_cell(building_data)
	_registered_buildings.erase(building_data)


func update_unit_position(unit_data: SpatialGridUnit, new_position: Vector3):
	var new_cell = world_to_grid(new_position)
	
	if new_cell != unit_data.grid_cell:
		_remove_from_grid(unit_data)
		unit_data.grid_cell = new_cell
		_add_to_grid(unit_data)
	
	unit_data.position = new_position


func get_nearby_units(position: Vector3, radius: float, exclude_team: int = -1) -> Array[SpatialGridUnit]:
	var nearby_units: Array[SpatialGridUnit] = []
	var center_cell = world_to_grid(position)
	
	var cell_distance_to_check = int(ceil(radius / grid_cell_size)) + 1
	var radius_squared = radius * radius
	
	var cells_to_check = get_cells_in_radius(center_cell, cell_distance_to_check)
	for cell_to_check in cells_to_check:
		if not _unit_grid.has(cell_to_check):
			continue
		
		var cell_units = _unit_grid[cell_to_check]
		for unit_data in cell_units.values():
			if exclude_team >= 0 and unit_data.team_id == exclude_team:
				continue
			
			var distance_squared = position.distance_squared_to(unit_data.position)
			if distance_squared <= radius_squared:
				nearby_units.append(unit_data)
	
	return nearby_units 


func get_nearby_buildings(position: Vector3, radius: float, exclude_team: int = -1) -> Array[SpatialGridBuilding]:
	var nearby_buildings: Array[SpatialGridBuilding] = []
	var center_cell = world_to_grid(position)
	var cell_radius = int(ceil(radius / grid_cell_size)) + 1

	var cells_to_check = get_cells_in_radius(center_cell, cell_radius)

	for cell in cells_to_check:
		if not _building_grid.has(cell):
			continue

		var cell_buildings = _building_grid[cell]
		for building_data in cell_buildings.values():
			if exclude_team >= 0 and building_data.team_id == exclude_team:
				continue

			var distance = position.distance_to(building_data.position)
			if distance <= radius + building_data.radius:
				nearby_buildings.append(building_data)

	return nearby_buildings


func get_nearest_unit(position: Vector3, radius: float, exclude_team: int = -1) -> SpatialGridUnit:
	var nearby_units = get_nearby_units(position, radius, exclude_team)
	
	if nearby_units.is_empty():
		return null
	
	var nearest_unit: SpatialGridUnit = null
	var nearest_unit_distance_squared = INF
	
	for unit_data in nearby_units:
		var distance_squared = position.distance_squared_to(unit_data.position)
		
		if distance_squared < nearest_unit_distance_squared:
			nearest_unit_distance_squared = distance_squared
			nearest_unit = unit_data
	
	return nearest_unit


func get_nearest_building(position: Vector3, radius: float, exclude_team: int = -1) -> SpatialGridBuilding:
	var nearby_buildings = get_nearby_buildings(position, radius, exclude_team)

	if nearby_buildings.is_empty():
		return null
	
	var nearest: SpatialGridBuilding = null
	var nearest_distance = INF

	for building_data in nearby_buildings:
		var distance = position.distance_to(building_data.position)
		if distance < nearest_distance:
			nearest_distance = distance
			nearest = building_data
	
	return nearest


func get_units_in_cell(cell: Vector2i) -> Array[SpatialGridUnit]:
	if _unit_grid.has(cell):
		var units: Array[SpatialGridUnit] = []
		units.assign(_unit_grid[cell].values())
		return units
	
	return []


func get_grid_stats() -> Dictionary:
	return {
		"total_units": _registered_units.size(),
		"occupied_cells": _unit_grid.size(),
		"cell_size": grid_cell_size
	}

func print_stats():
	var stats = get_grid_stats()
	print("=== Grid Stats ===")
	print("Total units: ", stats.total_units)
	print("Occupied cells: ", stats.occupied_cells)
	print("Avg units per cell: ", float(stats.total_units) / max(stats.occupied_cells, 1))


## private methods
func _add_to_grid(unit_data: SpatialGridUnit):
	if not _unit_grid.has(unit_data.grid_cell):
		_unit_grid[unit_data.grid_cell] = {}
	_unit_grid[unit_data.grid_cell][unit_data.entity_id] = unit_data


func _remove_from_grid(unit_data: SpatialGridUnit):
	if not _unit_grid.has(unit_data.grid_cell):
		return
	
	var cell_dict = _unit_grid[unit_data.grid_cell]
	cell_dict.erase(unit_data.entity_id)
	
	if cell_dict.is_empty():
		_unit_grid.erase(unit_data.grid_cell)


func _add_building_to_cell(building_data: SpatialGridBuilding):
	if not _building_grid.has(building_data.grid_cell):
		_building_grid[building_data.grid_cell] = {}
	_building_grid[building_data.grid_cell][building_data.entity_id] = building_data


func _remove_building_from_cell(building_data: SpatialGridBuilding):
	if not _building_grid.has(building_data.grid_cell):
		return
	
	var cell_dict = _building_grid[building_data.grid_cell]
	cell_dict.erase(building_data.entity_id)
	
	if cell_dict.is_empty():
		_building_grid.erase(building_data.grid_cell)
