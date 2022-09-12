using Clipper2Lib;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Paths64 = System.Collections.Generic.List<System.Collections.Generic.List<Clipper2Lib.Point64>>;
using Path64 = System.Collections.Generic.List<Clipper2Lib.Point64>;

using Random = Unity.Mathematics.Random;
//using PolygonMath.Clipping.Clipper2LibBURST;
using UnityEngine;

public partial class ClipperClass : SystemBase
{
    private Paths64 _subj;
    private Paths64 _clip;
    private Paths64 _solution;
    private const int DisplayWidth = 800;
    private const int DisplayHeight = 600;
    public int EdgeCount = 1000;

    protected override void OnCreate()
    {
        StaticHelper.GenerateRandomPath(new Random(1337), DisplayWidth, DisplayHeight, EdgeCount, out _subj, out _clip);
        _solution= new Paths64();
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
            for (int i = 0; i < 10; i++)
            {
                //L_solution = Clipper.Intersect(L_subj, L_clip, FillRule.NonZero);
                Clipper64 c = new Clipper64();
                c.AddSubject(L_subj);
                c.AddClip(L_clip);
                c.Execute(ClipType.Intersection, FillRule.NonZero, L_solution);
                c.Clear();
                L_solution.Clear();
            }
        }).Run();
    }    
}
