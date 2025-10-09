extends Node
## enums
## consts
## exports
## public vars
## private vars
## onready vars
## built-in override methods


func _ready() -> void:
	Input.set_mouse_mode(Input.MOUSE_MODE_CONFINED)


func _unhandled_input(_event: InputEvent) -> void:
	if (Input.is_action_just_released("enter") or 
			Input.is_action_just_released("left_click")):
		get_viewport().set_input_as_handled()
		Input.set_mouse_mode(Input.MOUSE_MODE_CONFINED)
		print(Engine.get_frames_per_second())
	
	if Input.is_action_just_released("escape"):
		get_viewport().set_input_as_handled()
		if Input.get_mouse_mode() == Input.MOUSE_MODE_CONFINED:
			Input.set_mouse_mode(Input.MOUSE_MODE_VISIBLE)
		else:
			get_tree().quit()


func _process(_delta: float) -> void:
	pass


## public methods

## private methods
