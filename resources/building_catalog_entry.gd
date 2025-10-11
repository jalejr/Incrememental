extends Resource
class_name BuildingCatalogEntry

@export var building_data: BuildingData
@export var scene: PackedScene
@export var preview_scene: PackedScene

var building_id: String

func _init() -> void:
	building_id = building_data.building_id
