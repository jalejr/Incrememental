extends BaseUnitManager
class_name SoldierManager
## enums
## consts
## exports
@export var patrol_radius: float = 20.0
## public vars
## private vars
## onready vars
## methods to override
func create_unit_instance() -> BaseUnitData:
	return SoldierData.new()


func update_unit_logic(unit: BaseUnitData, delta: float):
	var soldier = unit as SoldierData
	var delta_ms = int(delta * 1000.0)
	
	if soldier.attack_cooldown_remaining_ms > 0:
		soldier.attack_cooldown_remaining_ms -= delta_ms
	
	match soldier.state:
		SoldierData.State.IDLE:
			_soldier_idle_behavior(soldier)
		SoldierData.State.PATROLLING:
			_soldier_patrol_behavior(soldier)
		SoldierData.State.ATTACKING:
			_soldier_attack_behavior(soldier)
	
	super.update_unit_logic(unit, delta)


func on_unit_attacked(position: Vector3):
	print("We hitting")
	# Play melee/ranged hit sound
	# Spawn hit particle
	pass

func on_splash_attack(position: Vector3, radius: float):
	# Play explosion sound
	# Spawn AOE particle effect
	pass

func on_unit_damaged(unit: BaseUnitData, damage: float, source_position: Vector3):
	# Flash red, show damage number, etc.
	print("We hit", damage)
	pass


## built-in override methods
func _ready() -> void:
	super._ready()


func _process(delta: float) -> void:
	super._process(delta)


## public methods

## private methods
func _soldier_idle_behavior(soldier: SoldierData):
	var nearby_enemies = get_nearby_enemies(soldier.position, soldier.stats.attack_range)
	if not nearby_enemies.is_empty():
		soldier.state = SoldierData.State.ATTACKING
		soldier.nav_path.clear()
		soldier.path_index = 0
	else:
		if soldier.spawn_building:
			var patrol_point = soldier.spawn_building.global_position + Vector3(
				randf_range(-patrol_radius, patrol_radius),
				0,
				randf_range(-patrol_radius, patrol_radius)
			)
			_set_unit_path(soldier, patrol_point)
			soldier.state = SoldierData.State.PATROLLING


func _soldier_patrol_behavior(soldier: SoldierData):
	var nearby_enemies = get_nearby_enemies(soldier.position, soldier.stats.attack_range)
	if not nearby_enemies.is_empty():
		soldier.state = SoldierData.State.ATTACKING
		soldier.nav_path.clear()
		soldier.path_index = 0
		return
	
	if soldier.nav_path.is_empty() or soldier.path_index >= soldier.nav_path.size():
		soldier.state = SoldierData.State.IDLE


func _soldier_attack_behavior(soldier: SoldierData):
	var nearby_enemies = get_nearby_enemies(soldier.position, soldier.stats.attack_range)
	if nearby_enemies.is_empty():
		soldier.state = SoldierData.State.IDLE
		return
	
	if soldier.attack_cooldown_remaining_ms > 0:
		return
	
	if soldier.stats.has_splash_damage:
		_perform_splash_attack(soldier)
	else:
		_perform_single_attack(soldier)


func _perform_single_attack(soldier: SoldierData):
	var nearest_enemy_data = grid_manager.get_nearest_unit(
		soldier.position,
		soldier.stats.attack_range,
		team_id
	)
	
	print(nearest_enemy_data)
	
	if not nearest_enemy_data:
		return
	
	if nearest_enemy_data.manager:
		nearest_enemy_data.manager.damage_unit(
			nearest_enemy_data.manager_index,
			soldier.stats.attack_damage,
			soldier.position
		)
		
		soldier.attack_cooldown_remaining_ms = soldier.stats.attack_cooldown_ms
		on_unit_attacked(nearest_enemy_data.position)

func _perform_splash_attack(soldier: SoldierData):
	var primary_target = grid_manager.get_nearest_unit(
		soldier.position,
		soldier.stats.attack_range,
		team_id
	)
	
	if not primary_target:
		return
	
	var splash_targets = grid_manager.get_nearby_units(
		primary_target.position,
		soldier.stats.splash_radius,
		team_id
	)
	
	for target_data in splash_targets:
		if target_data.manager:
			target_data.manager.damage_unit(
				target_data.manager_index,
				soldier.stats.attack_damage,
				soldier.position
			)
	
	soldier.attack_cooldown_remaining_ms = soldier.stats.attack_cooldown_ms
	on_splash_attack(primary_target.position, soldier.stats.splash_radius)
