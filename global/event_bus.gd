extends Node

@warning_ignore("unused_signal")
signal building_placed(building: Node3D, grid_pos: Vector2i)
@warning_ignore("unused_signal")
signal building_removed(building: Node3D)
@warning_ignore("unused_signal")
signal building_upgraded(building: Node3D)
@warning_ignore("unused_signal")
signal building_sold(building: Node3D, grid_pos: Vector2i)
