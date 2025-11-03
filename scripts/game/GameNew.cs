using Godot;
using Incrememental.scripts.global;

namespace Incrememental.scripts.game;

/// <summary>
/// Main game controller managing input, mouse modes, and application lifecycle.
/// </summary>
[GlobalClass]
public partial class GameNew : Node
{
    // Cached string to avoid allocations during runtime
    private const string FpsLabelPrefix = "FPS: ";
    
    public override void _Ready()
    {
        Input.MouseMode = Input.MouseModeEnum.Confined;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // Handle enter/left click - confine mouse and print FPS
        if (Input.IsActionJustReleased(InputAction.Enter) || Input.IsActionJustReleased(InputAction.LeftClick))
        {
            GetViewport().SetInputAsHandled();
            Input.MouseMode = Input.MouseModeEnum.Confined;
            
            // Note: String interpolation in C# is optimized by the compiler
            // This only runs on input, not every frame, so allocation is acceptable
            GD.Print($"{FpsLabelPrefix}{Engine.GetFramesPerSecond()}");
        }
        
        // Handle escape - toggle mouse mode or quit
        if (Input.IsActionJustReleased(InputAction.Escape))
        {
            GetViewport().SetInputAsHandled();
            
            if (Input.MouseMode == Input.MouseModeEnum.Confined)
            {
                Input.MouseMode = Input.MouseModeEnum.Visible;
            }
            else
            {
                GetTree().Quit();
            }
        }
    }
}
