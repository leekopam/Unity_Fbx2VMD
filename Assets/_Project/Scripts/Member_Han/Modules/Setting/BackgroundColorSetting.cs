using UnityEngine;

[DisallowMultipleComponent]
public sealed class BackgroundColorSetting : MonoBehaviour
{
    [Header("대상")]
    [InspectorName("대상 카메라")]
    [Tooltip("배경색을 적용할 GameView 카메라입니다.")]
    [SerializeField] private Camera targetCamera;

    [Header("적용")]
    [InspectorName("실행 시작 시 자동 적용")]
    [Tooltip("Play Mode 시작 시 배경색을 자동으로 적용합니다.")]
    [SerializeField] private bool applyOnAwake = true;

    [InspectorName("Unity OnValidate 자동 적용")]
    [Tooltip("에디터에서 값이 바뀔 때 배경색을 즉시 적용합니다.")]
    [SerializeField] private bool applyOnValidate;

    [Header("카메라 배경")]
    [InspectorName("배경색 적용")]
    [Tooltip("켜져 있으면 대상 카메라를 Solid Color 배경으로 설정합니다.")]
    [SerializeField] private bool applyBackgroundColor = true;

    [InspectorName("배경색")]
    [Tooltip("GameView에 표시할 카메라 배경색입니다.")]
    [SerializeField] private Color backgroundColor = Color.black;

    public Camera TargetCamera => targetCamera;
    public bool ApplyBackgroundColor => applyBackgroundColor;
    public Color BackgroundColor => backgroundColor;

    private void Reset()
    {
        targetCamera = Camera.main;
    }

    private void Awake()
    {
        if (applyOnAwake)
        {
            ApplyNow();
        }
    }

    private void OnValidate()
    {
        if (applyOnValidate && !Application.isPlaying)
        {
            ApplyNow();
        }
    }

    public void SetBackgroundColor(Color color)
    {
        backgroundColor = color;
        applyBackgroundColor = true;
        ApplyNow();
    }

    public void ApplyNow()
    {
        Camera camera = ResolveTargetCamera();
        if (camera == null || !applyBackgroundColor)
        {
            return;
        }

        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = backgroundColor;
    }

    private Camera ResolveTargetCamera()
    {
        if (targetCamera != null)
        {
            return targetCamera;
        }

        targetCamera = Camera.main;
        return targetCamera;
    }
}
