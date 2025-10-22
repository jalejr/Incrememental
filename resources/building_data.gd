extends Resource
class_name BuildingData

@export_group("Identity")
@export var building_id: String = "name"
@export var display_name: String = "Name"
@export var description: String = "Populate with real description"
@export var icon: Texture2D

@export_group("Placement")
@export var grid_size: Vector2i = Vector2i(2, 2)
@export var unlock_radius: int = 1
@export var buff_radius: int = 1
@export var placement_cost: int = 50

@export_group("Combat")
@export var max_health: float = 100.0
@export var team_id: EntityData.Team
@export var radius: float = 2.0

@export_group("Adjacency Buffs")
@export var adjacent_aura_buffs: Array[Buff] = []

#TODO Add these in later
#@export_group("Upgrades")
#@export var upgrade_paths: Array[BuildingUpgradePath] = []

@export_group("Meta Progression")
@export var requires_research_id: String = ""

func is_unlocked() -> bool:
	if requires_research_id != "":
		# TODO do some logic here
		return false
	return true
