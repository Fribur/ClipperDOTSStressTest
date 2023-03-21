using Unity.Entities;
using Unity.Jobs;
using Clipper2Lib;
using Unity.Collections;
using Unity.Mathematics;

public partial class Clipper2Class : SystemBase
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

    protected override void OnUpdate()
    {
        if (SystemAPI.GetSingleton<ClipperStressTest>().clipperTestType != ClipperTestType.Clipper2Lib)
            return;
        //if (SystemAPI.GetSingleton<ClipperStressTest>().clipperTestType != ClipperTestType.Clipper2AoS)
        //    return;

        if (polygonQuery.IsEmpty)
            return;

        var polgyonEntities = polygonQuery.ToEntityArray(Allocator.Temp);
        Paths64 _subj=null;
        Paths64 _clip = null;
        Paths64 _solution = new Paths64();
        for (int i = 0, length= polgyonEntities.Length; i < length; i++)
        {
            var entity = polgyonEntities[i];
            var polyType = SystemAPI.GetComponent<PolygonType>(entity);
            var nodes = SystemAPI.GetBuffer<Nodes>(entity).Reinterpret<int2>();
            var startIDs = SystemAPI.GetBuffer<StartIDs>(entity).Reinterpret<int>();
            if (polyType.value==PolyType.Subject)
                _subj= StaticHelper.GetPaths64(nodes, startIDs);
            else if (polyType.value == PolyType.Clip)
                _clip = StaticHelper.GetPaths64(nodes, startIDs);
        }
        Job.WithoutBurst().WithCode(() =>
        {
            for (int i = 0; i < StaticHelper.numberOfPolygons; i++)
            {
                Clipper64 c = new Clipper64();
                c.AddSubject(_subj);
                c.AddClip(_clip);
                c.Execute(ClipType.Intersection, FillRule.NonZero, _solution);
                c.Clear();
                _solution.Clear();
            }
        }).Run();
    }    
}
