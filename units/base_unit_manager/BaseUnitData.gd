class_name BaseUnitData

var position: Vector3
var visual_position: Vector3
var velocity: Vector3
var health: float
var stats: UnitStats
var nav_path: PackedVector3Array = []
var path_index: int = 0
var agent_rid: RID
var grid_data: UnitGridData
var spawn_building: Node = null
