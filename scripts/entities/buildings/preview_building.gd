extends Node3D

## exports
@export var valid_material: StandardMaterial3D
@export var invalid_material: StandardMaterial3D

## public methods
func set_valid():
	_apply_material_to_meshes(valid_material)


func set_invalid():
	_apply_material_to_meshes(invalid_material)

## private methods
func _apply_material_to_meshes(material: Material):
	for child in get_children():
		if child is MeshInstance3D:
			child.material_override = material
