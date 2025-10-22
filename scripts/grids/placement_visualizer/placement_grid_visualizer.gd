extends Node3D
class_name PlacementGridVisualizer

## exports
@export var placement_grid: BuildingGridManager
@export var unlocked_cell_color: Color = Color(0.2, 0.8, 0.2, 0.3)
@export var occupied_cell_color: Color = Color(0.5, 0.5, 0.5, 0.4)
@export var valid_placement_color: Color = Color(0.2, 1.0, 0.2, 0.5)
@export var invalid_placement_color: Color = Color(1.0, 0.2, 0.2, 0.5)

## private vars
var _cell_mesh: MultiMeshInstance3D
var _preview_meshes: Array[MeshInstance3D] = []
var _current_preview_size: Vector2i = Vector2i.ZERO

## built-in override methods
func _ready() -> void:
	if not placement_grid:
		push_error("PlacementGridVisualizer needs Buildings")
	
	_setup_grid_mesh()
	_connect_signals()
	call_deferred("_update_all_cells")


## public methods
func show_placement_preview(world_pos: Vector3, building_size: Vector2i, is_valid: bool):
	if building_size != _current_preview_size:
		_create_preview_meshes(building_size)
		_current_preview_size = building_size
	
	var grid_pos = placement_grid.world_to_grid(world_pos)
	var color = valid_placement_color if is_valid else invalid_placement_color
	
	# Use helper function to get cells
	var cells = placement_grid.get_cells_for_area(grid_pos, building_size)
	
	for i in range(cells.size()):
		var cell_world_pos = placement_grid.grid_to_world(cells[i], true)
		cell_world_pos.y = 0.02
		
		_preview_meshes[i].global_position = cell_world_pos
		_preview_meshes[i].visible = true
		
		var mat = _preview_meshes[i].get_surface_override_material(0) as StandardMaterial3D
		mat.albedo_color = color


func hide_placement_preview():
	for mesh in _preview_meshes:
		mesh.visible = false


## private methods
func _create_preview_meshes(building_size: Vector2i):
	for mesh in _preview_meshes:
		mesh.queue_free()
	_preview_meshes.clear()
	
	var count = building_size.x * building_size.y
	for i in range(count):
		var mesh_instance = MeshInstance3D.new()
		add_child(mesh_instance)
		
		var quad = PlaneMesh.new()
		quad.size = Vector2(
			placement_grid.grid_cell_size * 0.9,
			placement_grid.grid_cell_size * 0.9
		)
		mesh_instance.mesh = quad
		
		var mat = StandardMaterial3D.new()
		mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
		mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
		mat.cull_mode = BaseMaterial3D.CULL_DISABLED
		mesh_instance.set_surface_override_material(0, mat)
		
		mesh_instance.visible = false
		_preview_meshes.append(mesh_instance)


func _update_all_cells():
	var unlocked_cells = placement_grid.get_unlocked_cells()
	
	if unlocked_cells.is_empty():
		_cell_mesh.multimesh.instance_count = 0
		return
	
	_cell_mesh.multimesh.instance_count = unlocked_cells.size()
	
	for i in range(unlocked_cells.size()):
		var cell = unlocked_cells[i]
		var world_pos = placement_grid.grid_to_world(cell, true)
		world_pos.y = 0.01  # Slightly above ground
		
		var instance_transform = Transform3D(Basis(), world_pos)
		_cell_mesh.multimesh.set_instance_transform(i, instance_transform)
		
		var color = unlocked_cell_color
		if placement_grid.is_cell_occupied(cell):
			color = occupied_cell_color
		
		_cell_mesh.multimesh.set_instance_color(i, color)


func _setup_grid_mesh():
	_cell_mesh = MultiMeshInstance3D.new()
	add_child(_cell_mesh)
	
	var quad_mesh = PlaneMesh.new()
	quad_mesh.size = Vector2(
		placement_grid.grid_cell_size * 0.95,
		placement_grid.grid_cell_size * 0.95
	)
	
	var multi_mesh = MultiMesh.new()
	multi_mesh.transform_format = MultiMesh.TRANSFORM_3D
	multi_mesh.use_colors = true
	multi_mesh.mesh = quad_mesh
	multi_mesh.instance_count = 0
	
	_cell_mesh.multimesh = multi_mesh
	
	var material = StandardMaterial3D.new()
	material.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	material.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	material.cull_mode = BaseMaterial3D.CULL_DISABLED
	material.vertex_color_use_as_albedo = true
	_cell_mesh.material_override = material


func _connect_signals():
	# Listen to EventBus - single source of truth
	EventBus.building_placed.connect(_on_building_placed)
	EventBus.building_sold.connect(_on_building_removed)


func _on_building_placed(_building: Variant, _grid_pos: Vector2i):
	_update_all_cells()


func _on_building_removed(_building: Variant):
	_update_all_cells()
