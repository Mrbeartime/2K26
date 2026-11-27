using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public sealed class BackdropBlurCapture : MonoBehaviour
{
    [SerializeField, Range(2, 8)] private int downsample = 4;
    [SerializeField, Range(0, 4)] private int blurPasses = 2;

    private Image image;
    private Sprite originalSprite;
    private Color originalColor;
    private Texture2D blurredTexture;
    private Sprite blurredSprite;

    private void Awake()
    {
        image = GetComponent<Image>();
        originalSprite = image.sprite;
        originalColor = image.color;
    }

    /// <summary>Captures the already-rendered game view before its overlay is shown.</summary>
    public void CaptureBackdrop()
    {
        if (image == null) Awake();

        Texture2D screenshot = ScreenCapture.CaptureScreenshotAsTexture();
        if (screenshot == null) return;

        ReleaseCapturedTexture();

        int width = Mathf.Max(1, screenshot.width / downsample);
        int height = Mathf.Max(1, screenshot.height / downsample);
        Color32[] sourcePixels = screenshot.GetPixels32();
        Color32[] pixels = Downsample(sourcePixels, screenshot.width, screenshot.height, width, height);
        Object.Destroy(screenshot);

        for (int pass = 0; pass < blurPasses; pass++)
            pixels = BoxBlur(pixels, width, height);

        // Preserve the old black Image's 200/255 opacity while displaying the blur.
        float brightness = 1f - originalColor.a;
        for (int i = 0; i < pixels.Length; i++)
        {
            Color32 pixel = pixels[i];
            pixel.r = (byte)(pixel.r * brightness);
            pixel.g = (byte)(pixel.g * brightness);
            pixel.b = (byte)(pixel.b * brightness);
            pixel.a = 255;
            pixels[i] = pixel;
        }

        blurredTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "Overlay Backdrop Blur"
        };
        blurredTexture.SetPixels32(pixels);
        blurredTexture.Apply(false, false);
        blurredSprite = Sprite.Create(blurredTexture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);

        image.sprite = blurredSprite;
        image.color = Color.white;
    }

    private static Color32[] Downsample(Color32[] source, int sourceWidth, int sourceHeight, int width, int height)
    {
        var result = new Color32[width * height];
        for (int y = 0; y < height; y++)
        {
            int sourceY = Mathf.Min(sourceHeight - 1, y * sourceHeight / height);
            for (int x = 0; x < width; x++)
            {
                int sourceX = Mathf.Min(sourceWidth - 1, x * sourceWidth / width);
                result[y * width + x] = source[sourceY * sourceWidth + sourceX];
            }
        }
        return result;
    }

    private static Color32[] BoxBlur(Color32[] source, int width, int height)
    {
        var result = new Color32[source.Length];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int red = 0;
                int green = 0;
                int blue = 0;
                int alpha = 0;
                int samples = 0;
                for (int offsetY = -1; offsetY <= 1; offsetY++)
                {
                    int sampleY = Mathf.Clamp(y + offsetY, 0, height - 1);
                    for (int offsetX = -1; offsetX <= 1; offsetX++)
                    {
                        int sampleX = Mathf.Clamp(x + offsetX, 0, width - 1);
                        Color32 sample = source[sampleY * width + sampleX];
                        red += sample.r;
                        green += sample.g;
                        blue += sample.b;
                        alpha += sample.a;
                        samples++;
                    }
                }
                result[y * width + x] = new Color32(
                    (byte)(red / samples), (byte)(green / samples),
                    (byte)(blue / samples), (byte)(alpha / samples));
            }
        }
        return result;
    }

    private void OnDestroy()
    {
        ReleaseCapturedTexture();
    }

    private void ReleaseCapturedTexture()
    {
        if (blurredSprite != null) Object.Destroy(blurredSprite);
        if (blurredTexture != null) Object.Destroy(blurredTexture);
        blurredSprite = null;
        blurredTexture = null;
    }
}
