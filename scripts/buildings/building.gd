extends Node3D
class_name Building

signal health_changed(new_health: float, max_health: float)

## public vars
# Placement vars
var building_data: BuildingData
var grid_position: Vector2i

# Combat vars
var team_id: int = 0
var max_health: float = 100.0
var health: float = 100.0
var radius: float = 0.5
var adjacent_aura_buffs: Array[Buff] = []
var active_buffs: Dictionary[Building, Array] = {}
var cached_buff_multiplier: Dictionary[Buff, float] = {}

## built-in override methods
func _ready() -> void:
	_initialize_from_data()
	_placed()

## public methods
func set_data(data: BuildingData):
	building_data = data


func damage(amount: float):
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
func _initialize_from_data():
	max_health = building_data.max_health
	health = max_health
	radius = building_data.radius
	team_id = building_data.team_id
	adjacent_aura_buffs = building_data.adjacent_aura_buffs.duplicate()


func _find_nearby_buildings(cell_radius: int) -> Array[Building]:
	var found_buildings = []
	# TODO logic here prob need to rework placement grid a bit
	return found_buildings


func _placed():
	var nearby_buildings = _find_nearby_buildings(building_data.buff_radius)
	for building in nearby_buildings:
		add_adjacency_buffs(self, adjacent_aura_buffs)
	
	EventBus.building_placed.emit(self)


func _killed():
	var nearby_buildings = _find_nearby_buildings(building_data.buff_radius)
	for building in nearby_buildings:
		remove_adjacency_buffs(self)
	
	# TODO do some anim work here


func _destroy():
	# TODO do some other necessary stuff but i think this is it and just called from anim
	EventBus.building_removed.emit(self)
	queue_free()


func _update_active_buffs():
	# TODO We need to calc the buffs changes
	# Need to update any UI that needs to know about this through signal probably
	pass
