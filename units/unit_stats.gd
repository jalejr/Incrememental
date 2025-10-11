class_name UnitStats

var radius: float = 0.5
var move_speed: float = 4.0
var max_health: float = 100.0
var attack_damage: float = 500.0
var attack_range: float = 15.0
var has_splash_damage: bool = false
var splash_radius: float = 0.0
var attacks_per_second: float = 1.0
var attack_cooldown_sec: float = 1.0

func _init():
	_recalculate_attack_cooldown()


func set_attack_speed(speed: float):
	attacks_per_second = speed
	_recalculate_attack_cooldown()


func duplicate_stats() -> UnitStats:
	var new_stats: UnitStats = UnitStats.new()
	new_stats.move_speed = move_speed
	new_stats.max_health = max_health
	new_stats.attack_damage = attack_damage
	new_stats.attack_range = attack_range
	new_stats.has_splash_damage = has_splash_damage
	new_stats.splash_radius = splash_radius
	new_stats.set_attack_speed(attacks_per_second)
	return new_stats


func _recalculate_attack_cooldown():
	attack_cooldown_sec = (1.0 / attacks_per_second) if attacks_per_second > 0.0 else 1.0
