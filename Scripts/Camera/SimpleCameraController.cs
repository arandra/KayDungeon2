using Godot;

public partial class SimpleCameraController : Node3D
{
    [Export] public float MoveSpeed = 10.0f;
    [Export] public float RotateSpeedDegrees = 60.0f;
    [Export] public float VerticalSpeed = 8.0f;

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        var move = Vector3.Zero;

        if (Input.IsKeyPressed(Key.W))
        {
            move += -GlobalTransform.Basis.Z;
        }
        if (Input.IsKeyPressed(Key.S))
        {
            move += GlobalTransform.Basis.Z;
        }
        if (Input.IsKeyPressed(Key.A))
        {
            move += -GlobalTransform.Basis.X;
        }
        if (Input.IsKeyPressed(Key.D))
        {
            move += GlobalTransform.Basis.X;
        }
        if (Input.IsKeyPressed(Key.R))
        {
            move += Vector3.Up;
        }
        if (Input.IsKeyPressed(Key.F))
        {
            move += Vector3.Down;
        }

        if (move != Vector3.Zero)
        {
            var speed = MoveSpeed;
            if (move == Vector3.Up || move == Vector3.Down)
            {
                speed = VerticalSpeed;
            }
            GlobalPosition += move.Normalized() * speed * dt;
        }

        if (Input.IsKeyPressed(Key.Q))
        {
            RotateY(Mathf.DegToRad(-RotateSpeedDegrees) * dt);
        }
        if (Input.IsKeyPressed(Key.E))
        {
            RotateY(Mathf.DegToRad(RotateSpeedDegrees) * dt);
        }
    }
}
