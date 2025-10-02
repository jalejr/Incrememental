extends Node3D
class_name UnitSpawnerBuilding
## enums
## consts
## exports
@export_group("Unit Spawning")
@export var unit_manager: BaseUnitManager
@export var max_units: int = 10
@export var spawn_interval: float = 3.0
@export var auto_spawn: bool = true

@export_group("Spawn Positions")
@export var spawn_points: Array[Node3D] = []

## public vars
## private vars
var _spawned_units: Array = []  # Using UnitStats to hold for smaller
var _spawn_timer: Timer
var _next_spawn_point_index: int = 0

## onready vars
## built-in override methods


func _ready() -> void:
	if not unit_manager:
		push_warning("No unit manager assigned to building: ", name)
		return
	
	unit_manager.unit_died.connect(_on_manager_says_unit_died)
	
	_spawn_timer = Timer.new()
	_spawn_timer.wait_time = spawn_interval
	_spawn_timer.timeout.connect(_on_spawn_timer_timeout)
	add_child(_spawn_timer)
	
	if auto_spawn:
		start_spawning()


## public methods
func start_spawning():
	if _spawn_timer:
		_spawn_timer.start();


func stop_spawning():
	if _spawn_timer:
		_spawn_timer.stop();


func despawn_all_units():
	for unit in _spawned_units:
		if unit:
			var index = unit_manager.units.find(unit)
			if index >= 0:
				unit_manager.kill_unit(index)
	
	_spawned_units.clear()


## private methods
func _attempt_spawning():
	if _spawned_units.size() >= max_units:
		return

	var spawn_pos = get_next_spawn_position()
	if spawn_pos == Vector3.ZERO:
		push_warning("No valid spawn position for building: ", name)
		return
	
	var unit_stats: UnitStats = _get_unit_stats()
	
	if unit_stats:
		_spawned_units.append(unit_stats)
		unit_manager.spawn_unit(spawn_pos, unit_stats, self)
		_on_unit_spawned(unit_stats, spawn_pos)


func get_next_spawn_position() -> Vector3:
	if spawn_points.is_empty():
		return global_position + Vector3(randf_range(-2, 2), 0, randf_range(-2, 2))
	
	var spawn_point = spawn_points[_next_spawn_point_index]
	_next_spawn_point_index = (_next_spawn_point_index + 1) % spawn_points.size()
	
	return spawn_point.global_position


func _get_unit_stats() -> UnitStats:
	var stats = unit_manager.get_default_stats()

	return stats


func get_alive_unit_count() -> int:
	return _spawned_units.size()


func get_available_capacity() -> int:
	return max_units - get_alive_unit_count()


func _force_spawn_now():
	_attempt_spawning()


func _on_unit_spawned(unit_stats: UnitStats, position: Vector3):
	# Logic should be put here. Probably a overridable 
	print(name, " spawned unit at ", position, " (", get_alive_unit_count(), "/", max_units, ")")


func _on_unit_died(unit) -> void:
	# Logic should be put here. Probably a overridable 
	print(name, " lost a unit (", get_alive_unit_count(), "/", max_units, " remaining)")


func _on_upgrade_applied(upgrade_type: String, value: float):
	# Logic should be put here. Probably a overridable 
	print(name, " upgraded: ", upgrade_type, " +", value)


func _on_manager_says_unit_died(unit, building):
	if building != self:
		return
	_spawned_units.erase(unit)
	_on_unit_died(unit)


func _on_spawn_timer_timeout() -> void:
	_attempt_spawning()
