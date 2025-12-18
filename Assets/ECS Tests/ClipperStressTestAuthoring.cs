using Unity.Entities;
using UnityEngine;

public class ClipperStressTestAuthoring : MonoBehaviour
{
    public ClipperTestType clipperTestType;
}
class ClipperStressTestBaker : Baker<ClipperStressTestAuthoring>
{
    public override void Bake(ClipperStressTestAuthoring authoring)
    {
        var spawned = GetEntity(TransformUsageFlags.None);
        AddComponent(spawned, new ClipperStressTest { clipperTestType = authoring.clipperTestType });
    }
}

