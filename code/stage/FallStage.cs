[Group( "CareFall" )]
[Title( "Fall Stage" )]
[Icon( "waterfall_chart" )]
public sealed class FallStage : Component
{
	[Property] public Color SkyColor { get; set; } = Color.Black;

	public float SkyEndZ = float.NaN;
	public float StageEndZ = float.NaN;

	protected override void OnAwake()
	{
		SkyEndZ = GameObject.Children.Find(c => c.Name == "EndSky")?.Transform.LocalPosition.z ?? float.NaN;
		StageEndZ = GameObject.Children.Find(c => c.Name == "EndStage")?.Transform.LocalPosition.z ?? float.NaN;
	}
}
