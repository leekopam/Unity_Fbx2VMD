using UnityEngine;

namespace Member_Han.Modules.FBXImporter
{
    public class RuntimeRetargeter : MonoBehaviour
    {
        private HumanPoseHandler _srcHandler;
        private HumanPoseHandler _dstHandler;
        private HumanPose _pose;
        private bool _active = false;

        public void Initialize(Animator src, Animator dst)
        {
            if (src.avatar == null || !src.avatar.isValid || dst.avatar == null) return;
            
            _srcHandler = new HumanPoseHandler(src.avatar, src.transform);
            _dstHandler = new HumanPoseHandler(dst.avatar, dst.transform);
            _pose = new HumanPose();
            _active = true;
        }

        void LateUpdate()
        {
            if (!_active) return;
            _srcHandler.GetHumanPose(ref _pose); // Ghost의 올바른 자세 읽기
            _dstHandler.SetHumanPose(ref _pose);     // Target에 적용
        }
    }
}
