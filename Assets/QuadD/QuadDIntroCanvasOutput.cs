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
    private GameObject preparationIndicator;
    private UnityEngine.UI.Image[] preparationBars;
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
        preparationIndicator = new GameObject("Preparation Indicator", typeof(RectTransform));
        preparationIndicator.transform.SetParent(canvasObject.transform, false);
        var indicatorRect = preparationIndicator.GetComponent<RectTransform>();
        indicatorRect.anchorMin = new Vector2(.46f, .18f);
        indicatorRect.anchorMax = new Vector2(.54f, .184f);
        indicatorRect.offsetMin = indicatorRect.offsetMax = Vector2.zero;
        preparationBars = new UnityEngine.UI.Image[3];
        for (int i = 0; i < preparationBars.Length; i++)
        {
            var barObject = new GameObject("Pulse " + (i + 1), typeof(RectTransform), typeof(UnityEngine.UI.Image));
            barObject.transform.SetParent(preparationIndicator.transform, false);
            var bar = barObject.GetComponent<UnityEngine.UI.Image>();
            bar.color = new Color32(250, 137, 71, 153);
            bar.raycastTarget = false;
            var barRect = barObject.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(.05f + i * .35f, 0);
            barRect.anchorMax = new Vector2(.25f + i * .35f, 1);
            barRect.offsetMin = barRect.offsetMax = Vector2.zero;
            preparationBars[i] = bar;
        }
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
            for (int i = 0; i < preparationBars.Length; i++)
            {
                Color color = preparationBars[i].color;
                color.a = .35f + .45f * (.5f + .5f * Mathf.Sin(Time.unscaledTime * 4.5f - i * 1.2f));
                preparationBars[i].color = color;
            }
            return;
        }
        if (preparationIndicator.activeSelf) preparationIndicator.SetActive(false);
        image.texture = video.texture;
        image.enabled = true;
        if (video.texture.height > 0)
            aspect.aspectRatio = (float)video.texture.width / video.texture.height;
    }

    private void Complete()
    {
        completed = true;
        preparationIndicator.SetActive(false);
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
