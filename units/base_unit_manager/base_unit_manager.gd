extends Node
class_name BaseUnitManager

signal unit_died(unit: BaseUnitData, building: Node)
## enums
## consts
## exports
@export var unit_mesh: Mesh
@export var navigation_region: NavigationRegion3D
@export var grid_manager: SpatialGridManager
@export var team_id: int = 0
@export var units_updated_per_frame: int = 100
@export var visual_lerp_speed: float = 10

## public vars
var default_move_speed: float = 4.0
var default_health: float = 100.0
var default_attack_damage: float = 10.0
var default_attack_range: float = 3.0

## private vars
var units: Array[BaseUnitData] = []
var multimesh: MultiMesh
var multimesh_instance: MultiMeshInstance3D
var nav_map: RID
var update_index: int = 0

## onready vars
## methods to override
func update_unit_logic(unit: BaseUnitData, delta: float):
	if unit.nav_path.is_empty() or unit.path_index >= unit.nav_path.size():
	# Unit is idle, give it a new patrol point
		var patrol_point = unit.spawn_building.global_position + Vector3(
			randf_range(-20, 20),  # Also made this smaller
			0,
			randf_range(-20, 20)
		)
		_set_unit_path(unit, patrol_point)
	if not unit.nav_path.is_empty():
		move_along_path(unit, delta)


func on_unit_damaged(_unit: BaseUnitData, _damage: float, _source_position: Vector3):
	pass

func on_unit_died(_position: Vector3):
	pass


## built-in override methods
func _ready() -> void:
	if not grid_manager:
		push_error("GridManager not assigned to ", name)
		return
	
	_setup_multimesh()
	_setup_navigation()


func _process(delta: float) -> void:
	_update_logic(delta) # we are time slicing
	_update_visuals(delta) # we do this on all


func _physics_process(_delta: float) -> void:
	for unit in units:
		NavigationServer3D.agent_set_position(unit.agent_rid, unit.position)
		NavigationServer3D.agent_set_velocity(unit.agent_rid, unit.velocity)


func _exit_tree():
	for unit in units:
		if unit.grid_data:
			grid_manager.unregister_unit(unit.grid_data)
		if unit.agent_rid.is_valid():
			NavigationServer3D.free_rid(unit.agent_rid)


## public methods
func spawn_unit(position: Vector3, custom_stats: UnitStats = null, building: Node = null) -> BaseUnitData:
	if units.size() >= multimesh.instance_count:
		push_error("Cannot spawn more units! Max capacity: ", multimesh.instance_count)
		return null
	
	var unit: BaseUnitData = BaseUnitData.new()
	unit.position = position
	unit.visual_position = position
	unit.spawn_building = building
	
	if custom_stats:
		unit.stats = custom_stats.duplicate_stats()
	else:
		unit.stats = get_default_stats()
	
	unit.health = unit.stats.max_health
	
	unit.agent_rid = NavigationServer3D.agent_create()
	NavigationServer3D.agent_set_map(unit.agent_rid, nav_map)
	NavigationServer3D.agent_set_radius(unit.agent_rid, 0.5)
	NavigationServer3D.agent_set_max_speed(unit.agent_rid, unit.stats.move_speed)
	
	var index = units.size()
	unit.grid_data = grid_manager.register_unit(position, self, index, team_id)
	
	units.append(unit)
	
	multimesh.visible_instance_count = units.size()
	_update_unit_visuals(unit, index)
	
	return unit


func destroy_unit(index: int):
	if index < 0 or index >= units.size():
		return
	
	var unit = units[index]
	var building: Node = unit.spawn_building
	
	grid_manager.unregister_unit(unit.grid_data)
	
	if unit.agent_rid.is_valid():
		NavigationServer3D.free_rid(unit.agent_rid)
	
	unit_died.emit(unit, building)
	
	units.remove_at(index)
	multimesh.instance_count = units.size()
	
	for i in range(index, units.size()):
		units[i].grid_data.manager_index = i
		_update_unit_visuals(units[i], i)
	
	on_unit_died(unit.position)


func get_unit(index: int) -> BaseUnitData:
	if index >= 0 and index < units.size():
		return units[index]
	return null


func damage_unit(index: int, damage: float, source_position: Vector3 = Vector3.ZERO) -> bool:
	if index < 0 or index >= units.size():
		return false
	
	var unit = units[index]
	unit.health -= damage
	
	on_unit_damaged(unit, damage, source_position)
	
	if unit.health <= 0:
		destroy_unit(index)
		return true
	
	return false


func get_default_stats() -> UnitStats:
	var stats: UnitStats = UnitStats.new()
	stats.move_speed = default_move_speed
	stats.max_health = default_health
	stats.attack_damage = default_attack_damage
	stats.attack_range = default_attack_range
	return stats


## private methods
func _setup_multimesh():
	multimesh = MultiMesh.new()
	multimesh.mesh = unit_mesh
	multimesh.transform_format = MultiMesh.TRANSFORM_3D
	multimesh.use_custom_data = true
	multimesh.instance_count = 3000
	multimesh.visible_instance_count = 0
	
	multimesh_instance = MultiMeshInstance3D.new()
	multimesh_instance.multimesh = multimesh
	add_child(multimesh_instance)


func _setup_navigation() -> void:
	if navigation_region:
		nav_map = navigation_region.get_navigation_map()


func _update_logic(delta: float):
	if units.is_empty():
		return
	
	# Calculate delta compensation (if updating every N frames, multiply delta by N)
	var frames_between_updates = float(units.size()) / float(units_updated_per_frame)
	var compensated_delta = delta * frames_between_updates
	
	for i in range(units_updated_per_frame):
		var index = (update_index + i) % units.size()
		var unit = units[index]
		
		update_unit_logic(unit, compensated_delta)
		
		# Update grid position
		if unit.grid_data:
			grid_manager.update_unit_position(unit.grid_data, unit.position)
	
	update_index = (update_index + units_updated_per_frame) % max(units.size(), 1)


func _update_visuals(delta: float):
	for i in range(units.size()):
		var unit = units[i]
		var lerp_weight: float = clamp(visual_lerp_speed * delta, 0.0, 1.0)
		unit.visual_position = unit.visual_position.lerp(unit.position, lerp_weight)
		
		_update_unit_visuals(unit, i)


func _update_unit_visuals(unit: BaseUnitData, index: int):
	var transform = Transform3D(Basis(), unit.visual_position)
	multimesh.set_instance_transform(index, transform)
	
	var health_percent = unit.health / unit.stats.max_health
	var custom_data = Color(health_percent, 0, 0, 1)
	# TODO health logic health bar perhaps
	multimesh.set_instance_custom_data(index, custom_data)


func move_along_path(unit: BaseUnitData, delta: float):
	if unit.nav_path.is_empty() or unit.path_index >= unit.nav_path.size():
		unit.velocity = Vector3.ZERO
		return
	
	var target = unit.nav_path[unit.path_index]
	var direction = (target - unit.position).normalized()
	unit.velocity = direction * unit.stats.move_speed
	
	var safe_velocity = NavigationServer3D.agent_get_velocity(unit.agent_rid)
	var distance = unit.position.distance_to(target)
	
	if distance < 0.5:
		unit.path_index += 1
	else:
		unit.position += safe_velocity * delta


func _move_along_path(unit: BaseUnitData, delta: float):
	if unit.nav_path.is_empty() or unit.path_index >= unit.nav_path.size():
		unit.velocity = Vector3.ZERO
		return
	
	var target = unit.nav_path[unit.path_index]
	var direction = (target - unit.position).normalized()
	unit.velocity = direction * unit.stats.move_speed
	
	var safe_velocity = NavigationServer3D.agent_get_velocity(unit.agent_rid)
	var distance = unit.position.distance_to(target)
	
	if distance < 0.5:
		unit.path_index += 1
	else:
		unit.position += safe_velocity * delta


func _set_unit_path(unit: BaseUnitData, target: Vector3):
	unit.nav_path = NavigationServer3D.map_get_path(nav_map, unit.position, target, true)
	unit.path_index = 0
