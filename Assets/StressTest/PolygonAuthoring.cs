using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Random = Unity.Mathematics.Random;

public class ClipAuthoringAuthoring : MonoBehaviour
{
    public PolyType polyType;
    public int width = StaticHelper.DisplayWidth;
    public int height = StaticHelper.DisplayHeight;
    public int nodesCount = StaticHelper.edgeCount;
}
class ClipAuthoringBaker : Baker<ClipAuthoringAuthoring>
{
    public override void Bake(ClipAuthoringAuthoring authoring)
    {
        var spawned = GetEntity(TransformUsageFlags.None);
        AddComponent(spawned, new PolygonType { value = authoring.polyType });
        var nodesBuffer = AddBuffer<Nodes>(spawned).Reinterpret<int2>();
        var rand = new Random(1337);
        StaticHelper.GenerateRandomNodes(rand, ref nodesBuffer, authoring.width, authoring.height, authoring.nodesCount);
        var startIDsBuffer = AddBuffer<StartIDs>(spawned).Reinterpret<int>();
        startIDsBuffer.Add(0);
        startIDsBuffer.Add(nodesBuffer.Length);
    }
}

