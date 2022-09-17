using PolygonMath;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using PolygonMath.Clipping.Clipper2LibBURST;
using PolygonMath.Clipping;
using static Unity.Entities.EntityQueryBuilder;

public partial class ClipperStruct : SystemBase
{
    //ClipperD c;
    Polygon _subj;
    Polygon _clip;
    const int DisplayWidth = 800;
    const int DisplayHeight = 600;
    public int EdgeCount = 1000;

    protected override void OnCreate()
    {
        //c = new ClipperD(Allocator.Persistent);
        StaticHelper.GenerateRandomPolygon(new Random(1337), DisplayWidth, DisplayHeight, EdgeCount, out _subj, out _clip);
        RequireSingletonForUpdate<ClipperStressTest>();
    }
    protected override void OnDestroy()
    {
        //c.Dispose();
        if (_subj.IsCreated) _subj.Dispose();
        if (_clip.IsCreated) _clip.Dispose();
    }

    protected override void OnUpdate()
    {
        if (GetSingleton<ClipperStressTest>().clipperTestType != ClipperTestType.Clipper2Struct_Intersection)
            return;

        var L_subj = _subj;
        var L_clip = _clip;
        //var L_c = c;
        Job
            .WithoutBurst()
            .WithCode(() =>
        {
            ClipperD L_c = new ClipperD(Allocator.Temp);
            Polygon _solution = new Polygon(2000, Allocator.Temp);            
            for (int i = 0; i < 10; i++)
            {
                L_c.AddSubject(L_subj);
                L_c.AddClip(L_clip);
                L_c.Execute(ClipType.Intersection, FillRule.NonZero, ref _solution);
                L_c.Clear();
                _solution.Clear();
            }

        }).Schedule();
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
