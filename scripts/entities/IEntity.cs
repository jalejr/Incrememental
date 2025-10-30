using Godot;

namespace Incrememental.scripts.entities;

public interface IEntity
{
    Vector3 Position { get; }
    Quaternion Rotation { get; }
    float Radius { get; }
    Team Team { get; }
    bool IsActive { get; }
}