using System;
using UniGLTF;
using UnityEngine;
using VRM;
using Object = UnityEngine.Object;

namespace Fbx2Vmd.CharacterLibrary
{
    /// <summary>
    /// VRM 메타 읽기 결과. UnityEngine.Object 대신 복사된 값만 담아 소유권 문제를 피한다.
    /// </summary>
    public sealed class VrmCharacterMetaResult
    {
        public bool success;
        public string error = string.Empty;
        public string specVersion = string.Empty;
        public CharacterLibraryEntryMeta meta = new CharacterLibraryEntryMeta();
        public byte[] thumbnailPng;
    }

    /// <summary>
    /// 전체 모델을 로드하지 않고 VRM 헤더만 읽어 메타데이터와 내장 썸네일을 추출한다.
    /// 메인 스레드에서 호출해야 한다(텍스처 생성 포함).
    /// </summary>
    public static class VrmCharacterMetaReader
    {
        /// <summary>
        /// .vrm 파일의 메타데이터를 읽는다. VRM 0.x가 아니면 success=false와 함께
        /// VRM 1.0 여부를 구분한 오류 메시지를 돌려준다.
        /// </summary>
        public static VrmCharacterMetaResult Read(string path)
        {
            var result = new VrmCharacterMetaResult();
            try
            {
                using (GltfData data = new AutoGltfFileParser(path).Parse())
                {
                    VRMData vrm;
                    try
                    {
                        vrm = new VRMData(data);
                    }
                    catch (NotVrm0Exception)
                    {
                        result.error = IsVrm10(data)
                            ? "VRM 1.0 파일은 현재 지원하지 않습니다. (UniVRM10 패키지가 필요합니다)"
                            : "VRM 0.x 형식이 아닌 파일입니다.";
                        return result;
                    }

                    result.specVersion = vrm.VrmExtension != null
                        ? (vrm.VrmExtension.specVersion ?? string.Empty)
                        : string.Empty;

                    using (var context = new VRMImporterContext(vrm))
                    {
                        VRMMetaObject meta = context.ReadMeta(createThumbnail: true);
                        try
                        {
                            CopyMeta(meta, result.meta);
                            result.thumbnailPng = TryEncodeThumbnail(meta);
                        }
                        finally
                        {
                            // 메타 읽기 경로에서 만든 오브젝트는 명시적으로 해제한다.
                            if (meta != null)
                            {
                                if (meta.Thumbnail != null)
                                {
                                    Object.DestroyImmediate(meta.Thumbnail);
                                }
                                Object.DestroyImmediate(meta);
                            }
                        }
                    }
                }

                result.success = true;
            }
            catch (Exception error)
            {
                result.error = $"VRM 메타 읽기 실패: {error.Message}";
            }
            return result;
        }

        private static void CopyMeta(VRMMetaObject source, CharacterLibraryEntryMeta target)
        {
            target.title = source.Title ?? string.Empty;
            target.version = source.Version ?? string.Empty;
            target.author = source.Author ?? string.Empty;
            target.contactInformation = source.ContactInformation ?? string.Empty;
            target.reference = source.Reference ?? string.Empty;
            target.licenseType = source.LicenseType.ToString();
            target.allowedUser = source.AllowedUser.ToString();
            target.commercialUsage = source.CommercialUssage.ToString();
            target.otherLicenseUrl = source.OtherLicenseUrl ?? string.Empty;
        }

        private static byte[] TryEncodeThumbnail(VRMMetaObject meta)
        {
            if (meta == null || meta.Thumbnail == null)
            {
                return null;
            }

            try
            {
                return meta.Thumbnail.EncodeToPNG();
            }
            catch (Exception)
            {
                // 읽기 불가능한 텍스처 등은 썸네일 없이 진행한다.
                return null;
            }
        }

        /// <summary>
        /// glTF 확장 목록에 VRMC_vrm이 있으면 VRM 1.0 계열로 판정한다.
        /// </summary>
        private static bool IsVrm10(GltfData data)
        {
            string json = data != null ? data.Json : null;
            return !string.IsNullOrEmpty(json) && json.Contains("\"VRMC_vrm\"");
        }
    }
}
