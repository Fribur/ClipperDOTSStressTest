using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public enum ClipperTestType
{
    None,

    Clipper2Class_Intersection,
    Clipper2Struct_Intersection,
    Clipper2StructBURST_Intersection,
    Clipper2StructBURSTJobs_Intersection,
}

[Serializable]
[GenerateAuthoringComponent]
public struct ClipperStressTest : IComponentData
{
    public ClipperTestType clipperTestType;
    //public int edgeCount;
}


[Serializable]
public struct IsInitialized : IComponentData
{
}
