using PolygonMath;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using PolygonMath.Clipping.Clipper2LibBURST;
using PolygonMath.Clipping;

public partial class ClipperStructBURSTJobs : SystemBase
{
    private Polygon _subj;
    private Polygon _clip;
    private const int DisplayWidth = 800;
    private const int DisplayHeight = 600;
    public int EdgeCount = 1000;

    protected override void OnCreate()
    {
        StaticHelper.GenerateRandomPolygon(new Random(1337), DisplayWidth, DisplayHeight, EdgeCount, out _subj, out _clip);
        RequireSingletonForUpdate<ClipperStressTest>();
    }
    protected override void OnDestroy()
    {
        if (_subj.IsCreated) _subj.Dispose();
        if (_clip.IsCreated) _clip.Dispose();
    }

    protected override void OnUpdate()
    {
        if (GetSingleton<ClipperStressTest>().clipperTestType != ClipperTestType.Clipper2StructBURSTJobs_Intersection)
            return;

        var L_subj = _subj;
        var L_clip = _clip;
        NativeArray<JobHandle> jobHandles = new NativeArray<JobHandle>(11, Allocator.TempJob);
        for (int i = 0; i < 10; i++)
        {
            jobHandles[i] = Job.WithBurst().WithReadOnly(L_subj).WithReadOnly(L_clip).WithCode(() =>
            {
                Polygon _solution = new Polygon(2000, Allocator.Temp);
                ClipperD c = new ClipperD(Allocator.Temp);
                c.AddSubject(L_subj);
                c.AddClip(L_clip);
                c.Execute(ClipType.Intersection, FillRule.NonZero, ref _solution);
                c.Clear();
                _solution.Clear();
            }).Schedule(Dependency);
        }
        jobHandles[10] = Dependency;
        Dependency = JobHandle.CombineDependencies(jobHandles);
        jobHandles.Dispose(Dependency);
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
