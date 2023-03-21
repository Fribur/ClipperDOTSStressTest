using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.PerformanceTesting;
using Unity.Transforms;
using UnityEditor.PackageManager;

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

		var subjectPolygon = m_Manager.CreateEntity(typeof(PolygonType), typeof(Nodes), typeof(StartIDs));
		m_Manager.SetComponentData(subjectPolygon, new PolygonType { value = PolyType.Subject });
		var nodesBuffer = m_Manager.GetBuffer<Nodes>(subjectPolygon).Reinterpret<int2>();
		var rand = new Random(1337);
		StaticHelper.GenerateRandomNodes(rand, ref nodesBuffer, StaticHelper.DisplayHeight, StaticHelper.DisplayHeight, StaticHelper.edgeCount);
		var startIDsBuffer = m_Manager.GetBuffer<StartIDs>(subjectPolygon).Reinterpret<int>();
		startIDsBuffer.Add(0);
		startIDsBuffer.Add(StaticHelper.edgeCount);

		var clipPolygon = m_Manager.CreateEntity(typeof(PolygonType), typeof(Nodes), typeof(StartIDs));
        m_Manager.SetComponentData(clipPolygon, new PolygonType { value = PolyType.Clip });
        var clipNodesBuffer = m_Manager.GetBuffer<Nodes>(clipPolygon).Reinterpret<int2>();
        StaticHelper.GenerateRandomNodes(rand, ref clipNodesBuffer, StaticHelper.DisplayHeight, StaticHelper.DisplayHeight, StaticHelper.edgeCount);
        var clipStartIDsBuffer = m_Manager.GetBuffer<StartIDs>(clipPolygon).Reinterpret<int>();
        clipStartIDsBuffer.Add(0);
        clipStartIDsBuffer.Add(StaticHelper.edgeCount);

        Measure.Method(() =>
        {
            // update the world once, running all systems once
				m_World.Update();

				// we don't want any jobs to continue running past the measurement cycle
				m_Manager.CompleteAllTrackedJobs();
			})
			.WarmupCount(2)
			.MeasurementCount(5)
			.IterationsPerMeasurement(1)
			.Run();
	}


	[Test, Performance]
	public void ClipperClass_Intersection() => MeasureWorldUpdate(ClipperTestType.Clipper2Lib);

	[Test, Performance]
	public void ClipperSoA_Intersection() => MeasureWorldUpdate(ClipperTestType.Clipper2SoA);
    [Test, Performance]
    public void ClipperAoS_Intersection() => MeasureWorldUpdate(ClipperTestType.Clipper2AoS);

    [Test, Performance]
	public void ClipperSoABURST_Intersection() => MeasureWorldUpdate(ClipperTestType.Clipper2SoABURST);

    [Test, Performance]
    public void ClipperAoSBURST_Intersection() => MeasureWorldUpdate(ClipperTestType.Clipper2AoSBURST);

    [Test, Performance]
	public void ClipperSoABURSTJobs_Intersection() => MeasureWorldUpdate(ClipperTestType.Clipper2SoABURSTJobs);

    [Test, Performance]
    public void ClipperAoSBURSTJobs_Intersection() => MeasureWorldUpdate(ClipperTestType.Clipper2AoSBURSTJobs);

    public override void Setup()
	{
		// custom flag in ECSTestsFixture that allows creating the default world, rather than an empty (no systems) world
		// must be set before base.Setup() !
		CreateDefaultWorld = true;

		// setup the Entities world
		base.Setup();
	}
}