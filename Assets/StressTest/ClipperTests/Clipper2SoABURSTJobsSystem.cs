using Chart3D.MathExtensions;
using Clipper2SoA;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
public partial struct Clipper2SoABURSTJobsSystem : ISystem
{
    EntityQuery polygonQuery;
    void OnCreate(ref SystemState state)
    {
        polygonQuery = new EntityQueryBuilder(Allocator.Temp)
                            .WithAll<PolygonType>()
                            .WithAll<Nodes>()
                            .WithAll<StartIDs>()
                            .Build(ref state);
        state.RequireForUpdate<ClipperStressTest>();
    }
    void OnDestroy(ref SystemState state)
    {
    }

    void OnUpdate(ref SystemState state)
    {
        if (SystemAPI.GetSingleton<ClipperStressTest>().clipperTestType != ClipperTestType.Clipper2SoABURSTJobs)
            return;

        if (polygonQuery.IsEmpty)
            return;

        var polgyonEntities = polygonQuery.ToEntityArray(Allocator.Temp);
        NativeArray<int2> subjectNodes = default;
        NativeArray<int> subjectStartIDs = default;
        NativeArray<int2> clipNodes = default;
        NativeArray<int> clipStartIDs = default;
        for (int i = 0, length = polgyonEntities.Length; i < length; i++)
        {
            var entity = polgyonEntities[i];
            var polyType = SystemAPI.GetComponent<PolygonType>(entity);
            var nodesBuffer = SystemAPI.GetBuffer<Nodes>(entity);
            var startIDsBuffer = SystemAPI.GetBuffer<StartIDs>(entity);
            if (polyType.value == PolyType.Subject)
                StaticHelper.GetPolygon(nodesBuffer, startIDsBuffer, out subjectNodes, out subjectStartIDs, Allocator.Persistent);
            else if (polyType.value == PolyType.Clip)
                StaticHelper.GetPolygon(nodesBuffer, startIDsBuffer, out clipNodes, out clipStartIDs, Allocator.Persistent);
        }

        var jobHandles = new NativeArray<JobHandle>(StaticHelper.numberOfPolygons + 1, Allocator.TempJob);
        for (int i = 0; i < StaticHelper.numberOfPolygons; i++)
        {
            jobHandles[i] = new Clipper2SoAJob()
            {
                subjectNodes = subjectNodes,
                subjectStartIDs = subjectStartIDs,
                clipNodes = clipNodes,
                clipStartIDs = clipStartIDs
            }.Schedule();
        }
        jobHandles[StaticHelper.numberOfPolygons] = state.Dependency;
        state.Dependency = JobHandle.CombineDependencies(jobHandles);        
        subjectNodes.Dispose(state.Dependency);
        subjectStartIDs.Dispose(state.Dependency);
        clipNodes.Dispose(state.Dependency);
        clipStartIDs.Dispose(state.Dependency);
        jobHandles.Dispose(state.Dependency);
    }
    [BurstCompile]
    struct Clipper2SoAJob : IJob
    {
        [ReadOnly] public NativeArray<int2> subjectNodes;
        [ReadOnly] public NativeArray<int> subjectStartIDs;
        [ReadOnly] public NativeArray<int2> clipNodes;
        [ReadOnly] public NativeArray<int> clipStartIDs;

        public void Execute()
        {
            ClipperD L_c = new ClipperD(Allocator.Temp);
            PolygonInt _solution = new PolygonInt(2000, Allocator.Temp);
            L_c.AddSubject(subjectNodes, subjectStartIDs);
            L_c.AddClip(clipNodes, clipStartIDs);
            L_c.Execute(ClipType.Intersection, FillRule.NonZero, ref _solution);
            L_c.Clear();
            _solution.Clear();
        }
    }
}