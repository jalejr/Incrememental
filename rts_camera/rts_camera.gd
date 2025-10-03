extends Node3D
## enums
## consts
const CAMERA_PAN_MARGIN: float = 5.0
const ZOOM_BUFFER: float = 25.0
const SPRING_DEAD_ZONE: float = 0.5
const SPRING_STRENGTH: float = 55.0
## exports
@export var camera_pan_speed: float = 10.0
@export var camera_rotate_speed: float = 1.0
@export var camera_zoom_speed: float = 4.0
@export var camera_pan_lerp_speed: float = 10.0
@export var camera_pan_remapped_range: Vector2 = Vector2(0.4, 1.0)
@export var camera_rotate_lerp_speed: float = 10.0
@export var camera_zoom_lerp_speed: float = 10.0
@export var camera_zoom_range: Vector2 = Vector2(50.0, 200.0)
@export var max_camera_zoom_velocity: float = 10

## public vars
## private vars
var _camera_pan_direction: Vector3 = Vector3.ZERO
var _camera_zoom_direction: Vector3 = Vector3.ZERO
var _camera_rotate_direction: Vector3 = Vector3.ZERO
var _initial_camera_y: float
var _initial_camera_z: float
## onready vars
@onready var camera_3d: Camera3D = $Camera3D

## built-in override methods
func _ready() -> void:
	_setup_camera(camera_3d)
	
	_initial_camera_y = camera_3d.position.y
	_initial_camera_z = camera_3d.position.z


func _process(delta: float) -> void:
	get_camera_pan_mouse_direction()
	get_camera_pan_keyboard_direction()
	get_camera_rotate_direction()
	_apply_velocity(delta)


func _input(event: InputEvent) -> void:
	if event.is_action_pressed("camera_zoom_in") and _camera_can_zoom_in():
		_camera_zoom_direction -= Vector3(0.0, 0.0, 1.0)
	if event.is_action_pressed("camera_zoom_out") and _camera_can_zoom_out():
		_camera_zoom_direction += Vector3(0.0, 0.0, 1.0)


## public methods
func get_camera_pan_mouse_direction() -> void:
	if Input.get_mouse_mode() != Input.MOUSE_MODE_CONFINED:
		return
	
	var mouse_pos: Vector2 = get_viewport().get_mouse_position()
	var viewport_size: Vector2 = get_viewport().get_visible_rect().size
	if mouse_pos.x < CAMERA_PAN_MARGIN: _camera_pan_direction.x += -1
	if mouse_pos.y < CAMERA_PAN_MARGIN: _camera_pan_direction.z += -1
	if mouse_pos.x > viewport_size.x - CAMERA_PAN_MARGIN: _camera_pan_direction.x += 1
	if mouse_pos.y > viewport_size.y - CAMERA_PAN_MARGIN: _camera_pan_direction.z += 1


func get_camera_pan_keyboard_direction() -> void:
	var input_direction = Input.get_vector("left", "right", "forward", "backward")
	_camera_pan_direction += Vector3(input_direction.x, 0.0, input_direction.y)


func get_camera_rotate_direction() -> void:
	if Input.is_action_pressed("rotate_left"):
		_camera_rotate_direction -= Vector3(0.0, 1.0, 0.0)
	if Input.is_action_pressed("rotate_right"):
		_camera_rotate_direction += Vector3(0.0, 1.0, 0.0)


func get_camera_pan_velocity() -> Vector3:
	return _camera_pan_direction * camera_pan_speed


func get_camera_zoom_velocity() -> Vector3:
	return _camera_zoom_direction * (camera_zoom_speed * 100)


func get_camera_rotate_velocity() -> Vector3:
	return _camera_rotate_direction * camera_rotate_speed

## private methods
func _setup_camera(camera: Camera3D) -> void:
	camera.fov = 10.0
	camera.position.y = 3.0
	camera.rotation.x = deg_to_rad(-30.0)
	rotation.y = deg_to_rad(-45.0)
	camera.translate_object_local(Vector3(0.0,0.0,100.0))


func _correct_camera_zoom(delta: float) -> void:
	var current_z = camera_3d.position.z
	var min_limit = camera_zoom_range.x - SPRING_DEAD_ZONE
	var max_limit = camera_zoom_range.y + SPRING_DEAD_ZONE
	
	var spring_correction = 0.0
	if current_z < min_limit:
		spring_correction = (min_limit - current_z) * SPRING_STRENGTH * delta
	elif current_z > max_limit:
		spring_correction = (max_limit - current_z) * SPRING_STRENGTH * delta
	
	if spring_correction != 0.0:
		camera_3d.translate_object_local(Vector3(0, 0, spring_correction))

	var hard_correction = 0.0
	if current_z < camera_zoom_range.x - ZOOM_BUFFER:
		hard_correction = (camera_zoom_range.x - ZOOM_BUFFER) - current_z
	elif current_z > camera_zoom_range.y + ZOOM_BUFFER:
		hard_correction = (camera_zoom_range.y + ZOOM_BUFFER) - current_z
	
	if hard_correction != 0.0:
		camera_3d.translate_object_local(Vector3(0, 0, hard_correction))


func _apply_velocity(delta: float) -> void:
	var pan_velocity: Vector3 = get_camera_pan_velocity() * delta
	var zoom_velocity: Vector3 = get_camera_zoom_velocity() * delta
	var rotate_velocity: Vector3 = get_camera_rotate_velocity() * delta
	var remapped_pan_modifier: float = remap(
		camera_3d.position.z,
		camera_zoom_range.x, camera_zoom_range.y,
		camera_pan_remapped_range.x, camera_pan_remapped_range.y
	)
	
	if pan_velocity != Vector3.ZERO:
		translate_object_local(pan_velocity * remapped_pan_modifier)
		
	if zoom_velocity != Vector3.ZERO:
		camera_3d.translate_object_local(zoom_velocity)
	
	if rotate_velocity != Vector3.ZERO:
		global_rotation.y += rotate_velocity.y
	
	_correct_camera_zoom(delta)
	
	_camera_pan_direction = lerp(_camera_pan_direction, Vector3.ZERO, camera_pan_lerp_speed * delta)
	_camera_zoom_direction = lerp(_camera_zoom_direction, Vector3.ZERO, camera_zoom_lerp_speed * delta)
	_camera_rotate_direction = lerp(_camera_rotate_direction, Vector3.ZERO, camera_rotate_lerp_speed * delta)


func _camera_can_zoom_in() -> bool:
	return camera_3d.position.z > camera_zoom_range.x


func _camera_can_zoom_out() -> bool:
	return camera_3d.position.z < camera_zoom_range.y
