using Godot;

namespace Incrememental.scripts.camera.rts;

/// <summary>
/// RTS-style camera controller with pan, zoom, and rotation.
/// </summary>
[GlobalClass]
public partial class RtsCameraNew : Node3D
{
    // Constants
    private const float CameraPanMargin = 5.0f;
    private const float ZoomBuffer = 25.0f;
    private const float SpringDeadZone = 0.5f;
    private const float SpringStrength = 55.0f;

    // Exports
    [Export] public float CameraPanSpeed { get; set; } = 10.0f;
    [Export] public float CameraRotateSpeed { get; set; } = 1.0f;
    [Export] public float CameraZoomSpeed { get; set; } = 4.0f;
    [Export] public float CameraPanLerpSpeed { get; set; } = 10.0f;
    [Export] public Vector2 CameraPanRemappedRange { get; set; } = new(0.4f, 1.0f);
    [Export] public float CameraRotateLerpSpeed { get; set; } = 10.0f;
    [Export] public float CameraZoomLerpSpeed { get; set; } = 10.0f;
    [Export] public Vector2 CameraZoomRange { get; set; } = new(50.0f, 200.0f);
    [Export] public float MaxCameraZoomVelocity { get; set; } = 10.0f;

    // Private vars
    private Vector3 _cameraPanDirection = Vector3.Zero;
    private Vector3 _cameraZoomDirection = Vector3.Zero;
    private Vector3 _cameraRotateDirection = Vector3.Zero;
    private float _initialCameraY;
    private float _initialCameraZ;
    private Camera3D _camera3D;

    public override void _Ready()
    {
        _camera3D = GetNode<Camera3D>("Camera3D");
        SetupCamera(_camera3D);

        _initialCameraY = _camera3D.Position.Y;
        _initialCameraZ = _camera3D.Position.Z;
    }

    public override void _Process(double delta)
    {
        GetCameraPanMouseDirection();
        GetCameraPanKeyboardDirection();
        GetCameraRotateDirection();
        ApplyVelocity((float)delta);
    }

    public override void _Input(InputEvent @event)
    {
        if (@event.IsActionPressed("camera_zoom_in") && CameraCanZoomIn())
        {
            _cameraZoomDirection -= new Vector3(0.0f, 0.0f, 1.0f);
        }
        if (@event.IsActionPressed("camera_zoom_out") && CameraCanZoomOut())
        {
            _cameraZoomDirection += new Vector3(0.0f, 0.0f, 1.0f);
        }
    }

    /// <summary>
    /// Detects camera pan input from mouse position near screen edges.
    /// </summary>
    public void GetCameraPanMouseDirection()
    {
        if (Input.MouseMode != Input.MouseModeEnum.Confined)
            return;

        var mousePos = GetViewport().GetMousePosition();
        var viewportSize = GetViewport().GetVisibleRect().Size;

        if (mousePos.X < CameraPanMargin)
            _cameraPanDirection.X += -1;
        if (mousePos.Y < CameraPanMargin)
            _cameraPanDirection.Z += -1;
        if (mousePos.X > viewportSize.X - CameraPanMargin)
            _cameraPanDirection.X += 1;
        if (mousePos.Y > viewportSize.Y - CameraPanMargin)
            _cameraPanDirection.Z += 1;
    }

    /// <summary>
    /// Detects camera pan input from keyboard (WASD/arrow keys).
    /// </summary>
    public void GetCameraPanKeyboardDirection()
    {
        var inputDirection = Input.GetVector("left", "right", "forward", "backward");
        _cameraPanDirection += new Vector3(inputDirection.X, 0.0f, inputDirection.Y);
    }

    /// <summary>
    /// Detects camera rotation input.
    /// </summary>
    public void GetCameraRotateDirection()
    {
        if (Input.IsActionPressed("rotate_left"))
            _cameraRotateDirection -= new Vector3(0.0f, 1.0f, 0.0f);
        if (Input.IsActionPressed("rotate_right"))
            _cameraRotateDirection += new Vector3(0.0f, 1.0f, 0.0f);
    }

    /// <summary>
    /// Gets the current pan velocity.
    /// </summary>
    public Vector3 GetCameraPanVelocity()
    {
        return _cameraPanDirection * CameraPanSpeed;
    }

    /// <summary>
    /// Gets the current zoom velocity.
    /// </summary>
    public Vector3 GetCameraZoomVelocity()
    {
        return _cameraZoomDirection * (CameraZoomSpeed * 100);
    }

    /// <summary>
    /// Gets the current rotation velocity.
    /// </summary>
    public Vector3 GetCameraRotateVelocity()
    {
        return _cameraRotateDirection * CameraRotateSpeed;
    }

    private void SetupCamera(Camera3D camera)
    {
        camera.Fov = 10.0f;
        camera.Position = camera.Position with { Y = 3.0f };
        camera.Rotation = camera.Rotation with { X = Mathf.DegToRad(-30.0f) };
        GlobalRotation = GlobalRotation with { Y = Mathf.DegToRad(-45.0f) };
        camera.TranslateObjectLocal(new Vector3(0.0f, 0.0f, 100.0f));
    }

    private void CorrectCameraZoom(float delta)
    {
        var currentZ = _camera3D.Position.Z;
        var minLimit = CameraZoomRange.X - SpringDeadZone;
        var maxLimit = CameraZoomRange.Y + SpringDeadZone;

        // Spring correction for smooth bounce
        var springCorrection = 0.0f;
        if (currentZ < minLimit)
        {
            springCorrection = (minLimit - currentZ) * SpringStrength * delta;
        }
        else if (currentZ > maxLimit)
        {
            springCorrection = (maxLimit - currentZ) * SpringStrength * delta;
        }

        if (springCorrection != 0.0f)
        {
            _camera3D.TranslateObjectLocal(new Vector3(0, 0, springCorrection));
        }

        // Hard correction to prevent going too far
        var hardCorrection = 0.0f;
        if (currentZ < CameraZoomRange.X - ZoomBuffer)
        {
            hardCorrection = (CameraZoomRange.X - ZoomBuffer) - currentZ;
        }
        else if (currentZ > CameraZoomRange.Y + ZoomBuffer)
        {
            hardCorrection = (CameraZoomRange.Y + ZoomBuffer) - currentZ;
        }

        if (hardCorrection != 0.0f)
        {
            _camera3D.TranslateObjectLocal(new Vector3(0, 0, hardCorrection));
        }
    }

    private void ApplyVelocity(float delta)
    {
        var panVelocity = GetCameraPanVelocity() * delta;
        var zoomVelocity = GetCameraZoomVelocity() * delta;
        var rotateVelocity = GetCameraRotateVelocity() * delta;

        // Remap pan speed based on zoom level
        var remappedPanModifier = Mathf.Remap(
            _camera3D.Position.Z,
            CameraZoomRange.X, CameraZoomRange.Y,
            CameraPanRemappedRange.X, CameraPanRemappedRange.Y
        );

        if (panVelocity != Vector3.Zero)
        {
            TranslateObjectLocal(panVelocity * remappedPanModifier);
        }

        if (zoomVelocity != Vector3.Zero)
        {
            _camera3D.TranslateObjectLocal(zoomVelocity);
        }

        if (rotateVelocity != Vector3.Zero)
        {
            GlobalRotation = GlobalRotation with { Y = GlobalRotation.Y + rotateVelocity.Y };
        }

        CorrectCameraZoom(delta);

        // Lerp directions back to zero for smooth deceleration
        _cameraPanDirection = _cameraPanDirection.Lerp(Vector3.Zero, CameraPanLerpSpeed * delta);
        _cameraZoomDirection = _cameraZoomDirection.Lerp(Vector3.Zero, CameraZoomLerpSpeed * delta);
        _cameraRotateDirection = _cameraRotateDirection.Lerp(Vector3.Zero, CameraRotateLerpSpeed * delta);
    }

    private bool CameraCanZoomIn()
    {
        return _camera3D.Position.Z > CameraZoomRange.X;
    }

    private bool CameraCanZoomOut()
    {
        return _camera3D.Position.Z < CameraZoomRange.Y;
    }
}
