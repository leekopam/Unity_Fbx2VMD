using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class RuntimeMeshBoneWeightCalculatorTests
    {
        private const string CalculatorTypeName =
            "Fbx2Vmd.FBXImporter.RuntimeMeshBoneWeightCalculator";

        [Test]
        public void Given_RuntimeMeshImport_When_CheckingOwnership_Then_DelegatesBoneWeightCalculation()
        {
            Type calculatorType = typeof(AssimpFBXImporter).Assembly.GetType(CalculatorTypeName);
            string importerSource = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "_Project",
                "Scripts",
                "FBXImporter",
                "AssimpFBXImporter.cs"));

            Assert.That(calculatorType, Is.Not.Null);
            Assert.That(importerSource, Does.Contain("RuntimeMeshBoneWeightCalculator.Add("));
            Assert.That(importerSource, Does.Contain("RuntimeMeshBoneWeightCalculator.Normalize("));
            Assert.That(importerSource, Does.Not.Contain("private void AddBoneWeight("));
            Assert.That(importerSource, Does.Not.Contain("private static void NormalizeBoneWeights("));
        }

        [Test]
        public void Given_MoreThanFourPositiveWeights_When_Adding_Then_PreservesFirstFourSlots()
        {
            MethodInfo addMethod = FindCalculatorMethod(
                "Add",
                typeof(BoneWeight).MakeByRefType(),
                typeof(int).MakeByRefType(),
                typeof(int),
                typeof(float));
            var boneWeight = new BoneWeight();
            int count = 0;

            InvokeAdd(addMethod, ref boneWeight, ref count, 99, 0f);
            InvokeAdd(addMethod, ref boneWeight, ref count, 98, -1f);
            Assert.That(count, Is.Zero);
            Assert.That(boneWeight.weight0, Is.Zero);

            InvokeAdd(addMethod, ref boneWeight, ref count, 10, 1f);
            InvokeAdd(addMethod, ref boneWeight, ref count, 20, 2f);
            InvokeAdd(addMethod, ref boneWeight, ref count, 30, 3f);
            InvokeAdd(addMethod, ref boneWeight, ref count, 40, 4f);
            InvokeAdd(addMethod, ref boneWeight, ref count, 50, 5f);

            Assert.That(count, Is.EqualTo(5));
            Assert.That(boneWeight.boneIndex0, Is.EqualTo(10));
            Assert.That(boneWeight.boneIndex1, Is.EqualTo(20));
            Assert.That(boneWeight.boneIndex2, Is.EqualTo(30));
            Assert.That(boneWeight.boneIndex3, Is.EqualTo(40));
            Assert.That(boneWeight.weight0, Is.EqualTo(1f));
            Assert.That(boneWeight.weight1, Is.EqualTo(2f));
            Assert.That(boneWeight.weight2, Is.EqualTo(3f));
            Assert.That(boneWeight.weight3, Is.EqualTo(4f));
        }

        [Test]
        public void Given_AssignedBoneWeights_When_Normalizing_Then_ProducesUnitTotal()
        {
            MethodInfo normalizeMethod = FindCalculatorMethod(
                "Normalize",
                typeof(BoneWeight[]));
            BoneWeight[] weights =
            {
                new BoneWeight
                {
                    weight0 = 1f,
                    weight1 = 1f,
                    weight2 = 2f,
                    weight3 = 4f
                },
                new BoneWeight(),
                new BoneWeight
                {
                    weight0 = -2f,
                    weight1 = 1f
                }
            };

            normalizeMethod.Invoke(null, new object[] { weights });

            Assert.That(weights[0].weight0, Is.EqualTo(0.125f));
            Assert.That(weights[0].weight1, Is.EqualTo(0.125f));
            Assert.That(weights[0].weight2, Is.EqualTo(0.25f));
            Assert.That(weights[0].weight3, Is.EqualTo(0.5f));
            Assert.That(weights[1].weight0, Is.Zero);
            Assert.That(weights[2].weight0, Is.EqualTo(-2f));
            Assert.That(weights[2].weight1, Is.EqualTo(1f));
        }

        private static MethodInfo FindCalculatorMethod(string methodName, params Type[] parameterTypes)
        {
            Type calculatorType = typeof(AssimpFBXImporter).Assembly.GetType(CalculatorTypeName);
            Assert.That(calculatorType, Is.Not.Null);

            MethodInfo method = calculatorType.GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: parameterTypes,
                modifiers: null);
            Assert.That(method, Is.Not.Null);
            return method;
        }

        private static void InvokeAdd(
            MethodInfo addMethod,
            ref BoneWeight boneWeight,
            ref int count,
            int boneIndex,
            float weight)
        {
            object[] arguments = { boneWeight, count, boneIndex, weight };
            addMethod.Invoke(null, arguments);
            boneWeight = (BoneWeight)arguments[0];
            count = (int)arguments[1];
        }
    }
}
