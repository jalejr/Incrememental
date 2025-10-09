extends Node
class_name BuildingPlacementController

## exports
@export var placement_grid: BuildingGridManager
@export var grid_visualizer: BuildingGridVisualizer
@export var camera: Camera3D
@export var test_scene: PackedScene

## private vars
var _selected_building_config: BuildingConfig
var _preview_position: Vector3
var _is_placing: bool = false
var _preview_valid: bool = false

## built-in override methods
func _process(_delta: float) -> void:
	if Input.is_action_just_pressed("test"):
		#TODO remove this when done testing and use real data
		var building_config = BuildingConfig.new()
		building_config.scene = test_scene
		building_config.grid_size = Vector2i(2,3)
		building_config.placement_cost = 5
		building_config.unlock_radius = 5
		start_placement(building_config)
	
	if not _is_placing:
		return
	
	_update_preview()
	
	if Input.is_action_just_pressed("ui_accept"):
		_try_placing_building()
	
	if Input.is_action_just_pressed("ui_cancel"):
		cancel_placement()


## public methods
func start_placement(config: BuildingConfig):
	_is_placing = true
	_selected_building_config = config
	grid_visualizer.show_placement_preview(Vector3.ZERO, config.grid_size, false)


func cancel_placement():
	_is_placing = false
	_selected_building_config = null
	grid_visualizer.hide_placement_preview()


## private methods
func _update_preview():
	var mouse_pos = get_viewport().get_mouse_position()
	var from = camera.project_ray_origin(mouse_pos)
	var to = from + camera.project_ray_normal(mouse_pos) * 1000.0
	
	var plane = Plane(Vector3.UP, 0)
	var intersection = plane.intersects_ray(from, to - from)
	
	if intersection:
		_preview_position = intersection
		var grid_pos = placement_grid.world_to_grid(_preview_position)
		_preview_valid = placement_grid.can_place_building(grid_pos, _selected_building_config.grid_size)
		grid_visualizer.show_placement_preview(_preview_position, _selected_building_config.grid_size, _preview_valid)


func _try_placing_building():
	if not _preview_valid:
		return
	
	#if not RunEconomyManager.can_afford(_selected_building_config.placement_cost):
		#print("Cannot afford building!")
		#return
	#
	#if not RunEconomyManager.spend_currency(_selected_building_config.placement_cost):
		#return
	
	var grid_pos = placement_grid.world_to_grid(_preview_position)
	var world_pos = placement_grid.get_placement_preview_position(
		_preview_position,
		_selected_building_config.grid_size
	)
	
	var building = _selected_building_config.scene.instantiate()
	get_tree().root.add_child(building)
	building.global_position = world_pos
	
	var building_data = placement_grid.place_building(
		building,
		grid_pos,
		_selected_building_config.grid_size,
		_selected_building_config.unlock_radius
	)
	
	if not building_data:
		#RunEconomyManager.add_currency(selected_building_config.placement_cost)
		building.queue_free()
