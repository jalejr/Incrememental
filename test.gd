extends Node3D

## Simple test script to spawn units and make them move around

@onready var unit_manager = $SubViewportContainer/SubViewport/BaseUnitManager
@onready var nav_region = $SubViewportContainer/SubViewport/NavigationRegion3D

@export var test_unit_count: int = 100
@export var spawn_area_size: float = 20.0
@export var move_area_size: float = 30.0

var spawned_units: Array = []

func _ready():
	# Give navigation a frame to setup
	await get_tree().process_frame
	
	print("Spawning ", test_unit_count, " test units...")
	spawn_test_units()
	
	# Give units random destinations every few seconds
	start_random_movement()
	
	var floor = $SubViewportContainer/SubViewport/NavigationRegion3D/CSGBox3D
	if floor:
		print("Floor found: ", floor.name)
		print("Floor use_collision: ", floor.use_collision)
		print("Floor position: ", floor.global_position)
		print("Floor size: ", floor.size)
	else:
		print("ERROR: Floor not found!")

func spawn_test_units():
	for i in range(test_unit_count):
		# Random spawn position
		var spawn_pos = Vector3(
			randf_range(-spawn_area_size, spawn_area_size),
			0,
			randf_range(-spawn_area_size, spawn_area_size)
		)
		
		var unit = unit_manager.spawn_unit(spawn_pos)
		spawned_units.append(unit)
	
	print("Spawned ", spawned_units.size(), " units successfully!")

func start_random_movement():
	# Every 3 seconds, give all units new random destinations
	while true:
		await get_tree().create_timer(10.0).timeout
		
		print("Assigning new random paths...")
		for unit in spawned_units:
			var target = get_random_position()
			unit_manager._set_unit_path(unit, target)

func get_random_position() -> Vector3:
	return Vector3(
		randf_range(-move_area_size, move_area_size),
		0,
		randf_range(-move_area_size, move_area_size)
	)

func _input(event):
	# Press SPACE to spawn 10 more units
	if event.is_action_pressed("ui_accept"):
		print("Spawning 10 more units...")
		for i in range(10):
			var pos = get_random_position()
			var unit = unit_manager.spawn_unit(pos)
			spawned_units.append(unit)
	
	# Press G to print grid stats
	if event is InputEventKey and event.pressed and event.keycode == KEY_G:
		unit_manager.grid_manager.print_stats()
		print(Engine.get_frames_per_second())
	
	# Click to send nearest unit to clicked position
	if event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
		handle_click(event.position)


func handle_click(screen_position: Vector2):
	"""Handle click with SubViewport for rendering, main world for physics"""
	var camera = $SubViewportContainer/SubViewport/RtsCamera/Camera3D
	if not camera:
		print("No camera assigned! Set it in the inspector.")
		return
	
	# Transform screen position to SubViewport coordinates
	var viewport_position = screen_position
	
	var sub_viewport = $SubViewportContainer/SubViewport
	if sub_viewport:
		# Get viewport and window sizes
		var viewport_size = Vector2(sub_viewport.size)
		var window_size = get_viewport().get_visible_rect().size
		
		# Scale mouse position to viewport coordinates
		viewport_position = screen_position * (viewport_size / window_size)
		
		print("Window size: ", window_size)
		print("Viewport size: ", viewport_size)
		print("Screen pos: ", screen_position, " -> Viewport pos: ", viewport_position)
	
	# Raycast using the camera
	var from = camera.project_ray_origin(viewport_position)
	var to = from + camera.project_ray_normal(viewport_position) * 1000
	
	print("Ray from: ", from, " direction: ", (to - from).normalized())
	
	# Use MAIN viewport's physics world (not SubViewport's)
	var world = get_viewport().get_world_3d()
	if not world:
		print("No World3D available!")
		return
	
	var space_state = world.direct_space_state
	if not space_state:
		print("No physics space available!")
		return
	
	# Perform raycast
	var query = PhysicsRayQueryParameters3D.create(from, to)
	query.collide_with_areas = false
	query.collide_with_bodies = true
	query.collision_mask = 0xFFFFFFFF
	
	var result = space_state.intersect_ray(query)
	
	if result:
		print("HIT! Position: ", result.position)
		if not spawned_units.is_empty():
			var nearest_unit = spawned_units[0]
			unit_manager._set_unit_path(nearest_unit, result.position)
			print("Sending unit to: ", result.position)
	else:
		print("MISS - Raycast didn't hit anything!")
