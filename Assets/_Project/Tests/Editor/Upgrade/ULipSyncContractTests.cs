using NUnit.Framework;
using UnityEngine;
using ULipSyncComponent = uLipSync.uLipSync;
using ULipSyncProfileAsset = uLipSync.Profile;

namespace Tests.Editor.Upgrade
{
    /// <summary>
    /// uLipSync 로컬 패키지 계약: 주요 컴포넌트 생성과 프로필 에셋 생성이
    /// 패키지 버전 변경 후에도 동작하는지 검증한다.
    /// </summary>
    [Category("UpgradeReadiness")]
    public class ULipSyncContractTests
    {
        [Test]
        public void ULipSyncComponent_AddsToGameObject()
        {
            var go = new GameObject("uLipSync_ContractProbe");
            try
            {
                var component = go.AddComponent<ULipSyncComponent>();
                Assert.That(component, Is.Not.Null, "uLipSync 컴포넌트 AddComponent 실패");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ULipSyncProfile_CreatesAsScriptableObject()
        {
            ULipSyncProfileAsset profile = null;
            try
            {
                profile = ScriptableObject.CreateInstance<ULipSyncProfileAsset>();
                Assert.That(profile, Is.Not.Null, "uLipSync Profile 에셋 생성 실패");
            }
            finally
            {
                if (profile != null)
                {
                    Object.DestroyImmediate(profile);
                }
            }
        }
    }
}
