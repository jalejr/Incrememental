using Godot;

namespace Incrememental.scripts.entities;

public interface IMovable
{
    Vector3 Velocity { get; set; }
    float MoveSpeed { get; }
}