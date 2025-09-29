extends Node3D
## enums
## consts
## exports
## public vars
## private vars
## onready vars
@onready var camera_3d: Camera3D = $Camera3D

## built-in override methods
func _ready() -> void:
	_setup_camera(camera_3d)


func _process(_delta: float) -> void:
	pass


## public methods

## private methods
func _setup_camera(camera: Camera3D) -> void:
	camera.fov = 10.0
	camera.position.y = 3.0
	camera.rotation.x = deg_to_rad(-30.0)
	rotation.y = deg_to_rad(-45.0)
	camera.translate_object_local(Vector3(0.0,0.0,100.0))
