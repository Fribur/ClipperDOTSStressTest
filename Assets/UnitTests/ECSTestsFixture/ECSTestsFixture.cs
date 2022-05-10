using NUnit.Framework;
using System.Reflection;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs.LowLevel.Unsafe;
using UnityEngine.LowLevel;


/// <summary>
/// Copied from the Entities package and slightly modified to enable default world creation and fixing a call to an internal method via reflection.
/// </summary>
public class ECSTestsFixture 
{
	protected World m_PreviousWorld;
	protected World m_World;
	protected PlayerLoopSystem m_PreviousPlayerLoop;
	protected EntityManager m_Manager;
	protected EntityManager.EntityManagerDebug m_ManagerDebug;

	protected int StressTestEntityCount = 1000;
	protected bool CreateDefaultWorld = false;
	private bool JobsDebuggerWasEnabled;

	[SetUp]
	public virtual void Setup()
	{
		// unit tests preserve the current player loop to restore later, and start from a blank slate.
		m_PreviousPlayerLoop = PlayerLoop.GetCurrentPlayerLoop();
		PlayerLoop.SetPlayerLoop(PlayerLoop.GetDefaultPlayerLoop());

		m_PreviousWorld = m_World;
		m_World = World.DefaultGameObjectInjectionWorld =
			CreateDefaultWorld ? DefaultWorldInitialization.Initialize("Default Test World") : new World("Empty Test World");
		m_Manager = m_World.EntityManager;
		m_ManagerDebug = new EntityManager.EntityManagerDebug(m_Manager);

		// Many ECS tests will only pass if the Jobs Debugger enabled;
		// force it enabled for all tests, and restore the original value at teardown.
		JobsDebuggerWasEnabled = JobsUtility.JobDebuggerEnabled;
		JobsUtility.JobDebuggerEnabled = true;
		JobUtility_ClearSystemIds();
	}

	[TearDown]
	public virtual void TearDown()
	{
		if (m_World != null && m_World.IsCreated)
		{
			// Clean up systems before calling CheckInternalConsistency because we might have filters etc
			// holding on SharedComponentData making checks fail
			while (m_World.Systems.Count > 0)
				m_World.DestroySystem(m_World.Systems[0]);

			m_ManagerDebug.CheckInternalConsistency();

			m_World.Dispose();
			m_World = null;

			m_World = m_PreviousWorld;
			m_PreviousWorld = null;
			m_Manager = default;
		}

		JobsUtility.JobDebuggerEnabled = JobsDebuggerWasEnabled;
		JobUtility_ClearSystemIds();

		PlayerLoop.SetPlayerLoop(m_PreviousPlayerLoop);
	}
	private void JobUtility_ClearSystemIds() =>
		typeof(JobsUtility).GetMethod("ClearSystemIds", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
}