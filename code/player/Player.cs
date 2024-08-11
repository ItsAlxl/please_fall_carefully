using System;

[Group( "CareFall" )]
[Title( "Flyer" )]
[Icon( "paragliding" )]
public sealed class Player : Component, Component.ITriggerListener
{
	[Property] public float RunSpeed { get; set; } = 150.0f;
	[Property] public float FlySpeed { get; set; } = 3750.0f;
	[Property] public float GroundInertia { get; set; } = 0.1f;
	[Property] public float AirInertia { get; set; } = 0.5f;
	[Property] public float FlyInertia { get; set; } = 1.5f;
	[Property] public float MaxFallSpeed { get; set; } = -3000.0f;
	[Property] public float FlyTime { get; set; } = 1.0f;

	[Sync] public Angles EyeAngles { get; set; }
	[Sync] public Vector3 WishVelocity { get; set; }
	[RequireComponent] CharacterController CharacterController { get; set; }

	public float EyeHeight = 48;

	public float Speed = 0.0f;

	private int scrapeCount = 0;
	public int ScoreBumps = 0;
	public float ScoreScrapes = 0.0f;

	private SoundHandle scrapeSound = null;

	private void TeleportTo( Vector3 to )
	{
		Transform.Position = to;
		Transform.ClearInterpolation();
	}

	protected override void OnAwake()
	{
		PFC.Game.plr = this;
	}

	void ITriggerListener.OnTriggerEnter( Collider other )
	{
		scrapeCount++;
		if ( IsFlying() )
		{
			var go = other.GameObject;
			var tags = go.Tags;
			BumpMemory bumpMem;
			if ( tags.Has( "pfc-bumped" ) )
			{
				bumpMem = go.Components.Get<BumpMemory>();
			}
			else
			{
				bumpMem = go.Components.Create<BumpMemory>();
				ScoreBumps++;
				Sound.Play("score_bump");
			}
			bumpMem.StartScrape();
		}
	}

	void ITriggerListener.OnTriggerExit( Collider other )
	{
		scrapeCount--;

		if ( other.GameObject.Tags.Has( "pfc-bumped" ) )
		{
			other.GameObject.Components.Get<BumpMemory>().EndScrape();
		}
	}

	public bool IsScraping()
	{
		return scrapeCount > 0 && IsFlying();
	}

	protected override void OnUpdate()
	{
		if ( !IsProxy )
		{
			var e = EyeAngles + Input.AnalogLook;
			e.pitch = e.pitch.Clamp( -90, 90 );
			e.roll = 0.0f;
			EyeAngles = e;

			Transform.Rotation = new Angles( 0, EyeAngles.yaw, 0 );
		}
	}

	protected override void OnFixedUpdate()
	{
		if ( IsProxy )
			return;
		MovementInput();
		if ( IsScraping() )
		{
			if ( scrapeSound?.IsStopped != false )
			{
				scrapeSound?.Dispose();
				scrapeSound = Sound.Play( "score_scrape" );
			}
			ScoreScrapes += Time.Delta * (float)Math.Pow( Speed, 1.5 ) * 0.0001f;
		}
		else
		{
			scrapeSound.Stop();
		}
	}

	RealTimeSince lastGrounded;
	RealTimeSince lastJump;

	float GetInertia()
	{
		float inertia = IsFlying() ? FlyInertia : (CharacterController.IsOnGround ? GroundInertia : AirInertia);
		return inertia < 0.09f ? 0.1f : inertia;
	}

	public bool IsFlying()
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
			WishVelocity = WishVelocity.WithZ( 0 ).ClampLength( 1 ) * (IsFlying() ? FlySpeed : RunSpeed);
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
		Speed = cc.Velocity.Length;

		if ( cc.IsOnGround )
		{
			cc.Velocity = cc.Velocity.WithZ( 0 );
			lastGrounded = 0;
		}

		if ( Transform.Position.z < -10000 )
		{
			TeleportTo( Transform.Position.WithZ( 10000 ) );
		}
	}

	protected override void OnPreRender()
	{
		if ( IsProxy )
			return;

		var camera = Scene.GetAllComponents<CameraComponent>().FirstOrDefault( x => x.IsMainCamera );
		if ( camera is null ) return;

		camera.Transform.Position = Transform.Position + (Vector3.Up * EyeHeight);
		camera.Transform.Rotation = EyeAngles;
		camera.FieldOfView = Preferences.FieldOfView;
	}
}
