class_name EntityData
extends RefCounted
## enums
enum Type {
	UNDEFINED,
	UNIT,
	BUILDING
}

enum Team {
	NONE,
	PLAYER,
	ENEMY
}

## public vars
var position: Vector3
var radius: float
var team_id: Team
var type: Type = Type.UNDEFINED
var is_targetable: bool = true
var is_attackable: bool = true
var is_alive: bool = true

var health: float
var max_health: float
