class_name Unit
extends RefCounted

enum Type {
	BASE,
	SOLDIER,
	GATHERER,
	MINER,
	
	DEMON,
	TROLL,
}

# Core data
var manager_index: int
var entity_data: EntityData
var unit_type: Type
var visual_position: Vector3
var velocity: Vector3
var stats: UnitStats
# In-process of cleaning up
var is_dying: bool = false
var death_timer: float = 0.0
var death_duration: float = 1.5
# Spawner building gone so disappearing
var is_homeless: bool = false
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
# Caching for optimization
var cached_runtime: UnitManager.UnitTypeRuntimeData = null
# Delegating to entity_data
var position: Vector3:
	get: return entity_data.position if entity_data else Vector3.ZERO
	set(value):
		if entity_data: entity_data.position = value
var radius: float:
	get: return entity_data.radius if entity_data else 0.5
	set(value):
		if entity_data: entity_data.radius = value
var max_health: float:
	get: return entity_data.max_health if entity_data else 0.0
	set(value):
		if entity_data: entity_data.max_health = value
var health: float:
	get: return entity_data.health if entity_data else 0.0
	set(value):
		if entity_data: entity_data.health = value
var team_id: EntityData.Team:
	get: return entity_data.team_id if entity_data else EntityData.Team.NONE
	set(value):
		if entity_data: entity_data.team_id = value
var is_alive: bool:
	get: return entity_data.is_alive if entity_data else false
	set(value):
		if entity_data: entity_data.is_alive = value
var is_targetable: bool:
	get: return entity_data.is_targetable if entity_data else true
	set(value):
		if entity_data: entity_data.is_targetable = value
var is_attackable: bool:
	get: return entity_data.is_attackable if entity_data else true
	set(value):
		if entity_data: entity_data.is_attackable = value


func update_logic(delta: float, context: Dictionary) -> void:
	if is_dying:
		death_timer += delta
		
		if death_timer >= death_duration:
			context.destroy_queue.append(self)
	
	if not is_alive:
		return


func get_custom_visual_data() -> Color:
	return Color()


func take_damage(damage: float, source_pos: Vector3, context: Dictionary):
	@warning_ignore("narrowing_conversion")
	context.damage_queue[self] = maxi(1, damage - stats.armor)
	show_hit_effect(source_pos)


# TODO create global particle emitter and calc "normal" or backside of object with this source_pos
func show_hit_effect(_source_pos: Vector3):
	pass


func start_dying():
	is_dying = true


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
