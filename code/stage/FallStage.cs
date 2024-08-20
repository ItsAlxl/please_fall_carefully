[Group( "CareFall" )]
[Title( "Fall Stage" )]
[Icon( "waterfall_chart" )]
public sealed class FallStage : Component
{
	[Property] public ModelRenderer HideModel { get; set; } = null;
	[Property] public Color SkyColor { get; set; } = Color.Black;
	[Property] public bool IsFinish { get; set; } = false;

	public float SkyEndZ = float.NaN;
	public float StageEndZ = float.NaN;

	private GameObject BeginObject;

	protected override void OnAwake()
	{
		SkyEndZ = GameObject.Children.Find( c => c.Name == "EndSky" )?.Transform.LocalPosition.z ?? float.NaN;
		StageEndZ = GameObject.Children.Find( c => c.Name == "EndStage" )?.Transform.LocalPosition.z ?? float.NaN;

		BeginObject = GameObject.Children.Find( c => c.Name == "OnBegin" );
		Restore();
	}

	public void Begin()
	{
		if ( BeginObject is not null )
		{
			BeginObject.Enabled = true;
		}
		if (HideModel is not null)
		{
			HideModel.Enabled = false;
		}
	}

	public void Restore()
	{
		if ( BeginObject is not null )
		{
			BeginObject.Enabled = false;
		}
		if ( HideModel is not null )
		{
			HideModel.Enabled = true;
		}
	}
}
