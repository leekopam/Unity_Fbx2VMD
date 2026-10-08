namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 툰 템플릿 머티리얼의 역할 구분.
    /// Opaque/Cutout/Transparent는 표면 방식, Skin/Hair/Eye는 캐릭터 부위 구분이다.
    /// </summary>
    public enum ToonMaterialRole
    {
        Opaque,
        Cutout,
        Transparent,
        Skin,
        Hair,
        Eye,
        Face,
    }
}
