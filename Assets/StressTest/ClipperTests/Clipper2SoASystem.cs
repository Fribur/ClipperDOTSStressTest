using Chart3D.MathExtensions;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Clipper2SoA;

public partial class Clipper2SoASystem : SystemBase
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
        if (SystemAPI.GetSingleton<ClipperStressTest>().clipperTestType != ClipperTestType.Clipper2SoA)
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
        Job
            .WithoutBurst()
            .WithReadOnly(_subj)
            .WithReadOnly(_clip)
            .WithCode(() =>
        {
            ClipperD L_c = new ClipperD(Allocator.Temp);
            PolygonInt _solution = new PolygonInt(2000, Allocator.Temp);
            for (int i = 0; i < StaticHelper.numberOfPolygons; i++)
            {
                L_c.AddSubject(_subj);
                L_c.AddClip(_clip);
                L_c.Execute(ClipType.Intersection, FillRule.NonZero, ref _solution);
                L_c.Clear();
                _solution.Clear();
            }
        }).Schedule();
        _subj.Dispose(Dependency);
        _clip.Dispose(Dependency);
        //World.EntityManager.DestroyEntity(GetSingletonEntity<ClipperStressTest>());
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
