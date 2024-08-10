using System;

[Group( "CareFall" )]
[Title( "Faller Controller" )]
[Icon( "paragliding" )]
public sealed class PlayerController : Component
{
	[Property] public float RunSpeed { get; set; } = 150.0f;
	[Property] public float FlySpeed { get; set; } = 3750.0f;
	[Property] public float GroundInertia { get; set; } = 0.1f;
	[Property] public float AirInertia { get; set; } = 0.5f;
	[Property] public float FlyInertia { get; set; } = 1.5f;
	[Property] public float MaxFallSpeed { get; set; } = -3000.0f;
	[Property] public float FlyTime { get; set; } = 0.75f;
	[Property] public int FeelerCount { get; set; } = 16;
	[Property] public float FeelerLength { get; set; } = 50.0f;

	[Sync] public Angles EyeAngles { get; set; }
	[Sync] public Vector3 WishVelocity { get; set; }
	[RequireComponent] CharacterController CharacterController { get; }

	public float EyeHeight = 48;

	protected override void OnUpdate()
	{
		if ( !IsProxy )
		{
			MouseInput();
			Transform.Rotation = new Angles( 0, EyeAngles.yaw, 0 );
		}
	}

	protected override void OnFixedUpdate()
	{
		if ( IsProxy )
			return;
		MovementInput();
	}

	private void MouseInput()
	{
		var e = EyeAngles;
		e += Input.AnalogLook;
		e.pitch = e.pitch.Clamp( -90, 90 );
		e.roll = 0.0f;
		EyeAngles = e;
	}

	float CurrentMoveSpeed
	{
		get
		{
			return IsFlying() ? FlySpeed : RunSpeed;
		}
	}

	RealTimeSince lastGrounded;
	RealTimeSince lastJump;

	float GetInertia()
	{
		float inertia = IsFlying() ? FlyInertia : (CharacterController.IsOnGround ? GroundInertia : AirInertia);
		return inertia < 0.09f ? 0.1f : inertia;
	}

	bool IsFlying()
	{
		return lastGrounded > FlyTime;
	}

	private void MovementInput()
	{
		if ( CharacterController is null )
			return;

		var cc = CharacterController;

		float deltaInertia = Time.Delta / GetInertia();

		WishVelocity = Input.AnalogMove;

		if ( lastGrounded < 0.2f && lastJump > 0.3f && Input.Pressed( "jump" ) )
		{
			lastJump = 0;
			cc.Punch( Vector3.Up * 250 );
		}

		if ( !WishVelocity.IsNearlyZero() )
		{
			WishVelocity = new Angles( 0, EyeAngles.yaw, 0 ).ToRotation() * WishVelocity;
			WishVelocity = WishVelocity.WithZ( 0 ).ClampLength( 1 ) * CurrentMoveSpeed;
		}
		WishVelocity = ((WishVelocity - cc.Velocity) * deltaInertia).WithZ( 0 );

		float fallSpeed = cc.Velocity.z;
		if ( fallSpeed < MaxFallSpeed )
		{
			cc.Velocity = fallSpeed + cc.Velocity.WithZ( (MaxFallSpeed - fallSpeed) * deltaInertia );
		}
		else if ( !cc.IsOnGround )
		{
			cc.Velocity += Scene.PhysicsWorld.Gravity * Time.Delta;
			if ( cc.Velocity.z < MaxFallSpeed )
			{
				cc.Velocity = cc.Velocity.WithZ( MaxFallSpeed );
			}
		}
		cc.Velocity += WishVelocity;
		cc.Move();

		if ( cc.IsOnGround )
		{
			cc.Velocity = cc.Velocity.WithZ( 0 );
			lastGrounded = 0;
		}

		if ( Transform.Position.z < -1000 )
		{
			CharacterController.MoveTo( Transform.Position.WithZ( 10000 ), false );
		}
	}

	private void UpdateCamera()
	{
		var camera = Scene.GetAllComponents<CameraComponent>().FirstOrDefault( x => x.IsMainCamera );
		if ( camera is null ) return;

		camera.Transform.Position = Transform.Position + (Vector3.Up * EyeHeight);
		camera.Transform.Rotation = EyeAngles;
		camera.FieldOfView = Preferences.FieldOfView;
	}

	protected override void OnPreRender()
	{
		if ( IsProxy )
			return;
		UpdateCamera();
	}
}
