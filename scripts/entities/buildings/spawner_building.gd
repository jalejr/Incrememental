extends Building
class_name SpawnerBuilding

## exports
@export var spawner_data: SpawnerData
@export var spawn_points: Array[Node3D] = []

## private vars
var _unit_manager: UnitManager
var _spawned_units: Array[Unit] = []
var _spawn_timer: Timer
var _next_spawn_point_index: int = 0

## built-in override methods
func _ready() -> void:
	_unit_manager = $"../UnitManager"
	if not _unit_manager:
		push_warning("No unit manager assigned to building: ", name)
		return
	
	_unit_manager.unit_died.connect(_on_manager_says_unit_died)
	
	_spawn_timer = Timer.new()
	_spawn_timer.wait_time = spawner_data.spawn_cooldown
	_spawn_timer.timeout.connect(_on_spawn_timer_timeout)
	add_child(_spawn_timer)
	
	start_spawning()

## public methods
func start_spawning():
	if _spawn_timer:
		_spawn_timer.start();


func stop_spawning():
	if _spawn_timer:
		_spawn_timer.stop();


func despawn_all_units():
	# TODO change logic to units leaving
	# Outdated _unit_manager logic as well
	for unit in _spawned_units:
		if unit:
			var index = _unit_manager.units.find(unit)
			if index >= 0:
				_unit_manager.kill_unit(index)
	
	_spawned_units.clear()


## private methods
func _attempt_spawning():
	if _spawned_units.size() >= spawner_data.max_units:
		return
	var spawn_pos = _get_next_spawn_position()
	if spawn_pos == Vector3.ZERO:
		push_warning("No valid spawn position for building: ", name)
		return
	
	var unit_data = _unit_manager.spawn_unit(spawner_data.unit_type, team_id, spawn_pos, cached_buffs_calculated, self)
	_spawned_units.append(unit_data)
	_on_unit_spawned(unit_data, spawn_pos)


func _get_next_spawn_position() -> Vector3:
	if spawn_points.is_empty():
		return global_position + Vector3(randf_range(-2, 2), 0, randf_range(-2, 2))
	
	var spawn_point = spawn_points[_next_spawn_point_index]
	_next_spawn_point_index = (_next_spawn_point_index + 1) % spawn_points.size()
	
	return spawn_point.global_position


func _on_manager_says_unit_died(unit: Unit, building):
	if building != self:
		return
	_spawned_units.erase(unit)
	_on_unit_died(unit)


func _on_unit_spawned(unit_data: Unit, position: Vector3):
	# Logic should be put here. Probably a overridable 
	#print(name, " spawned unit at ", position, " (", get_alive_unit_count(), "/", max_units, ")")
	pass


func _on_unit_died(unit) -> void:
	# Logic should be put here. Probably a overridable 
	# Should update UI on building at least
	#print(name, " lost a unit (", get_alive_unit_count(), "/", max_units, " remaining)")
	pass


func _on_spawn_timer_timeout() -> void:
	_attempt_spawning()
