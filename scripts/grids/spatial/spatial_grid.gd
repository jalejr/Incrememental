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
func register_unit(position: Vector3, radius: float, manager_index: int, team_id: int = 0) -> SpatialGridUnit:
	var data = SpatialGridUnit.new()
	data.manager_index = manager_index
	data.position = position
	data.radius = radius
	data.team_id = team_id
	data.grid_cell = world_to_grid(position)
	data.entity_id = _next_unit_id
	data.occupied_cells = _get_potentially_occupied_cells(data.grid_cell, radius)
	_next_unit_id += 1
	
	_registered_units.append(data)
	_add_to_grid(data)
	
	return data


func register_building(position: Vector3, radius: float, building: Node3D) -> SpatialGridBuilding:
	var data = SpatialGridBuilding.new()
	data.position = position
	data.radius = building.radius
	data.building = building
	data.team_id = building.team_id
	data.grid_cell = world_to_grid(position)
	data.entity_id = _next_building_id
	data.occupied_cells = _get_potentially_occupied_cells(data.grid_cell, radius)
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


func update_unit_position(unit_data: SpatialGridUnit, new_position: Vector3) -> void:
	var new_cell = world_to_grid(new_position)
	
	if new_cell != unit_data.grid_cell:
		_remove_from_grid(unit_data)
		unit_data.grid_cell = new_cell
		unit_data.position = new_position
		unit_data.occupied_cells = _get_potentially_occupied_cells(unit_data.grid_cell, unit_data.radius)
		_add_to_grid(unit_data)
	else:
		unit_data.position = new_position


func get_nearby_units(position: Vector3, radius: float, team_id: int = -1, is_targeting_allies: bool = false) -> Array[SpatialGridUnit]:
	var nearby_units: Array[SpatialGridUnit] = []
	var seen_entities: Dictionary = {}
	var center_cell = world_to_grid(position)
	var cell_distance_to_check = int(ceil(radius / grid_cell_size)) + 1
	var cells_to_check = get_cells_in_radius(center_cell, cell_distance_to_check)
	
	for cell_to_check in cells_to_check:
		if not _unit_grid.has(cell_to_check):
			continue
		
		var cell_units = _unit_grid[cell_to_check]
		for unit_data in cell_units.values():
			if seen_entities.has(unit_data.entity_id):
				continue
			
			seen_entities[unit_data.entity_id] = true
			
			if is_targeting_allies:
				if unit_data.team_id != team_id:
					continue
			else:
				if unit_data.team_id == team_id:
					continue
			
			var distance_squared = position.distance_squared_to(unit_data.position)
			var effective_radius = radius + unit_data.radius
			
			if distance_squared <= effective_radius * effective_radius:
				nearby_units.append(unit_data)
	
	return nearby_units


func get_nearby_buildings(position: Vector3, radius: float, team_id: int = -1, is_targeting_allies: bool = false) -> Array[SpatialGridBuilding]:
	var nearby_buildings: Array[SpatialGridBuilding] = []
	var seen_entities: Dictionary = {}
	var center_cell = world_to_grid(position)
	var cell_radius = int(ceil(radius / grid_cell_size)) + 1
	var cells_to_check = get_cells_in_radius(center_cell, cell_radius)

	for cell_to_check in cells_to_check:
		if not _building_grid.has(cell_to_check):
			continue

		var cell_buildings = _building_grid[cell_to_check]
		for building_data in cell_buildings.values():
			if seen_entities.has(building_data.entity_id):
				continue
			
			seen_entities[building_data.entity_id] = true
			
			if is_targeting_allies:
				if building_data.team_id != team_id:
					continue
			else:
				if building_data.team_id == team_id:
					continue

			var distance = position.distance_squared_to(building_data.position)
			var effective_radius = radius + building_data.radius
			
			if distance <= effective_radius * effective_radius:
				nearby_buildings.append(building_data)

	return nearby_buildings


func get_nearest_unit(position: Vector3, radius: float, team_id: int = -1, is_targeting_allies: bool = false) -> SpatialGridUnit:
	var center_cell = world_to_grid(position)
	var max_cell_radius = int(ceil(radius / grid_cell_size)) + 1
	var seen_entities: Dictionary = {}
	
	# Search in expanding rings for early exit
	for ring in range(max_cell_radius + 1):
		var cells_in_ring = _get_cells_in_ring(center_cell, ring)
		
		for cell in cells_in_ring:
			if not _unit_grid.has(cell):
				continue
			
			var cell_units = _unit_grid[cell]
			for unit_data in cell_units.values():
				if seen_entities.has(unit_data.entity_id):
					continue
				seen_entities[unit_data.entity_id] = true
				
				if is_targeting_allies:
					if unit_data.team_id != team_id:
						continue
				else:
					if unit_data.team_id == team_id:
						continue
				
				var distance_squared = position.distance_squared_to(unit_data.position)
				var effective_radius = radius + unit_data.radius
				
				if distance_squared <= effective_radius * effective_radius:
					return unit_data
	
	return null


func get_nearest_building(position: Vector3, radius: float, team_id: int = -1, is_targeting_allies: bool = false) -> SpatialGridBuilding:
	var center_cell = world_to_grid(position)
	var max_cell_radius = int(ceil(radius / grid_cell_size)) + 1
	var seen_entities: Dictionary = {}

	for ring in range(max_cell_radius + 1):
		var cells_in_ring = _get_cells_in_ring(center_cell, ring)
		
		for cell in cells_in_ring:
			if not _building_grid.has(cell):
				continue

			var cell_buildings = _building_grid[cell]
			for building_data in cell_buildings.values():
				if seen_entities.has(building_data.entity_id):
					continue
				seen_entities[building_data.entity_id] = true
				
				if is_targeting_allies:
					if building_data.team_id != team_id:
						continue
				else:
					if building_data.team_id == team_id:
						continue
				
				var distance_squared = position.distance_squared_to(building_data.position)
				var effective_radius = radius + building_data.radius
				
				if distance_squared <= effective_radius * effective_radius:
					return building_data
	
	return null


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
	for cell in unit_data.occupied_cells:
		if not _unit_grid.has(cell):
			_unit_grid[cell] = {}
		_unit_grid[cell][unit_data.entity_id] = unit_data


func _remove_from_grid(unit_data: SpatialGridUnit):
	for cell in unit_data.occupied_cells:
		if not _unit_grid.has(cell):
			continue
		
		var cell_dict = _unit_grid[cell]
		cell_dict.erase(unit_data.entity_id)
		
		if cell_dict.is_empty():
			_unit_grid.erase(cell)


func _add_building_to_cell(building_data: SpatialGridBuilding):
	for cell in building_data.occupied_cells:
		if not _building_grid.has(cell):
			_building_grid[cell] = {}
		_building_grid[cell][building_data.entity_id] = building_data


func _remove_building_from_cell(building_data: SpatialGridBuilding):
	for cell in building_data.occupied_cells:
		if not _building_grid.has(cell):
			continue
		
		var cell_dict = _building_grid[cell]
		cell_dict.erase(building_data.entity_id)
		
		if cell_dict.is_empty():
			_building_grid.erase(cell)


func _get_potentially_occupied_cells(center_cell: Vector2i, radius: float) -> Array[Vector2i]:
	var occupied_cells: Array[Vector2i] = []
	
	if radius < grid_cell_size * 0.5:
		occupied_cells.append(center_cell)
		return occupied_cells
	
	var cell_radius = int(ceil(radius / grid_cell_size))
	
	for x_offset in range(-cell_radius, cell_radius + 1):
		for z_offset in range(-cell_radius, cell_radius + 1):
			occupied_cells.append(Vector2i(
				center_cell.x + x_offset,
				center_cell.y + z_offset
			))
	
	return occupied_cells


func _get_cells_in_ring(center: Vector2i, ring_radius: int) -> Array[Vector2i]:
	var cells: Array[Vector2i] = []
	
	if ring_radius == 0:
		cells.append(center)
		return cells
	
	for x in range(-ring_radius, ring_radius + 1):
		cells.append(Vector2i(center.x + x, center.y - ring_radius))  # Top edge
		cells.append(Vector2i(center.x + x, center.y + ring_radius))  # Bottom edge
	
	for z in range(-ring_radius + 1, ring_radius):
		cells.append(Vector2i(center.x - ring_radius, center.y + z))  # Left edge
		cells.append(Vector2i(center.x + ring_radius, center.y + z))  # Right edge
	
	return cells
