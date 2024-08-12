using System;
using Sandbox.Audio;

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
	[Property] public float FlyBounciness { get; set; } = 1.5f;
	[Property] public float MaxFallSpeed { get; set; } = -3000.0f;
	[Property] public float FlyTime { get; set; } = 1.0f;
	[Property] public float DVelSqSmack { get; set; } = 100000.0f;
	[Property] public float DVelSqKill { get; set; } = 5000000.0f;
	[Property] public float VerticalBound { get; set; } = 100000.0f;

	[Sync] public Angles EyeAngles { get; set; }
	[Sync] public Vector3 WishVelocity { get; set; }
	[RequireComponent] CharacterController CharacterController { get; set; }

	public float EyeHeight = 48;
	private float RunBounciness = 0.0f;

	public bool Flying = false;
	public float Speed = 0.0f;
	public bool Alive = true;

	private int scrapeCount = 0;
	public int ScoreBumps = 0;
	public float ScoreScrapes = 0.0f;

	private readonly Mixer mixerScore = Mixer.FindMixerByName( "Scoring" );
	private readonly Mixer mixerSmack = Mixer.FindMixerByName( "Impacts" );
	private SoundHandle scrapeSound = null;

	private RealTimeSince lastGrounded;
	private RealTimeSince lastJump;

	private void TeleportTo( Vector3 to )
	{
		Transform.Position = to;
		Transform.ClearInterpolation();
	}

	protected override void OnAwake()
	{
		CareFall.Game.plr = this;
		RunBounciness = CharacterController.Bounciness;
		Respawn();
	}

	void ITriggerListener.OnTriggerEnter( Collider col )
	{
		if ( Alive )
		{
			var go = col.GameObject;
			var tags = go.Tags;
			if ( Flying && !tags.Has( "pfc-ignore" ) )
			{
				FallObstacle obstacle = go.Components.GetOrCreate<FallObstacle>();
				if ( !tags.Has( "pfc-bumped" ) )
				{
					ScoreBumps++;
					Sound.Play( "score_bump", mixerScore );
				}

				if ( obstacle.StartScrape( col ) )
				{
					scrapeCount++;
				}
			}
		}
	}

	void ITriggerListener.OnTriggerExit( Collider col )
	{
		if ( Alive )
		{
			var go = col.GameObject;
			var tags = go.Tags;
			if ( !tags.Has( "pfc-ignore" ) && tags.Has( "pfc-bumped" ) && go.Components.Get<FallObstacle>().EndScrape( col ) )
			{
				scrapeCount--;
			}
		}
	}

	public bool IsScraping()
	{
		return Alive && Flying && scrapeCount > 0;
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
				scrapeSound = Sound.Play( "score_scrape", mixerScore );
			}
			ScoreScrapes += Time.Delta * (float)Math.Pow( Speed, 1.5 ) * 0.0002f;
		}
		else
		{
			scrapeSound?.Stop();
		}
	}

	float GetInertia()
	{
		float inertia = Flying ? FlyInertia : (CharacterController.IsOnGround ? GroundInertia : AirInertia);
		return inertia < 0.09f ? 0.1f : inertia;
	}

	private void Respawn()
	{
		TeleportTo( Scene.Directory.FindByName( "PlrStart" ).FirstOrDefault().Transform.Position );
		CharacterController.Velocity = 0.0f;
		foreach ( var obstacle in Scene.GetAllComponents<FallObstacle>() )
		{
			obstacle.Restore();
		}
		scrapeCount = 0;
		ScoreBumps = 0;
		ScoreScrapes = 0.0f;
		Alive = true;
		lastGrounded = 0;
		EyeAngles = Angles.Zero;
		EndFlight();
	}

	private void EndFlight()
	{
		CharacterController.Bounciness = RunBounciness;
		Flying = false;
	}

	private void BeginFlight()
	{
		CharacterController.Bounciness = FlyBounciness;
		Flying = true;
	}

	private void MovementInput()
	{
		if ( CharacterController is null )
			return;
		var cc = CharacterController;

		float deltaInertia = Time.Delta / GetInertia();

		WishVelocity = Alive ? Input.AnalogMove : Vector3.Zero;

		if ( Input.Pressed( "restart" ) )
		{
			Respawn();
			return;
		}

		if ( lastGrounded < 0.2f && lastJump > 0.3f && Input.Pressed( "jump" ) )
		{
			lastJump = 0;
			cc.Punch( Vector3.Up * 250 );
		}

		if ( !WishVelocity.IsNearlyZero() )
		{
			WishVelocity = new Angles( 0, EyeAngles.yaw, 0 ).ToRotation() * WishVelocity;
			WishVelocity = WishVelocity.WithZ( 0 ).ClampLength( 1 ) * (Flying ? FlySpeed : RunSpeed);
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
		Vector3 prevVel = cc.Velocity;
		cc.Move();
		Speed = cc.Velocity.Length;

		float dVelSq = prevVel.DistanceSquared( cc.Velocity );
		if ( dVelSq >= DVelSqSmack )
		{
			Sound.Play( "smack", mixerSmack );
		}
		if ( Alive && dVelSq >= DVelSqKill )
		{
			Alive = false;
		}

		if ( !Flying || !Alive )
		{
			if ( cc.IsOnGround )
			{
				lastGrounded = 0;
				cc.Velocity = cc.Velocity.WithZ( 0 );
				EndFlight();
			}
			else if ( lastGrounded > FlyTime )
			{
				BeginFlight();
			}
		}

		if ( Transform.Position.z < -VerticalBound )
		{
			TeleportTo( Transform.Position.WithZ( VerticalBound ) );
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
