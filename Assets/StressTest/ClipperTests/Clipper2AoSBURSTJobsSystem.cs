using Chart3D.MathExtensions;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Clipper2AoS;
public partial class Clipper2AoSBURSTJobsSystem : SystemBase
{
    EntityQuery polygonQuery;

    protected override void OnCreate()
    {
        polygonQuery = new EntityQueryBuilder(Allocator.Temp)
                            .WithAll<PolygonType>()
                            .WithAll<Nodes>()
                            .WithAll<StartIDs>()
                            .Build(World.EntityManager);
        RequireForUpdate<ClipperStressTest>();
    }
    protected override void OnDestroy()
    {
    }

    protected override void OnUpdate()
    {
        if (SystemAPI.GetSingleton<ClipperStressTest>().clipperTestType != ClipperTestType.Clipper2AoSBURSTJobs)
            return;

        if (polygonQuery.IsEmpty)
            return;

        var polgyonEntities = polygonQuery.ToEntityArray(Allocator.Temp);
        PolygonInt _subj = default;
        PolygonInt _clip = default;
        for (int i = 0, length = polgyonEntities.Length; i < length; i++)
        {
            var entity = polgyonEntities[i];
            var polyType = SystemAPI.GetComponent<PolygonType>(entity);
            var nodes = SystemAPI.GetBuffer<Nodes>(entity).Reinterpret<int2>();
            var startIDs = SystemAPI.GetBuffer<StartIDs>(entity).Reinterpret<int>();
            if (polyType.value == PolyType.Subject)
                _subj = StaticHelper.GetPolygonInt(nodes, startIDs, Allocator.TempJob);
            else if (polyType.value == PolyType.Clip)
                _clip = StaticHelper.GetPolygonInt(nodes, startIDs, Allocator.TempJob);
        }
        var jobHandles = new NativeArray<JobHandle>(StaticHelper.numberOfPolygons+1, Allocator.TempJob);
        for (int i = 0; i < StaticHelper.numberOfPolygons; i++)
        {
            jobHandles[i] = Job
                .WithBurst()
                .WithReadOnly(_subj)
                .WithReadOnly(_clip)
                .WithCode(() =>
            {
                PolygonInt _solution = new PolygonInt(2000, Allocator.Temp);
                ClipperL c = new ClipperL(Allocator.Temp);
                c.AddSubject(_subj);
                c.AddClip(_clip);
                c.Execute(ClipType.Intersection, FillRule.NonZero, ref _solution);
                c.Dispose();
                _solution.Dispose();
            }).Schedule(Dependency);
        }
        jobHandles[StaticHelper.numberOfPolygons] = Dependency;
        Dependency = JobHandle.CombineDependencies(jobHandles);
        jobHandles.Dispose(Dependency);
        _subj.Dispose(Dependency);
        _clip.Dispose(Dependency);
    }
}
////example use of tree structure to access polygons directly from outPtList and OutRecList 
//var jobClipper2 = new ClipperD(Allocator.Temp);
//var earthLandPoly = new Polygon(100, Allocator.Temp);
//jobClipper2.AddSubject(subjectPolgyon);
//jobClipper2.AddClip(L_mapUnionPoly);
//PolyTree result = new PolyTree(4, Allocator.Temp);
//jobClipper2.Execute(ClipType.Difference, FillRule.NonZero, ref result, ref earthLandPoly);
//earthLandPoly.Clear();
//for (int h = 0, length = result.exteriorIDs.Length; h < length; h++) //now tesselate each polygon of solution
//{
//    jobClipper2.GetPolygonWithHoles(result, result.exteriorIDs[h], ref earthLandPoly);
//    //..etc pp
//    earthLandPoly.Clear();
//}
