//using Chart3D.Components;
//using Chart3D.Components.IComparer;
//using Chart3D.Helper;
//using Chart3D.S57Helper;
//using S57;
//using Unity.Assertions;
//using Unity.Burst;
//using Unity.Collections;
//using Unity.Entities;
//using Unity.Jobs;
//using Unity.Mathematics;
//using Unity.Transforms;
//using UnityEngine;

//public class MinHeapTest : SystemBase
//{
//    protected override void OnUpdate()
//    {
//        var featureToVectorBuffer = GetBufferFromEntity<FeatureToVector>(true);
//        var s57NodesBuffer = GetBufferFromEntity<S57Nodes>(true);
//        var s57NodeCDFE = GetComponentDataFromEntity<S57Node>(true);
//        var vectorPointerCDFE = GetComponentDataFromEntity<VectorPointer>(true);

//        Entities
//            .WithName("MinHeapTest")
//            .WithReadOnly(featureToVectorBuffer)
//            .WithReadOnly(s57NodesBuffer)
//            .WithReadOnly(s57NodeCDFE)
//            .WithReadOnly(vectorPointerCDFE)
//            .ForEach((Entity entity, in S57Object jobS57Object, in S57GeometricPrimitive s57GeometricPrimitive) =>
//            {
//                if (jobS57Object.Value != S57Obj.DEPARE || s57GeometricPrimitive.Value != GeometricPrimitive.Area) return;

//                var featureToVector = featureToVectorBuffer[entity];
//                NativeList<int2> vertices = new NativeList<int2>(100, Allocator.Temp);
//                NativeList<int> holes = new NativeList<int>(0, Allocator.Temp);
//                GetGeometry.GetArea(ref vertices, ref holes, in vectorPointerCDFE, in s57NodeCDFE, in s57NodesBuffer, in featureToVector);
//                var bla = PolyLabelAccurate.GetPolyLabel(ref vertices, ref holes);
//            }).Schedule();
//    }
//}
