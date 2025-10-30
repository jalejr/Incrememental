extends Node
class_name BuildingPlacementController

## exports
@export var placement_grid: BuildingGridManager
@export var grid_visualizer: PlacementGridVisualizer
@export var camera: Camera3D
@export var test_scene: PackedScene

# TODO remove these they are for testing
const BARRACKS_CATALOG_ENTRY = preload("uid://cycjr7js35a5e")

## private vars
var _selected_catalog_entry: BuildingCatalogEntry
var _preview_position: Vector3
var _is_placing: bool = false
var _preview_valid: bool = false

## built-in override methods
func _process(_delta: float) -> void:
	if Input.is_action_just_pressed("test"):
		#TODO remove this when done testing and use real data

		start_placement(BARRACKS_CATALOG_ENTRY)
	
	if not _is_placing:
		return
	
	_update_preview()
	
	if Input.is_action_just_pressed("ui_accept"):
		_try_placing_building()
	
	if Input.is_action_just_pressed("ui_cancel"):
		cancel_placement()


## public methods
func start_placement(catalog_entry: BuildingCatalogEntry):
	_is_placing = true
	_selected_catalog_entry = catalog_entry
	grid_visualizer.show_placement_preview(Vector3.ZERO, catalog_entry.building_data.grid_size, false)


func cancel_placement():
	_is_placing = false
	_selected_catalog_entry = null
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
		_preview_valid = placement_grid.can_place_building(grid_pos, _selected_catalog_entry.building_data.grid_size)
		grid_visualizer.show_placement_preview(_preview_position, _selected_catalog_entry.building_data.grid_size, _preview_valid)


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
		_selected_catalog_entry.building_data.grid_size
	)
	
	var building = _selected_catalog_entry.scene.instantiate()
	get_parent().add_child(building)
	building.global_position = world_pos
	
	var building_data = placement_grid.place_building(
		building,
		grid_pos,
		_selected_catalog_entry.building_data.grid_size,
		_selected_catalog_entry.building_data.unlock_radius
	)
	
	if not building_data:
		#RunEconomyManager.add_currency(selected_building_config.placement_cost)
		building.queue_free()
