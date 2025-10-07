extends Node
## enums
## consts
## exports
## public vars
## private vars
## onready vars
@onready var label: Label = $Label
@onready var level: Node3D = $"../Node3D/SubViewportContainer/SubViewport/Level"
## built-in override methods


func _ready() -> void:
	level.money_updated.connect(_update_money_label)


func _process(_delta: float) -> void:
	pass


## public methods

## private methods
func _update_money_label(count: int) -> void:
	label.text = str(count)
