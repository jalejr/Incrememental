extends Node

signal money_updated(money: int)
## enums
## consts
## exports
@export var money_interval: float = 0.2
## public vars
var money_count: int = 0
## private vars
var _money_timer: Timer
## onready vars
## built-in override methods


func _ready() -> void:
	_money_timer = Timer.new()
	_money_timer.wait_time = money_interval
	_money_timer.timeout.connect(_on_money_timer_timeout)
	add_child(_money_timer)
	start_money_timer()


## public methods
func start_money_timer() -> void:
	_money_timer.start()


func stop_money_timer() -> void:
	_money_timer.stop()

## private methods
func _on_money_timer_timeout() -> void:
	money_count += 1
	money_updated.emit(money_count)
