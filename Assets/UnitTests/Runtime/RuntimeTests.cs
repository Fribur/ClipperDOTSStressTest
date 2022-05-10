using NUnit.Framework;
using Unity.Entities;
using Unity.PerformanceTesting;
using Unity.Transforms;

/// <summary>
/// To run performance measurements:
/// - open StressTest scene
/// - open: Window => Analysis => Performance Test Report (and enable "Auto Refresh")
/// - open: Window => General => Test Runner
/// Run tests in Test Runner. Then check Performance Test Report window or the saved TestResults.xml (see Console).
/// 
/// Observations:
/// - With Burst compilation disabled, performance (and testing time) is 10 times slower! (12-core CPU)
/// - Jobs => Burst => Safety Checks => Off ... affects some tests more than others! This should be considered in summary.
/// - Jobs => Jobs Debugger ... has practically no effect on measurements
/// - Jobs => Use Job Threads ... as expected: hardly affects "Single" tests, if "off" makes companion Single/Parallel tests perform about the same
/// - [BurstCompile(OptimizeFor = OptimizeFor.Performance)] ... some tests benefit from this, between 0.5-1.0 ms faster (A, D, G, H, I) 
/// </summary>
public class RuntimeTests : ECSTestsFixture
{
	private void MeasureWorldUpdate(ClipperTestType eventType)
	{		

		// we need this to spawn all the entities during the first world update
		var spawner = m_Manager.CreateEntity(typeof(ClipperStressTest));
		m_Manager.SetComponentData(spawner, new ClipperStressTest{ clipperTestType = eventType});

		Measure.Method(() =>
			{
				// update the world once, running all systems once
				m_World.Update();

				// we don't want any jobs to continue running past the measurement cycle
				m_Manager.CompleteAllJobs();
			})
			.WarmupCount(2)
			.MeasurementCount(5)
			.IterationsPerMeasurement(1)
			.Run();
	}


	[Test, Performance]
	public void ClipperClass_Intersection() => MeasureWorldUpdate(ClipperTestType.Clipper2Class_Intersection);
	[Test, Performance]
	public void ClipperStruct_Intersection() => MeasureWorldUpdate(ClipperTestType.Clipper2Struct_Intersection);
	[Test, Performance]
	public void ClipperStructBURST_Intersection() => MeasureWorldUpdate(ClipperTestType.Clipper2StructBURST_Intersection);
	[Test, Performance]
	public void ClipperStructBURSTJobs_Intersection() => MeasureWorldUpdate(ClipperTestType.Clipper2StructBURSTJobs_Intersection);

	public override void Setup()
	{
		// custom flag in ECSTestsFixture that allows creating the default world, rather than an empty (no systems) world
		// must be set before base.Setup() !
		CreateDefaultWorld = true;

		// setup the Entities world
		base.Setup();
	}
}