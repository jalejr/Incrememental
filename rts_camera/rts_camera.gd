extends Node3D
## enums
## consts
const CAMERA_PAN_MARGIN: float = 5.0 # pixels
## exports
@export var camera_pan_speed: float = 10.0
@export var camera_rotate_speed: float = 1.0
@export var camera_zoom_speed: float = 4.0
@export var camera_pan_lerp_speed: float = 10.0
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
	_apply_corrective_input()
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


func _apply_corrective_input() -> void:
	print(camera_3d.position)
	#var target_position
	#target_position = clamp(camera_3d.position.z, camera_zoom_range.x, camera_zoom_range.y)
	#camera_3d.position.z = lerp(camera_3d.position.z, target_position, camera_zoom_lerp_speed * get_process_delta_time())


func _apply_velocity(delta: float) -> void:
	var pan_velocity: Vector3 = get_camera_pan_velocity() * delta
	var zoom_velocity: Vector3 = get_camera_zoom_velocity() * delta
	var rotate_velocity: Vector3 = get_camera_rotate_velocity() * delta
	
	if pan_velocity != Vector3.ZERO:
		translate_object_local(pan_velocity)
		
	if zoom_velocity != Vector3.ZERO:
		var future_z = camera_3d.position.z + zoom_velocity.z * cos(camera_3d.rotation.x)
		
		# If it would exceed bounds, scale down the velocity
		if future_z < camera_zoom_range.x:
			var allowed_delta = camera_zoom_range.x - camera_3d.position.z
			zoom_velocity.z = allowed_delta / cos(camera_3d.rotation.x)
		elif future_z > camera_zoom_range.y:
			var allowed_delta = camera_zoom_range.y - camera_3d.position.z
			zoom_velocity.z = allowed_delta / cos(camera_3d.rotation.x)
		
		camera_3d.translate_object_local(zoom_velocity)
	
	if rotate_velocity != Vector3.ZERO:
		global_rotation.y += rotate_velocity.y
	
	_camera_pan_direction = lerp(_camera_pan_direction, Vector3.ZERO, camera_pan_lerp_speed * delta)
	_camera_zoom_direction = lerp(_camera_zoom_direction, Vector3.ZERO, camera_zoom_lerp_speed * delta)
	_camera_rotate_direction = lerp(_camera_rotate_direction, Vector3.ZERO, camera_rotate_lerp_speed * delta)


func _camera_can_zoom_in() -> bool:
	if camera_3d.position.z > camera_zoom_range.x:
		return true
	return false


func _camera_can_zoom_out() -> bool:
	if camera_3d.position.z < camera_zoom_range.y:
		return true
	return false
