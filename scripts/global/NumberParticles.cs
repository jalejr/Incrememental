using Godot;

namespace Incrememental.scripts.global;

/// <summary>
/// Global particle emitter for number damage indicators.
/// This should be set up as an AutoLoad singleton in Project Settings.
/// Access via NumberParticles.Instance in C# code.
/// Directly instantiates the particle scene instead of wrapping an autoload.
/// </summary>
public partial class NumberParticles : Node
{
    public static NumberParticles Instance { get; private set; }
    
    private GpuParticles3D _particleSystem;
    
    public override void _Ready()
    {
        Instance = this;
        
        // Load and instantiate the particle scene directly
        var particleScene = GD.Load<PackedScene>("res://scenes/particles/number_particles.tscn");
        if (particleScene != null)
        {
            _particleSystem = particleScene.Instantiate<GpuParticles3D>();
            if (_particleSystem != null)
            {
                AddChild(_particleSystem);
                GD.Print("NumberParticles: Particle system loaded and instantiated");
            }
            else
            {
                GD.PrintErr("NumberParticles: Failed to instantiate particle system as GpuParticles3D");
            }
        }
        else
        {
            GD.PrintErr("NumberParticles: Failed to load particle scene");
        }
    }
    
    /// <summary>
    /// Emits a particle with custom data (typically for damage numbers).
    /// </summary>
    /// <param name="particleTransform">The transform of the particle</param>
    /// <param name="velocity">Initial velocity</param>
    /// <param name="color">Particle color</param>
    /// <param name="customData">Custom data (damage value, etc.)</param>
    /// <param name="flags">Particle flags</param>
    public void EmitParticle(Transform3D particleTransform, Vector3 velocity, Color color, Color customData, uint flags)
    {
        if (_particleSystem != null)
        {
            _particleSystem.EmitParticle(particleTransform, velocity, color, customData, flags);
        }
        else
        {
            GD.PrintErr("NumberParticles: Particle system not initialized!");
        }
    }
}
