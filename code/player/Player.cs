using System;
using Sandbox.Audio;

[Group( "CareFall" )]
[Title( "Flyer" )]
[Icon( "paragliding" )]
public sealed class Player : Component
{
	const int FEELER_LAYERS = 3;
	const int FEELERS_PER_LAYER = 16;
	const int SQUEEZE_COL_THRESHOLD = FEELERS_PER_LAYER / 16;
	const int SQUEEZE_MAX_THRESHOLD = FEELERS_PER_LAYER - SQUEEZE_COL_THRESHOLD - 1;
	const float SQUEEZE_COOLDOWN = 0.15f;
	const float FOOTLIGHT_RADIUS_MAX = 3200.0f;
	const float FOOTLIGHT_RADIUS_SPEED = FOOTLIGHT_RADIUS_MAX / 2.5f;

	[Property] public float RunSpeed { get; set; } = 150.0f;
	[Property] public float FlySpeed { get; set; } = 4250.0f;
	[Property] public float GroundInertia { get; set; } = 0.1f;
	[Property] public float AirInertia { get; set; } = 0.5f;
	[Property] public float FlyInertia { get; set; } = 1.5f;
	[Property] public float FlyBounciness { get; set; } = 1.5f;
	[Property] public float MaxFallSpeed { get; set; } = -3000.0f;
	[Property] public float FlyTime { get; set; } = 1.0f;
	[Property] public float DVelSqSmack { get; set; } = 100000.0f;
	[Property] public float DVelSqKill { get; set; } = 5000000.0f;
	[Property] public float VerticalBound { get; set; } = 100000.0f;
	[Property] public float FeelerLength { get; set; } = 200.0f;

	[Sync] public Angles EyeAngles { get; set; }
	[Sync] public Vector3 WishVelocity { get; set; }
	[RequireComponent] CharacterController CharacterController { get; set; }

	public float EyeHeight = 48;
	private float RunBounciness = 0.0f;

	public bool Flying = false;
	public float Speed = 0.0f;
	public bool Alive = true;
	public bool FinishedRun = false;

	private int scrapeCount = 0;
	private bool squeezing = false;
	public int ScoreBumps = 0;
	public float ScoreScrapes = 0.0f;
	public int ScoreSqueezes = 0;
	public int ScoreStages = 0;

	public int NumWins = 0;
	public int NumDeaths = 0;

	private readonly Mixer mixerScore = Mixer.FindMixerByName( "Scoring" );
	private readonly Mixer mixerSmack = Mixer.FindMixerByName( "Impacts" );
	private SoundHandle scrapeSound = null;

	private RealTimeSince lastGrounded;
	private RealTimeSince lastJump;
	private RealTimeSince lastSqueeze;
	public RealTimeSince LastStageCompletion;

	private readonly Vector3[] feelerDirs = new Vector3[FEELER_LAYERS * FEELERS_PER_LAYER];
	private HashSet<Collider> prevCols = new( FEELER_LAYERS * FEELERS_PER_LAYER );
	private readonly HashSet<Collider> newCols = new( FEELER_LAYERS * FEELERS_PER_LAYER );
	private float FootLightRadiusTarget = 0.0f;

	private CameraComponent Camera;
	private PointLight FootLight;

	protected override void OnAwake()
	{
		CareFall.Game.plr = this;
		Camera = Scene.GetAllComponents<CameraComponent>().FirstOrDefault( x => x.IsMainCamera );
		FootLight = GameObject.Children.Find( c => c.Name == "Light" ).Components.Get<PointLight>();
		RunBounciness = CharacterController.Bounciness;

		var plrStats = Sandbox.Services.Stats.GetLocalPlayerStats( Game.Ident );
		NumDeaths = (int)plrStats.Get( "deaths" ).Value;
		NumWins = (int)plrStats.Get( "wins" ).Value;

		const float feelerStep = (float)Math.Tau / FEELERS_PER_LAYER;
		const float layerStep = (float)Math.PI * 0.25f / FEELER_LAYERS;
		const int layerOffset = FEELER_LAYERS / 2;
		for ( int i = 0; i < feelerDirs.Length; i++ )
		{
			float angle = i / FEELER_LAYERS * feelerStep;
			feelerDirs[i] = new Vector3( (float)Math.Cos( angle ), (float)Math.Sin( angle ), layerStep * ((i % FEELER_LAYERS) - layerOffset) );
		}
	}

	private void StartScrape( Collider col )
	{
		var go = col.GameObject;
		var tags = go.Tags;
		if ( !tags.Has( "pfc-ignore" ) )
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

	private void EndScrape( Collider col )
	{
		var go = col.GameObject;
		var tags = go.Tags;
		if ( tags.Has( "pfc-bumped" ) && go.Components.Get<FallObstacle>().EndScrape( col ) )
		{
			scrapeCount--;
		}
	}

	public bool CanScore()
	{
		return Alive && Flying;
	}

	public bool IsScraping()
	{
		return scrapeCount > 0 && CanScore();
	}

	public void TeleportTo( Vector3 to )
	{
		Transform.Position = to;
		Transform.ClearInterpolation();
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

			if ( FootLight.Radius < FootLightRadiusTarget )
			{
				FootLight.Radius = Math.Min( FootLight.Radius + (FOOTLIGHT_RADIUS_SPEED * Time.Delta), FootLightRadiusTarget );
			}
		}
	}

	protected override void OnFixedUpdate()
	{
		if ( IsProxy )
			return;
		MovementInput();
		if ( CanScore() )
		{
			CheckFeelers();
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

	public void TakeStageSky( FallStage stage )
	{
		Camera.BackgroundColor = stage.SkyColor;
	}

	private void ScoreStage()
	{
		ScoreStages++;
		LastStageCompletion = 0;
		Sound.Play( "score_stage", mixerScore );
	}

	public void AdvanceStage( FallStage stage, float dZ, bool scoring )
	{
		TakeStageSky( stage );
		TeleportTo( Transform.Position.WithZ( Transform.Position.z + dZ ) );

		if ( scoring && CanScore() )
		{
			ScoreStage();
		}
	}

	public int GetScore()
	{
		return
			(CareFall.SCORE_STAGE_AMT * ScoreStages) +
			(CareFall.SCORE_SQUEEZE_AMT * ScoreSqueezes) +
			(CareFall.SCORE_BUMP_AMT * ScoreBumps) +
			(int)ScoreScrapes;
	}

	public void Die()
	{
		if ( FinishedRun )
		{
			NumWins++;
		}
		else
		{
			NumDeaths++;
		}

		if ( !Application.IsEditor )
		{
			Sandbox.Services.Stats.Increment( FinishedRun ? "wins" : "deaths", 1 );
			Sandbox.Services.Stats.SetValue( "high-score", GetScore() );
		}

		Alive = false;
	}

	public void FinishRun()
	{
		if ( Alive )
		{
			ScoreStage();
			FinishedRun = true;
			Die();
		}
	}

	public void Respawn()
	{
		TeleportTo( Scene.Directory.FindByName( "PlrStart" ).FirstOrDefault().Transform.Position );
		CharacterController.Velocity = Vector3.Zero;

		scrapeCount = 0;
		squeezing = false;

		ScoreBumps = 0;
		ScoreScrapes = 0.0f;
		ScoreSqueezes = 0;
		ScoreStages = 0;
		FinishedRun = false;

		Alive = true;
		lastGrounded = 0;
		EyeAngles = Angles.Zero;
		EndFlight();
	}

	private void EndFlight()
	{
		CharacterController.Bounciness = RunBounciness;
		Flying = false;
		FootLight.Radius = 0.0f;
		FootLightRadiusTarget = 0.0f;
	}

	private void BeginFlight()
	{
		CharacterController.Bounciness = FlyBounciness;
		Flying = true;
		FootLightRadiusTarget = FOOTLIGHT_RADIUS_MAX;
	}

	private void CheckFeelers()
	{
		newCols.Clear();
		int lastHitCol = -1;

		var startPos = Transform.Position.WithZ( Transform.Position.z + (0.5f * EyeHeight) );
		for ( int i = 0; i < feelerDirs.Length; i++ )
		{
			var result = Scene.Trace.Ray( startPos, startPos + (feelerDirs[i] * FeelerLength) ).Run();
			/*
			Gizmo.Draw.Color = result.Hit ? (result.Component is Collider ? Gizmo.Colors.Blue : Gizmo.Colors.Red) : Gizmo.Colors.Green;
			Gizmo.Draw.Line( result.StartPosition, result.Hit ? result.HitPosition : result.EndPosition );
			//*/
			if ( result.Hit && result.Component is Collider col )
			{
				newCols.Add( col );
				int iCol = i / FEELER_LAYERS;
				lastHitCol = (iCol <= lastHitCol + SQUEEZE_COL_THRESHOLD) ? iCol : -1;
			}
		}

		foreach ( var col in newCols.Except( prevCols ) )
		{
			StartScrape( col );
		}
		foreach ( var col in prevCols.Except( newCols ) )
		{
			EndScrape( col );
		}
		prevCols = new( newCols );

		squeezing = IsScraping() && lastHitCol > SQUEEZE_MAX_THRESHOLD;
		if ( squeezing )
		{
			if ( lastSqueeze > SQUEEZE_COOLDOWN )
			{
				Sound.Play( "score_squeeze", mixerScore );
				ScoreSqueezes++;
			}
			lastSqueeze = 0;
		}

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

	private void MovementInput()
	{
		if ( CharacterController is null )
			return;
		var cc = CharacterController;

		float deltaInertia = Time.Delta / GetInertia();

		WishVelocity = Alive ? Input.AnalogMove : Vector3.Zero;

		if ( Input.Pressed( "restart" ) )
		{
			CareFall.Game.stages.RestartToBeginning();
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
			Die();
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
	}

	public bool JustBeganRun()
	{
		return lastGrounded > FlyTime && lastGrounded < (FlyTime + 2.5f);
	}

	protected override void OnPreRender()
	{
		if ( IsProxy || Camera is null )
			return;
		Camera.Transform.Position = Transform.Position + (Vector3.Up * EyeHeight);
		Camera.Transform.Rotation = EyeAngles;
		Camera.FieldOfView = Preferences.FieldOfView;
	}
}
