extends BaseUnitData
class_name SoldierData

enum State {
	IDLE,
	PATROLLING,
	ATTACKING
}

var state: State = State.IDLE
var attack_cooldown_remaining: float = 0.0
