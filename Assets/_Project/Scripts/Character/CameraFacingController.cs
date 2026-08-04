using UnityEngine;

namespace Fbx2Vmd.Character
{
    /// <summary>
    /// 대상 오브젝트를 카메라 정면으로 회전시킨다.
    /// FBXVmdPipeline.FaceTargetToCamera()에서 추출 (청사진 단계 1-2).
    /// ponytail: Camera.main은 전역 캐싱 안 함 — 단일 프레임 호출이므로 비용 무시.
    /// </summary>
    public static class CameraFacingController
    {
        /// <summary>
        /// targetObject를 메인 카메라 방향으로 회전시킨다. Y축만 회전.
        /// </summary>
        public static void FaceTargetToCamera(GameObject targetObject, Camera targetCamera)
        {
            if (targetObject == null)
                return;

            if (targetCamera == null)
            {
                targetObject.transform.rotation = Quaternion.identity;
                return;
            }

            Vector3 directionToCamera = targetCamera.transform.position - targetObject.transform.position;
            directionToCamera.y = 0f;
            if (directionToCamera.sqrMagnitude <= 0.001f)
            {
                return;
            }

            targetObject.transform.rotation = Quaternion.LookRotation(directionToCamera.normalized, Vector3.up);
        }
    }
}
