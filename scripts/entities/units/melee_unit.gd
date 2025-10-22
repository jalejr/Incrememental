class_name MeleeUnit
extends Unit

## private vars
var _target_entity: Variant = null
var attack_cooldown: float = 0.0

## built-in override methods
func _init() -> void:
	path_recalc_interval = 0.5


func update_logic(delta: float, context: Dictionary) -> void:
	path_age += delta
	attack_cooldown = maxf(0, attack_cooldown - delta)
	
	if not _target_entity:
		_target_entity = context.find_nearest_enemy.call(self, 25)
		
		if _target_entity:
			path_age = 999.0
	
	if not _target_entity:
		nav_path.clear()
		return
	
	var target_pos = _target_entity.entity_data.position
	var distance = position.distance_to(target_pos)
	
	if distance <= stats.attack_range:
		if attack_cooldown <= 0:
			_target_entity.take_damage(stats.attack_damage, position, context)
			attack_cooldown = stats.attack_cooldown_sec
		nav_path.clear()
	else:
		if needs_path_recalc(target_pos):
			context.set_path.call(self, target_pos)
			mark_path_recalculated(target_pos)
			
