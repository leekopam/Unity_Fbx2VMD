using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

internal sealed class VmdMorphRecorder
{
    List<SkinnedMeshRenderer> skinnedMeshRendererList;
    //キーはunity上のモーフ名
    public Dictionary<string, MorphDriver> MorphDrivers { get; private set; } = new Dictionary<string, MorphDriver>();

    public VmdMorphRecorder(Transform model, int expectedFrameCapacity = 0)
    {
        List<SkinnedMeshRenderer> searchBlendShapeSkins(Transform t)
        {
            List<SkinnedMeshRenderer> skinnedMeshRendererList = new List<SkinnedMeshRenderer>();
            Queue queue = new Queue();
            queue.Enqueue(t);
            while (queue.Count != 0)
            {
                SkinnedMeshRenderer skinnedMeshRenderer = (queue.Peek() as Transform).GetComponent<SkinnedMeshRenderer>();

                if (skinnedMeshRenderer != null && skinnedMeshRenderer.sharedMesh.blendShapeCount != 0)
                {
                    skinnedMeshRendererList.Add(skinnedMeshRenderer);
                }

                foreach (Transform childT in (queue.Dequeue() as Transform))
                {
                    queue.Enqueue(childT);
                }
            }

            return skinnedMeshRendererList;
        }
        skinnedMeshRendererList = searchBlendShapeSkins(model);

        foreach (SkinnedMeshRenderer skinnedMeshRenderer in skinnedMeshRendererList)
        {
            int morphCount = skinnedMeshRenderer.sharedMesh.blendShapeCount;
            for (int i = 0; i < morphCount; i++)
            {
                string morphName = skinnedMeshRenderer.sharedMesh.GetBlendShapeName(i);
                ////モーフ名に重複があれば2コ目以降は無視
                if (MorphDrivers.ContainsKey(morphName)) { continue; }
                MorphDrivers.Add(morphName, new MorphDriver(skinnedMeshRenderer, i, expectedFrameCapacity));
            }
        }
    }

    public void RecrodAllMorph()
    {
        foreach (MorphDriver morphDriver in MorphDrivers.Values)
        {
            morphDriver.RecordMorph();
        }
    }

    public void TrimMorphNumber()
    {
        string dot = ".";
        Dictionary<string, MorphDriver> morphDriversTemp = new Dictionary<string, MorphDriver>();
        foreach (string morphName in MorphDrivers.Keys)
        {
            string outputName = morphName;
            //正規表現使うより、dot探して整数か見る
            if (morphName.Contains(dot) && int.TryParse(morphName.Substring(0, morphName.IndexOf(dot)), out int dummy))
            {
                outputName = morphName.Substring(morphName.IndexOf(dot) + 1);
            }

            if (morphDriversTemp.ContainsKey(outputName))
            {
                continue;
            }

            morphDriversTemp.Add(outputName, MorphDrivers[morphName]);
        }
        MorphDrivers = morphDriversTemp;
    }

    public void DisableIntron()
    {
        foreach (string morphName in MorphDrivers.Keys)
        {
            for (int i = 0; i < MorphDrivers[morphName].ValueList.Count; i++)
            {
                //情報がなければ次へ
                if (MorphDrivers[morphName].ValueList.Count == 0) { continue; }
                //今、前、後が同じなら不必要なので無効化
                if (i > 0
                    && i < MorphDrivers[morphName].ValueList.Count - 1
                    && MorphDrivers[morphName].ValueList[i].value == MorphDrivers[morphName].ValueList[i - 1].value
                    && MorphDrivers[morphName].ValueList[i].value == MorphDrivers[morphName].ValueList[i + 1].value)
                {
                    MorphDrivers[morphName].ValueList[i] = (MorphDrivers[morphName].ValueList[i].value, false);
                }
            }
        }
    }

    public class MorphDriver
    {
        const float MorphAmplifier = 0.01f;
        public SkinnedMeshRenderer SkinnedMeshRenderer { get; private set; } = new SkinnedMeshRenderer();
        public int MorphIndex { get; private set; }

        public List<(float value, bool enabled)> ValueList { get; private set; }

        public MorphDriver(SkinnedMeshRenderer skinnedMeshRenderer, int morphIndex, int expectedFrameCapacity = 0)
        {
            SkinnedMeshRenderer = skinnedMeshRenderer;
            MorphIndex = morphIndex;
            ValueList = expectedFrameCapacity > 0
                ? new List<(float value, bool enabled)>(expectedFrameCapacity)
                : new List<(float value, bool enabled)>();
        }

        public void RecordMorph()
        {
            ValueList.Add((SkinnedMeshRenderer.GetBlendShapeWeight(MorphIndex) * MorphAmplifier, true));
        }
    }
}
