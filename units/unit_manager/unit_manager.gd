extends Node
class_name UnitManager

signal unit_died(unit: UnitData, building: Node)

class UnitTypeRuntimeData:
	var config: UnitTypeConfig
	var multimesh: MultiMesh
	var multimesh_instance: MultiMeshInstance3D
	var alive_count: int = 0
	var visual_index: int = 0

## exports
@export var unit_type_configs: Array[UnitTypeConfig] = []
@export var navigation_region: NavigationRegion3D
@export var grid_manager: SpatialGridManager
@export var max_units_updated_per_frame: int = 100
@export var visual_lerp_speed: float = 10

## private vars
var nav_map: RID
var update_index: int = 0

var _unit_types_runtime: Dictionary = {}
var _all_units: Array[UnitData] = []
var _free_indices: Array[int] = []
var _alive_count_for_all: int = 0

var _thread_pool: Array[Thread] = []
var _thread_count: int
var _result_queue: Array = []
var _queue_mutex: Mutex = Mutex.new()

var _path_request_queue: Array = []
var _damage_request_queue: Array = []

## methods to override
# These two should probably become methods in UnitData
func on_unit_damaged(_unit: UnitData, _damage: float, _source_position: Vector3):
	pass

func on_unit_died(_position: Vector3):
	pass


## built-in override methods
func _ready() -> void:
	_thread_count = _calculate_optimal_thread_count()
	_initialize_threads()
	_setup_unit_types()
	print("How manY?")
	if not grid_manager:
		push_error("GridManager not assigned to ", name)
		return
	_setup_navigation()


func _process(delta: float) -> void:
	var start = Time.get_ticks_usec()
	var logic_time = Time.get_ticks_usec() - start
	start = Time.get_ticks_usec()
	
	_update_visuals(delta)
	
	var visual_time = Time.get_ticks_usec() - start
	
	print("Name: ", name)
	print("Logic: %dμs, Visuals: %dμs" % [logic_time, visual_time])


func _physics_process(delta: float) -> void:
	_update_logic(delta)
	_update_navigation_sync()
	_update_movement(delta)


## public methods
func spawn_unit(
	unit_type: Unit.Type,
	team_id: int,
	position: Vector3, 
	buffs: Dictionary[Buff.Type, float] = {}, 
	building: Node = null,
	targetable: bool = true,
	attackable: bool = true
	) -> UnitData:
	if not _unit_types_runtime.has(unit_type):
		push_error("Unknown unit type ID: %d" % unit_type)
		return null
	
	var runtime: UnitTypeRuntimeData = _unit_types_runtime[unit_type]
	var config: UnitTypeConfig = runtime.config
	
	var unit: UnitData = config.unit_script.new()
	
	unit.unit_type = config.unit_type
	unit.position = position
	unit.visual_position = position
	unit.spawn_building = building
	unit.team_id = team_id
	unit.is_alive = true
	unit.is_targetable = targetable
	unit.is_attackable = attackable
	unit.cached_runtime = runtime
	
	unit.stats = _calculate_stats_with_buffs(config.default_stats, buffs)
	unit.health = unit.stats.max_health
	
	unit.agent_rid = NavigationServer3D.agent_create()
	NavigationServer3D.agent_set_map(unit.agent_rid, nav_map)
	NavigationServer3D.agent_set_radius(unit.agent_rid, unit.stats.radius)
	NavigationServer3D.agent_set_max_speed(unit.agent_rid, unit.stats.move_speed)
	NavigationServer3D.agent_set_avoidance_enabled(unit.agent_rid, true)
	
	var index: int
	
	if not _free_indices.is_empty():
		index = _free_indices.pop_back()
		_all_units[index] = unit
	else:
		index = _all_units.size()
		_all_units.append(unit)
	
	unit.grid_data = grid_manager.register_unit(position, unit.stats.radius, index, team_id)
	
	_alive_count_for_all += 1
	runtime.alive_count += 1
	runtime.multimesh.visible_instance_count = runtime.alive_count
	
	var instance_idx = runtime.alive_count - 1
	var transform = Transform3D(Basis(), position)
	runtime.multimesh.set_instance_transform(instance_idx, transform)
	runtime.multimesh.set_instance_custom_data(instance_idx, unit.get_custom_visual_data())
	
	return unit


func destroy_unit(index: int):
	if index < 0 or index >= _all_units.size():
		return
	
	var unit = _all_units[index]
	var building: Node = unit.spawn_building
	if not unit.is_alive:
		return
		
	unit.is_alive = false
	
	grid_manager.unregister_unit(unit.grid_data)
	
	if unit.agent_rid.is_valid():
		NavigationServer3D.free_rid(unit.agent_rid)
	
	var runtime: UnitTypeRuntimeData = _unit_types_runtime[unit.unit_type]
	runtime.alive_count -= 1
	runtime.multimesh.visible_instance_count = runtime.alive_count
	
	unit_died.emit(unit, building)
	on_unit_died(unit.position)


func get_unit(index: int) -> UnitData:
	if index >= 0 and index < _all_units.size():
		var unit = _all_units[index]
		if unit.is_alive:
			return unit
	return null


func damage_unit(index: int, damage: float, source_position: Vector3 = Vector3.ZERO) -> bool:
	var unit = get_unit(index)
	if not unit:
		return false
	
	if not unit.is_attackable:
		return false
	
	unit.health -= damage
	on_unit_damaged(unit, damage, source_position)
	
	if unit.health <= 0:
		destroy_unit(index)
	
	return true


func set_unit_path(unit: UnitData, target: Vector3):
	unit.nav_path = NavigationServer3D.map_get_path(
		nav_map, 
		unit.position, 
		target, 
		true
	)
	unit.path_index = 0
	unit.cached_target_position = target
	unit.path_age = 0.0


func queue_damage_request(target_unit: UnitData, damage: float, source_position: Vector3 = Vector3.ZERO):
	if not target_unit or not target_unit.is_alive or not target_unit.is_attackable:
		return
	
	target_unit.health -= damage
	on_unit_damaged(target_unit, damage, source_position)
	
	if target_unit.health <= 0:
		var index = _all_units.find(target_unit)
		if index >= 0:
			destroy_unit(index)


## private methods
func _setup_unit_types():
	if unit_type_configs.size() <= 0:
		push_warning("No unit_type_configs added to manager...")
	
	for config in unit_type_configs:
		if _unit_types_runtime.has(config.unit_type):
			push_error("Duplicate type_id %d for %s" % [config.unit_type, config.type_name])
			continue
		
		var runtime = UnitTypeRuntimeData.new()
		runtime.config = config
		
		_setup_multimesh(runtime, config)
		
		_unit_types_runtime[config.unit_type] = runtime
		

func _setup_multimesh(runtime: UnitTypeRuntimeData, config: UnitTypeConfig):
	runtime.multimesh = MultiMesh.new()
	runtime.multimesh.mesh = config.mesh
	runtime.multimesh.transform_format = MultiMesh.TRANSFORM_3D
	runtime.multimesh.use_custom_data = true
	runtime.multimesh.instance_count = config.max_count
	runtime.multimesh.visible_instance_count = 0
	
	runtime.multimesh_instance = MultiMeshInstance3D.new()
	runtime.multimesh_instance.multimesh = runtime.multimesh
	
	add_child(runtime.multimesh_instance)


func _calculate_stats_with_buffs(base_stats: UnitStats, buffs: Dictionary) -> UnitStats:
	var final_stats = base_stats.duplicate_stats()
	# TODO real logic soon tm
	return final_stats


func _setup_navigation() -> void:
	if navigation_region:
		nav_map = navigation_region.get_navigation_map()


func _update_logic(delta: float):
	if _alive_count_for_all == 0:
		return
	
	var frames_between_updates = ceili(_alive_count_for_all / float(max_units_updated_per_frame))
	var compensated_delta = delta * frames_between_updates
	var units_this_frame = mini(max_units_updated_per_frame, _alive_count_for_all)
	var checked = 0
	var updated = 0
	var context = _create_logic_context(compensated_delta)
	var start_index = update_index
	
	while updated < units_this_frame and checked < _all_units.size():
		var index = (start_index + checked) % _all_units.size()
		var unit = _all_units[index]
		
		checked += 1
		
		if not unit.is_alive:
			continue
		
		unit.path_age += compensated_delta
		unit.update_logic(compensated_delta, context)
		
		updated += 1
	
	update_index = (start_index + checked) % max(_all_units.size(), 1)


func _update_visuals_minimal(delta: float):
	var count = 0
	for unit in _all_units:
		if not unit.is_alive:
			continue
		count += 1
	print("Minimal: %d alive units iterated" % count)


func _update_visuals(delta: float):
	for runtime in _unit_types_runtime.values():
		runtime.visual_index = 0
	
	for unit in _all_units:
		if not unit.is_alive:
			continue
		
		var lerp_weight = clampf(visual_lerp_speed * delta, 0.0, 1.0)
		
		unit.visual_position = unit.visual_position.lerp(unit.position, lerp_weight)

		var runtime = unit.cached_runtime
		var instance_idx = runtime.visual_index
		runtime.visual_index += 1
		
		var transform = Transform3D(Basis(), unit.visual_position)
		runtime.multimesh.set_instance_transform(instance_idx, transform)
		
		var custom_data = unit.get_custom_visual_data()
		runtime.multimesh.set_instance_custom_data(instance_idx, custom_data)


func _update_navigation_sync():
	for unit in _all_units:
		if not unit.is_alive:
			continue
		
		NavigationServer3D.agent_set_position(unit.agent_rid, unit.position)
		NavigationServer3D.agent_set_velocity(unit.agent_rid, unit.velocity)


func _update_movement(delta: float) -> void:
	for i in range(_all_units.size()):
		var unit = _all_units[i]
		if not unit.is_alive:
			continue
	
		var safe_velocity = NavigationServer3D.agent_get_velocity(unit.agent_rid)
		
		if not unit.nav_path.is_empty() and unit.path_index < unit.nav_path.size():
			var target = unit.nav_path[unit.path_index]
			var distance = unit.position.distance_to(target)
			
			if distance < 0.5:
				unit.path_index += 1
				if unit.path_index >= unit.nav_path.size():
					unit.velocity = Vector3.ZERO
					continue
				else:
					target = unit.nav_path[unit.path_index]
			
			var direction = (target - unit.position).normalized()
			unit.velocity = direction * unit.stats.move_speed
			
			safe_velocity.y = unit.velocity.y
			unit.position += safe_velocity * delta
		else:
			unit.velocity = Vector3.ZERO
		
		if unit.grid_data:
			var new_cell = grid_manager.world_to_grid(unit.position)
			if new_cell != unit.grid_data.grid_cell:
				grid_manager.update_unit_position(unit.grid_data, unit.position)


func _create_logic_context(delta: float) -> Dictionary:
	"""Create context dictionary for unit logic updates"""
	return {
		"delta": delta,
		"manager": self,
		"grid_manager": grid_manager,
		"nav_map": nav_map,

		"set_path": set_unit_path,
		"damage_unit": queue_damage_request  # For thread-safety later
	}


func _calculate_optimal_thread_count() -> int:
	var cpu_count = OS.get_processor_count()
	return maxi(1, mini(int(cpu_count * 0.75), 8))


func _initialize_threads():
	_thread_pool.resize(_thread_count)
	for i in _thread_count:
		_thread_pool[i] = Thread.new()


func _exit_tree():
	for unit in _all_units:
		if unit.is_alive:
			if unit.grid_data:
				grid_manager.unregister_unit(unit.grid_data)
			if unit.agent_rid.is_valid():
				NavigationServer3D.free_rid(unit.agent_rid)
	
	for thread in _thread_pool:
		if thread.is_started():
			thread.wait_to_finish()
