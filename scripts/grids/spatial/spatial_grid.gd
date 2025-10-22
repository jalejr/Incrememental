extends GridBase
class_name SpatialGridManager

## private vars
var _unit_grid: Dictionary = {}
var _building_grid: Dictionary = {}
var _entity_id_to_entity_object: Dictionary[int, Variant] = {}
var _next_entity_id: int = 0

## built-in override methods
func _ready() -> void:
	print("SpatialGridManager initialized")


## public methods
func register_entity(entity_data: EntityData, entity_object: Variant) -> SpatialGridEntity:
	var entity = SpatialGridEntity.new()
	entity.entity_data = entity_data
	entity.entity_id = _next_entity_id
	_next_entity_id += 1
	
	entity.grid_cell = world_to_grid(entity_data.position)
	entity.occupied_cells = _get_potentially_occupied_cells(
		entity.grid_cell,
		entity_data.radius
	)
	
	_add_to_grid(entity)
	_entity_id_to_entity_object[entity.entity_id] = entity_object
	
	return entity


func unregister_entity(entity: SpatialGridEntity) -> void:
	_remove_from_grid(entity)
	
	_entity_id_to_entity_object.erase(entity.entity_id)


func update_entity_position(entity: SpatialGridEntity, new_position: Vector3) -> void:
	var new_cell = world_to_grid(new_position)
	
	if new_cell != entity.grid_cell:
		_remove_from_grid(entity)
		
		entity.grid_cell = new_cell
		entity.entity_data.position = new_position
		entity.occupied_cells = _get_potentially_occupied_cells(entity.grid_cell, entity.radius)
		
		_add_to_grid(entity)
	else:
		entity.entity_data.position = new_position


func get_nearby_entities(position: Vector3, radius: float, team_id: int = -1, is_targeting_allies: bool = false) -> Array[Variant]:
	var nearby_units: Array[Variant] = get_nearby_entities_by_type(
		EntityData.Type.UNIT, 
		position, 
		radius, 
		team_id, 
		is_targeting_allies
	)
	var nearby_buildings: Array[Variant] = get_nearby_entities_by_type(
		EntityData.Type.BUILDING, 
		position, 
		radius, 
		team_id, 
		is_targeting_allies
	)
	
	return nearby_units + nearby_buildings


func get_nearby_entities_by_type(
	entity_type: EntityData.Type, 
	position: Vector3, 
	radius: float, 
	team_id: EntityData.Team, 
	is_targeting_allies: bool = false
) -> Array[Variant]:
	var nearby_entities: Array[Variant] = []
	var seen_entities: Dictionary = {}
	var center_cell = world_to_grid(position)
	var cell_distance_to_check = int(ceil(radius / grid_cell_size)) + 1
	var cells_to_check = get_cells_in_radius(center_cell, cell_distance_to_check)
	var target_grid = _find_grid_to_target(entity_type)
	
	for cell_to_check in cells_to_check:
		if not target_grid.has(cell_to_check):
			continue
		
		var cell_units = target_grid[cell_to_check]
		for entity in cell_units.values():
			if seen_entities.has(entity.entity_id):
				continue
			
			seen_entities[entity.entity_id] = true
			
			var data: EntityData = entity.entity_data
			
			if not data.is_alive or not data.is_targetable:
				continue
			
			if team_id != EntityData.Team.NONE:
				if is_targeting_allies:
					if data.team_id != team_id:
						continue
				else:
					if data.team_id == team_id:
						continue
			
			var distance_squared = position.distance_squared_to(data.position)
			var effective_radius = radius + data.radius
			
			if distance_squared <= effective_radius * effective_radius:
				nearby_entities.append(_get_entity_object(entity))
	
	return nearby_entities


func get_nearest_entity(position: Vector3, radius: float, team_id: EntityData.Team, is_targeting_allies: bool = false) -> Variant:
	var nearby_unit: Variant = get_nearest_entity_by_type(
		EntityData.Type.UNIT, 
		position, 
		radius, 
		team_id, 
		is_targeting_allies
	)
	
	if nearby_unit: return nearby_unit
	
	var nearby_building: Variant = get_nearest_entity_by_type(
		EntityData.Type.BUILDING, 
		position, 
		radius, 
		team_id, 
		is_targeting_allies
	)
	
	if nearby_building: return nearby_building
	
	return null


func get_nearest_entity_by_type(
	entity_type: EntityData.Type, 
	position: Vector3, 
	radius: float, 
	team_id: EntityData.Team, 
	is_targeting_allies: bool = false
) -> Variant:
	var center_cell = world_to_grid(position)
	var max_cell_radius = int(ceil(radius / grid_cell_size)) + 1
	var seen_entities: Dictionary = {}
	var target_grid = _find_grid_to_target(entity_type)
	
	for ring in range(max_cell_radius + 1):
		var cells_in_ring = _get_cells_in_ring(center_cell, ring)
		
		for cell in cells_in_ring:
			if not target_grid.has(cell):
				continue
			
			var cell_units = target_grid[cell]
			for entity in cell_units.values():
				if seen_entities.has(entity.entity_id):
					continue
				
				seen_entities[entity.entity_id] = true
				
				var data: EntityData = entity.entity_data
				
				if not data.is_alive or not data.is_targetable:
					continue
				
				if team_id != EntityData.Team.NONE:
					if is_targeting_allies:
						if data.team_id != team_id:
							continue
					else:
						if data.team_id == team_id:
							continue
				
				var distance_squared = position.distance_squared_to(data.position)
				var effective_radius = radius + data.radius
				
				if distance_squared <= effective_radius * effective_radius:
					return _get_entity_object(entity)
	
	return null


func get_grid_stats() -> Dictionary:
	return {
		"total_entities": _entity_id_to_entity_object.size(),
		"occupied_cells": _unit_grid.size(),
		"cell_size": grid_cell_size
	}


func print_stats():
	var stats = get_grid_stats()
	print("=== Grid Stats ===")
	print("Occupied cells: ", stats.occupied_cells)
	print("Avg entities per cell: ", float(stats.total_entities) / max(stats.occupied_cells, 1))

## private methods
func _add_to_grid(entity: SpatialGridEntity):
	var target_grid: Dictionary = _find_grid_to_target(entity.entity_data.type)
	
	for cell in entity.occupied_cells:
		if not target_grid.has(cell):
			target_grid[cell] = {}
		target_grid[cell][entity.entity_id] = entity


func _remove_from_grid(entity: SpatialGridEntity):
	var target_grid: Dictionary = _find_grid_to_target(entity.entity_data.type)
	
	for cell in entity.occupied_cells:
		if not target_grid.has(cell):
			continue
		
		var cell_dict = target_grid[cell]
		cell_dict.erase(entity.entity_id)
		
		if cell_dict.is_empty():
			target_grid.erase(cell)


func _find_grid_to_target(entity_type: EntityData.Type) -> Dictionary:
	match entity_type:
		EntityData.Type.UNIT:
			return _unit_grid
		EntityData.Type.BUILDING:
			return _building_grid
	
	push_error("Trying to access Unknown unit type grid ID: %d" % entity_type)
	return {}


func _get_entity_object(entity: SpatialGridEntity) -> Variant:
	return _entity_id_to_entity_object.get(entity.entity_id)


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
