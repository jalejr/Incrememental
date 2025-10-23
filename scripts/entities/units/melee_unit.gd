class_name MeleeUnit
extends Unit

enum BehaviorState {
	IDLE,
	PURSUE,
	ATTACKING
}

## private vars
var _target_entity: Variant = null
var attack_cooldown: float = 0.0
var behavior_state: BehaviorState = BehaviorState.ATTACKING

## built-in override methods
func _init() -> void:
	path_recalc_interval = 0.5


func update_active_state(delta: float, context: Dictionary) -> void:
	match behavior_state:
		BehaviorState.IDLE:
			pass
		BehaviorState.PURSUE:
			pass
		BehaviorState.ATTACKING:
			update_attacking_state(delta, context)

func update_idle_state(delta: float, context: Dictionary):
	pass


func update_pursue_state(delta: float, context: Dictionary):
	pass


func update_attacking_state(delta: float, context: Dictionary):
	path_age += delta
	attack_cooldown = attack_cooldown - delta
	
	if is_valid_target(_target_entity):
		var current_distance = position.distance_to(_target_entity.entity_data.position)
		
		if current_distance <= stats.attack_range:
			nav_path.clear()
			
			if attack_cooldown <= 0:
				_target_entity.take_damage(stats.attack_damage, position, context)
				attack_cooldown = stats.attack_cooldown_sec
			return
	
	var nearby_enemy = context.find_nearest_enemy.call(self, stats.attack_range)
	
	if nearby_enemy and nearby_enemy.is_alive:
		_target_entity = nearby_enemy
		nav_path.clear()
		
		if attack_cooldown <= 0:
			_target_entity.take_damage(stats.attack_damage, position, context)
			attack_cooldown = stats.attack_cooldown_sec
		return
	
	if not is_valid_target(_target_entity):
		_target_entity = context.find_nearest_enemy.call(self, 25)
		if _target_entity:
			path_age = 999.0
	
	if not is_valid_target(_target_entity):
		nav_path.clear()
		return
	
	var target_pos = _target_entity.entity_data.position
	
	if needs_path_recalc(target_pos):
		context.set_path.call(self, target_pos)
		mark_path_recalculated(target_pos)
	elif nav_path.is_empty() or path_index >= nav_path.size():
		if path_age > path_recalc_interval:
			context.set_path.call(self, target_pos)
			mark_path_recalculated(target_pos)


# TODO this shouldn't exist. The spatial query shoould handle checking is_targetable/is_attackable
# to determine if it should even be sent to the unit to do anything with
func is_valid_target(target_entity) -> bool:
	return target_entity and target_entity.is_alive and not target_entity.is_dying
