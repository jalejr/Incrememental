class_name UnitData
extends RefCounted

# Core data
var unit_type: Unit.Type
var position: Vector3
var visual_position: Vector3
var radius: float
var velocity: Vector3
var health: float
var stats: UnitStats
var team_id: int

var is_alive: bool
var is_dying: bool

var is_targetable: bool
var is_attackable: bool

var cached_target_position: Vector3 = Vector3.ZERO
var path_age: float = 0.0
var path_recalc_interval: float = 0.5

# Navigation
var agent_rid: RID
var nav_path: PackedVector3Array = []
var path_index: int = 0

# Grid tracking
var grid_data: SpatialGridUnit
var spawn_building: Node = null

func update_logic(_delta: float, _context: Dictionary) -> void:
	pass


func get_custom_visual_data() -> Color:
	return Color()


func needs_path_recalc(target_position: Vector3, max_age: float = -1.0, max_drift: float = 5.0) -> bool:
	if max_age < 0:
		max_age = path_recalc_interval

	if nav_path.is_empty():
		return true

	if path_age > max_age:
		return true

	if cached_target_position.distance_to(target_position) > max_drift:
		return true

	return false

func mark_path_recalculated(target_position: Vector3):
	cached_target_position = target_position
	path_age = 0.0
