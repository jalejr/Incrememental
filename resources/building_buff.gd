extends Resource
class_name BuildingBuff

enum Type {
	ATTACK_DAMAGE,
	ATTACK_SPEED,
	MOVEMENT_SPEED,
	HEALTH_REGEN,
	MAX_HEALTH,
	RANGE
}

@export var buff_type: Type = Type.ATTACK_DAMAGE
@export var buff_value: float = 0.1
