using Clipper2Lib;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

using Random = Unity.Mathematics.Random;

public partial class ClipperClass : SystemBase
{
    private List<List<Point64>> _subj;
    private List<List<Point64>> _clip;
    private List<List<Point64>> _solution;
    private const int DisplayWidth = 800;
    private const int DisplayHeight = 600;
    public int EdgeCount = 1000;

    protected override void OnCreate()
    {
        StaticHelper.GenerateRandomPath(new Random(1337), DisplayWidth, DisplayHeight, EdgeCount, out _subj, out _clip);
        _solution=new List<List<Point64>>();
        RequireSingletonForUpdate<ClipperStressTest>();
    }

    protected override void OnUpdate()
    {
        if (GetSingleton<ClipperStressTest>().clipperTestType != ClipperTestType.Clipper2Class_Intersection)
            return;

        var L_subj = _subj;
        var L_clip = _clip;
        var L_solution = _solution;
        Job.WithoutBurst().WithCode(() =>
        {
            Clipper c = new Clipper();
            for (int i = 0; i < 10; i++)
            {
                c.AddSubject(L_subj);
                c.AddClip(L_clip);
                c.Execute(ClipType.Intersection, FillRule.NonZero, L_solution);
                c.Clear();
                L_solution.Clear();
            }
           
        }).Run();
    }    
}
