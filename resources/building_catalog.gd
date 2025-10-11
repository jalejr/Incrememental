extends Resource
class_name BuildingCatalog

@export var entries: Dictionary[String, BuildingCatalogEntry] = {}

func get_entry_by_id(building_id: String) -> BuildingCatalogEntry:
	var entry = entries.get(building_id, null)
	
	if entry:
		return entry
	
	push_error("No building found with ID: ", building_id)
	return null


func get_available_entries() -> Array[BuildingCatalogEntry]:
	var available: Array[BuildingCatalogEntry] = []
	
	for entry in entries.values():
		available.append(entry)
	
	return available
