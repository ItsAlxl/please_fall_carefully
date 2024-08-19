
public sealed class Spinner : Component
{
	[Property] public Vector3 RotateAxis { get; set; }
	[Property] public float RotateAmount { get; set; }

	protected override void OnUpdate()
	{
		Transform.LocalRotation = Transform.LocalRotation.RotateAroundAxis(RotateAxis, RotateAmount * Time.Delta);
	}
}
