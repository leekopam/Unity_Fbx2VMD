using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

using BoneNames = UnityHumanoidVMDRecorder.BoneNames;

namespace Tests.Editor.Upgrade
{
    /// <summary>
    /// VMD 바이너리 골든 계약. 고정 입력으로 VMD를 쓰고 SHA256+길이를 JSON으로 남긴다.
    /// 엔진 버전이 달라도 VMD 출력은 바이트 동일이어야 한다 — 해시가 바뀌면 출력 회귀.
    /// </summary>
    [Category("UpgradeReadiness")]
    public class VmdGoldenBinaryTests
    {
        [Test]
        public void FixedInput_ProducesStableVmdBinary()
        {
            List<BoneNames> activeBones = Enum.GetValues(typeof(BoneNames))
                .Cast<BoneNames>()
                .Where(b => b != BoneNames.None && (int)b > 5)
                .Take(2)
                .ToList();

            const int frameCount = 5;
            var positions = new Dictionary<BoneNames, List<Vector3>>();
            var rotations = new Dictionary<BoneNames, List<Quaternion>>();

            foreach (BoneNames bone in activeBones)
            {
                positions[bone] = Enumerable.Repeat(new Vector3(1f, 2f, 3f), frameCount).ToList();
                rotations[bone] = Enumerable.Repeat(Quaternion.identity, frameCount).ToList();
            }

            string tempDir = Path.Combine(Path.GetTempPath(), "fbx2vmd-upgrade-readiness");
            Directory.CreateDirectory(tempDir);
            string filePath = Path.Combine(tempDir, $"vmd_golden_{Guid.NewGuid():N}.vmd");

            try
            {
                VmdFileWriter.WriteVmdFile(
                    modelName: "upgradeReadiness",
                    filePath: filePath,
                    activeBones: activeBones,
                    frameCount: frameCount,
                    keyReductionLevel: 2,
                    positionDictionarySaved: positions,
                    rotationDictionarySaved: rotations,
                    morphSnapshot: null,
                    useCenterAsParentOfAll: false,
                    routeCenterBoneToGroove: false,
                    centerNameString: "CENTER",
                    grooveNameString: "GROOVE");

                byte[] bytes = File.ReadAllBytes(filePath);
                Assert.That(bytes.Length, Is.GreaterThan(0), "VMD 출력이 비어 있음");

                string sha256;
                using (var sha = SHA256.Create())
                {
                    sha256 = BitConverter.ToString(sha.ComputeHash(bytes))
                        .Replace("-", "").ToLowerInvariant();
                }

                // 헤더 문자열은 VMD 포맷 계약 그 자체다.
                string header = Encoding.ASCII.GetString(bytes, 0, Math.Min(30, bytes.Length));
                Assert.That(header, Does.StartWith("Vocaloid Motion Data 0002"),
                    "VMD 헤더가 포맷 계약과 다름");

                string report = "{\n"
                    + $"  \"sha256\": \"{sha256}\",\n"
                    + $"  \"length\": {bytes.Length},\n"
                    + $"  \"bones\": {activeBones.Count},\n"
                    + $"  \"frames\": {frameCount}\n"
                    + "}\n";
                string reportPath = UpgradeReadinessReport.Write("vmd-golden.json", report);
                Assert.That(File.Exists(reportPath), Is.True, "VMD 골든 리포트 미작성");

                Debug.Log($"[UpgradeReadiness] VMD golden sha256={sha256} length={bytes.Length}");
            }
            finally
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
        }
    }
}
