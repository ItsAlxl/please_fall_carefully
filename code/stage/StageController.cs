using Sandbox.Audio;

[Group( "CareFall" )]
[Title( "Stage Controller" )]
[Icon( "import_export" )]
public sealed class StageController : Component
{
	private int currentStage = 0;
	private readonly List<FallStage> stages = new();

	private readonly Mixer mixerMusic = Mixer.FindMixerByName( "Music" );

	protected override void OnAwake()
	{
		CareFall.Game.stages = this;
		foreach ( var c in GameObject.Children )
		{
			var s = c.Components.Get<FallStage>();
			if ( s is not null )
			{
				stages.Add( s );
			}
		}
		RestartToBeginning();

		Sound.Play( "ost-reckoning", mixerMusic );
	}

	public void RestartToBeginning()
	{
		BeginStage( 0 );
		foreach ( var obstacle in Scene.GetAllComponents<FallObstacle>() )
		{
			obstacle.Restore();
		}
		CareFall.Game.plr.Respawn();
	}

	private void BeginStage( int idx )
	{
		var plr = CareFall.Game.plr;
		if ( idx < stages.Count )
		{
			var stage = stages[idx];
			var prevZ = Transform.Position.z;

			Transform.Position = Transform.Position.WithZ( prevZ + plr.VerticalBound - stage.Transform.Position.z );
			Transform.ClearInterpolation();
			plr.AdvanceStage( stage, Transform.Position.z - prevZ, idx > 0 );
			stage.Begin();
		}
		else
		{
			plr.FinishRun();
		}
		currentStage = idx;
	}

	public void AdvanceStage()
	{
		BeginStage( currentStage + 1 );
	}

	protected override void OnFixedUpdate()
	{
		if ( currentStage < stages.Count )
		{
			var stage = stages[currentStage];
			var plr = CareFall.Game.plr;
			var plrRelativeZ = plr.Transform.Position.z - stage.Transform.Position.z;
			if ( plrRelativeZ < stage.SkyEndZ && currentStage + 1 < stages.Count )
			{
				plr.TakeStageSky( stages[currentStage + 1] );
			}
			if ( plrRelativeZ < stage.StageEndZ )
			{
				AdvanceStage();
			}
		}
	}
}
