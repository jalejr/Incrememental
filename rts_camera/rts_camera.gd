extends Node3D
## enums
## consts
const CAMERA_PAN_MARGIN: float = 5.0 # pixels
## exports
@export var camera_pan_speed: float = 10.0
## public vars
## private vars
var _camera_pan_direction: Vector3 = Vector3.ZERO
## onready vars
@onready var camera_3d: Camera3D = $Camera3D

## built-in override methods
func _ready() -> void:
	_setup_camera(camera_3d)


func _process(delta: float) -> void:
	get_camera_pan_mouse_direction(delta)
	get_camera_pan_keyboard_direction(delta)
	_apply_velocity()


## public methods
func get_camera_pan_mouse_direction(delta: float) -> void:
	if Input.get_mouse_mode() != Input.MOUSE_MODE_CONFINED:
		return
	
	var mouse_pos: Vector2 = get_viewport().get_mouse_position()
	var viewport_size: Vector2 = get_viewport().get_visible_rect().size
	if mouse_pos.x < CAMERA_PAN_MARGIN: _camera_pan_direction.x = -1 * delta
	if mouse_pos.y < CAMERA_PAN_MARGIN: _camera_pan_direction.z = -1 * delta
	if mouse_pos.x > viewport_size.x - CAMERA_PAN_MARGIN: _camera_pan_direction.x = 1 * delta
	if mouse_pos.y > viewport_size.y - CAMERA_PAN_MARGIN: _camera_pan_direction.z = 1 * delta


func get_camera_pan_keyboard_direction(delta: float) -> void:
	var input_direction = Input.get_vector("left", "right", "forward", "backward")
	_camera_pan_direction = Vector3(input_direction.x, 0.0, input_direction.y) * delta

## private methods
func _setup_camera(camera: Camera3D) -> void:
	camera.fov = 10.0
	camera.position.y = 3.0
	camera.rotation.x = deg_to_rad(-30.0)
	rotation.y = deg_to_rad(-45.0)
	camera.translate_object_local(Vector3(0.0,0.0,100.0))


func _apply_velocity() -> void:
	if _camera_pan_direction != Vector3.ZERO:
		var velocity: Vector3 = _camera_pan_direction * camera_pan_speed
		translate_object_local(velocity)
		_camera_pan_direction = Vector3.ZERO
