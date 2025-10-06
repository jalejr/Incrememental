extends BaseUnitData
class_name SoldierData

enum State {
	IDLE,
	PATROLLING,
	ATTACKING
}

var state: State = State.IDLE
var attack_cooldown_remaining_ms: int = 0
