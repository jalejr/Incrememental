using Godot;

namespace Incrememental.scripts.global;

/// <summary>
/// Centralized cache of input action StringNames.
/// </summary>
public static class InputAction
{
    // Mouse/Click Actions
    public static readonly StringName LeftClick = "left_click";
    public static readonly StringName RightClick = "right_click";
    
    // Keyboard Actions
    public static readonly StringName Enter = "enter";
    public static readonly StringName Escape = "escape";
    
    // Camera Controls
    public static readonly StringName CameraZoomIn = "camera_zoom_in";
    public static readonly StringName CameraZoomOut = "camera_zoom_out";
    public static readonly StringName RotateLeft = "rotate_left";
    public static readonly StringName RotateRight = "rotate_right";
    
    // Movement (WASD / Arrow Keys)
    public static readonly StringName Left = "left";
    public static readonly StringName Right = "right";
    public static readonly StringName Forward = "forward";
    public static readonly StringName Backward = "backward";
    
    // UI Navigation
    public static readonly StringName UiAccept = "ui_accept";
    public static readonly StringName UiCancel = "ui_cancel";
    
    // Testing Actions (TODO: Remove in production)
    public static readonly StringName Test = "test";
}
