using QuadD;
using UnityEngine;
using UnityEngine.Video;

// Camera-plane video does not appear in this project's URP renderer. Present the
// decoded texture in an overlay while leaving the shared intro's timing/audio intact.
[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(QuadDIntro), typeof(VideoPlayer))]
public sealed class QuadDIntroCanvasOutput : MonoBehaviour
{
    private QuadDIntro intro;
    private VideoPlayer video;
    private Canvas output;
    private UnityEngine.UI.RawImage image;
    private UnityEngine.UI.AspectRatioFitter aspect;
    private bool completed;
    private bool firstFrameReady;

    private void Awake()
    {
        intro = GetComponent<QuadDIntro>();
        video = GetComponent<VideoPlayer>();
        var canvasObject = new GameObject("Quad D Video Output", typeof(RectTransform), typeof(Canvas));
        canvasObject.transform.SetParent(transform, false);
        output = canvasObject.GetComponent<Canvas>();
        output.renderMode = RenderMode.ScreenSpaceOverlay;
        output.sortingOrder = 32760;
        var background = new GameObject("Preparation Background", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        background.transform.SetParent(canvasObject.transform, false);
        var backdrop = background.GetComponent<UnityEngine.UI.Image>();
        backdrop.color = new Color32(16, 21, 28, 255);
        backdrop.raycastTarget = false;
        var backgroundRect = background.GetComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = backgroundRect.offsetMax = Vector2.zero;
        var picture = new GameObject("Video", typeof(RectTransform), typeof(UnityEngine.UI.RawImage),
            typeof(UnityEngine.UI.AspectRatioFitter));
        picture.transform.SetParent(canvasObject.transform, false);
        image = picture.GetComponent<UnityEngine.UI.RawImage>();
        image.raycastTarget = false;
        image.color = Color.white;
        // A texture-less RawImage draws the UI white texture while the decoder prepares.
        image.enabled = false;
        var rect = picture.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        aspect = picture.GetComponent<UnityEngine.UI.AspectRatioFitter>();
        aspect.aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.FitInParent;
        aspect.aspectRatio = 16f / 9f;
        video.frameReady += OnFrameReady;
        video.sendFrameReadyEvents = true;
        intro.OnCompleted.AddListener(Complete);
    }

    private void OnFrameReady(VideoPlayer source, long frame)
    {
        if (completed || source != video || frame < 0) return;
        firstFrameReady = true;
        // Only the first decoded frame is needed; avoid a callback on every video frame.
        video.sendFrameReadyEvents = false;
    }

    private void LateUpdate()
    {
        if (completed) return;
        // The package initializes its player in Start, before this LateUpdate.
        video.renderMode = VideoRenderMode.APIOnly;
        if (!firstFrameReady || video.texture == null)
        {
            image.enabled = false;
            return;
        }
        image.texture = video.texture;
        image.enabled = true;
        if (video.texture.height > 0)
            aspect.aspectRatio = (float)video.texture.width / video.texture.height;
    }

    private void Complete()
    {
        completed = true;
        image.enabled = false;
        image.texture = null;
        video.sendFrameReadyEvents = false;
        output.enabled = false;
    }

    private void OnDestroy()
    {
        if (intro != null) intro.OnCompleted.RemoveListener(Complete);
        if (video != null) video.frameReady -= OnFrameReady;
    }
}
