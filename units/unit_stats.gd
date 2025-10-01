class_name UnitStats

var move_speed: float = 4.0
var max_health: float = 100.0
var attack_damage: float = 10.0
var attack_range: float = 3.0
var has_splash_damage: bool = false
var splash_radius: float = 0.0
var attacks_per_second: float = 1.0

func duplicate_stats() -> UnitStats:
	var new_stats: UnitStats = UnitStats.new()
	new_stats.move_speed = move_speed
	new_stats.max_health = max_health
	new_stats.attack_damage = attack_damage
	new_stats.attack_range = attack_range
	new_stats.has_splash_damage = has_splash_damage
	new_stats.splash_radius = splash_radius
	new_stats.attacks_per_second = attacks_per_second
	return new_stats
