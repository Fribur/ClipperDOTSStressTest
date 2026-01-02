using Polybool;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

public partial struct PolyboolBURSTSystem : ISystem
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

    public void OnUpdate(ref SystemState state)
    {
        if (SystemAPI.GetSingleton<ClipperStressTest>().clipperTestType != ClipperTestType.PolyboolBURST)
            return;

        if (polygonQuery.IsEmpty)
            return;

        var polgyonEntities = polygonQuery.ToEntityArray(Allocator.Temp);
        NativeList<long2> subjectNodes = default;
        NativeList<int> subjectStartIDs = default;
        NativeList<long2> clipNodes = default;
        NativeList<int> clipStartIDs = default;
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

        var polyboolJob = new PolyboolJob()
        {
            subjectNodes = subjectNodes,
            subjectStartIDs = subjectStartIDs,
            clipNodes = clipNodes,
            clipStartIDs = clipStartIDs
        };
        state.Dependency = polyboolJob.Schedule(state.Dependency);
        subjectNodes.Dispose(state.Dependency);
        subjectStartIDs.Dispose(state.Dependency);
        clipNodes.Dispose(state.Dependency);
        clipStartIDs.Dispose(state.Dependency);
    }
    [BurstCompile]
    struct PolyboolJob : IJob
    {
        [ReadOnly] public NativeList<long2> subjectNodes;
        [ReadOnly] public NativeList<int> subjectStartIDs;
        [ReadOnly] public NativeList<long2> clipNodes;
        [ReadOnly] public NativeList<int> clipStartIDs;

        public void Execute()
        {
            var polyBoolIntersector = new Intersecter(true, subjectNodes.Length, FillRule.EvenOdd, Allocator.Temp);
            var subject = new Polygon(subjectNodes, subjectStartIDs, false);
            var clip = new Polygon(clipNodes, clipStartIDs, false);
            for (int i = 0; i < StaticHelper.numberOfPolygons; i++)
            {
                polyBoolIntersector.Reset(true, FillRule.EvenOdd);
                var result = PolyboolClipper.Operate(subject, clip, ClipType.Intersection, FillRule.EvenOdd, ref polyBoolIntersector);
            }
        }
    }
}
