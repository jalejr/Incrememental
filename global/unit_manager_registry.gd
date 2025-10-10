extends Node

# private variables
var _managers: Dictionary = {}

# public methods
func register_manager(unit_type: Unit.Type, manager: BaseUnitManager):
	if _managers.has(unit_type):
		push_error("Manager already registered for type: ", unit_type)
		return
	
	_managers[unit_type] = manager
	print("Registered manager for ", Unit.Type.keys()[unit_type])


func get_manager(unit_type: Unit.Type) -> BaseUnitManager:
	if not _managers.has(unit_type):
		push_error("No manager registered for type: ", unit_type)
		return null
	return _managers[unit_type]


func has_manager(unit_type: Unit.Type) -> bool:
	return _managers.has(unit_type)
