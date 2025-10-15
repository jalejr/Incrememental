extends Building
class_name HQBuilding

## exports
@export var income_rate: float = 1.0
@export var timer_wait_time: float = 1.0

## onready vars
@onready var timer = $Timer

## built-in override methods
func _ready() -> void:
	super._ready()
	
	timer.start()

## public methods
func open_research_ui():
	#TODO research logic here
	pass

## private methods
func _on_timer_timeout() -> void:
	# TODO money logic here
	pass
