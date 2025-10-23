extends Node3D
class_name Building

signal health_changed(new_health: float, max_health: float)
## export vars
@export var building_data: BuildingData

## public vars
var entity_data: EntityData
var adjacent_aura_buffs: Array[Buff] = []
var active_buffs: Dictionary[Building, Array] = {}
var cached_buffs_calculated: Dictionary[Buff.Type, float] = {}

# TODO hacky stuff should just use interface in C#
var is_dying = false

# Delegating to entity_data
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

## built-in override methods
func _ready() -> void:
	_initialize()
	_placed()

## public methods
func set_data(data: BuildingData):
	building_data = data


func take_damage(amount: float, _position: Vector3, _context: Dictionary):
	health -= amount
	health = max(0, health)
	health_changed.emit(health, max_health)
	
	if health <= 0:
		_killed()


func heal(amount: float):
	health += amount
	health = min(health, max_health)
	health_changed.emit(health, max_health)


func add_adjacency_buffs(source_building: Building, adjacency_buffs: Array[Buff]):
	active_buffs[source_building] = adjacency_buffs
	_update_active_buffs()


func remove_adjacency_buffs(source_building: Building):
	active_buffs.erase(source_building)
	_update_active_buffs()

## private methods
func _initialize():
	entity_data = EntityData.new()
	entity_data.type = EntityData.Type.BUILDING
	entity_data.position = global_position
	radius = building_data.radius
	max_health = building_data.max_health
	health = max_health
	team_id = building_data.team_id
	entity_data.is_alive = true
	entity_data.is_attackable = true
	entity_data.is_targetable = true
	adjacent_aura_buffs = building_data.adjacent_aura_buffs.duplicate()


func _find_nearby_buildings(cell_radius: int) -> Array[Building]:
	var found_buildings: Array[Building] = []
	# TODO logic here prob need to rework placement grid a bit
	return found_buildings


func _placed():
	var nearby_buildings = _find_nearby_buildings(building_data.buff_radius)
	for building in nearby_buildings:
		add_adjacency_buffs(self, adjacent_aura_buffs)


func _killed():
	var nearby_buildings = _find_nearby_buildings(building_data.buff_radius)
	for building in nearby_buildings:
		remove_adjacency_buffs(self)
	
	# TODO do some anim work here
	_destroy()


func _destroy():
	# TODO do some other necessary stuff but i think this is it and just called from anim
	EventBus.building_removed.emit(self)
	queue_free()


func _update_active_buffs():
	# TODO We need to calc the buffs changes
	# Need to update any UI that needs to know about this through signal probably
	pass
