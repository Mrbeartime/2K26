using System.Collections;
using System.Collections.Generic;
using ProjectD.Menus;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Spawns the level's starting characters (from SceneCatalog) into the Resolve "Character Icons"
/// container and can place a star stamp over the ones that died in the level.
/// Driven by LevelCompleteSequence; it does not run anything on its own.
/// </summary>
public class LevelCompleteCharacterIcons : MonoBehaviour
{
    [Header("Container")]
    [Tooltip("Object with the HorizontalLayoutGroup. Defaults to this object's RectTransform.")]
    [SerializeField] private RectTransform iconContainer;

    [Header("Icon look")]
    [SerializeField, Min(1f)] private float iconWidth = 160f;
    [SerializeField] private bool preserveAspect = true;
    [Tooltip("Optional. Leave empty to use SceneCatalog's Quest Character Icon Material (same as Level Select).")]
    [SerializeField] private Material outlineMaterialOverride;

    [Header("Source")]
    [Tooltip("Optional. Leave empty to use the catalog's characterAvailable for this scene's level.")]
    [SerializeField] private Sprite[] overrideSprites;

    [Header("Star Stamp (dead characters)")]
    [Tooltip("Assign StarStamp-Icon.png.")]
    [SerializeField] private Sprite starStampSprite;
    [SerializeField, Min(0.1f), Tooltip("Final size relative to the icon box.")] private float stampSize = 1f;
    [SerializeField, Min(1f), Tooltip("Scale the stamp starts at before it lands.")] private float stampStartScale = 2.5f;
    [SerializeField] private Color stampColor = Color.white;

    [Header("SceneCatalog character names")]
    [Tooltip("Names used in SceneCatalog's Quest Characters list, per character class.")]
    [SerializeField] private string swordManName = "Warrior";
    [SerializeField] private string archerName = "Archer";
    [SerializeField] private string rogueName = "Rogue";

    private sealed class Slot
    {
        public Image Outline;
        public Image Stamp;
        public Sprite Sprite;
    }

    private readonly List<Slot> slots = new List<Slot>();
    private int activeCount;
    private CanvasGroup containerGroup;
    private Animator animator;   // optional, on this object; needs states 'Hidden' and 'PopIn'
    private bool warnedMissingStamp;

    private const string HiddenState = "Hidden";
    private const string ShowState = "PopIn";

    /// <summary>Builds the icons for the current level (hidden stamps). Returns how many icons exist.</summary>
    public int Prepare()
    {
        if (iconContainer == null) iconContainer = transform as RectTransform;
        if (iconContainer == null) return 0;
        if (animator == null) TryGetComponent(out animator);
        if (containerGroup == null && !iconContainer.TryGetComponent(out containerGroup))
            containerGroup = iconContainer.gameObject.AddComponent<CanvasGroup>();

        var material = outlineMaterialOverride != null
            ? outlineMaterialOverride
            : GameSceneManager.Catalog?.questCharacterIconMaterial;

        Sprite[] sprites = overrideSprites != null && overrideSprites.Length > 0 ? overrideSprites : CatalogSprites();

        int count = 0;
        foreach (var sprite in sprites ?? System.Array.Empty<Sprite>())
        {
            if (sprite == null) continue;
            if (count == slots.Count) slots.Add(CreateSlot());

            var slot = slots[count++];
            slot.Sprite = sprite;
            slot.Outline.material = material;
            slot.Outline.enabled = material != null;
            slot.Outline.GetComponent<LayoutElement>().preferredWidth = iconWidth;
            slot.Outline.sprite = sprite;
            slot.Outline.transform.Find("Portrait").GetComponent<Image>().sprite = sprite;
            slot.Stamp.enabled = false;
            slot.Outline.gameObject.SetActive(true);
        }

        for (int i = count; i < slots.Count; i++)
            slots[i].Outline.gameObject.SetActive(false);

        activeCount = count;
        return count;
    }

    /// <summary>
    /// Shows or hides the whole icon row. With an Animator on this object it plays the 'PopIn' / 'Hidden'
    /// state (instant jumps to the end); otherwise it just switches alpha.
    /// </summary>
    public void SetVisible(bool visible, bool instant = false)
    {
        if (animator != null && animator.runtimeAnimatorController != null && animator.isActiveAndEnabled)
        {
            animator.Play(visible ? ShowState : HiddenState, 0, visible && instant ? 1f : 0f);
            animator.Update(0f);
            return;
        }
        if (containerGroup != null) containerGroup.alpha = visible ? 1f : 0f;
    }

    /// <summary>Waits until the 'PopIn' animation has finished (returns at once without an Animator).</summary>
    public IEnumerator WaitUntilShown(System.Func<bool> skip)
    {
        if (animator == null || animator.runtimeAnimatorController == null || !animator.isActiveAndEnabled) yield break;
        while (!skip())
        {
            var info = animator.GetCurrentAnimatorStateInfo(0);
            if (!info.IsName(ShowState) || info.normalizedTime >= 1f) yield break;
            yield return null;
        }
    }

    /// <summary>Indices of icons whose character died in this level (by class, matched through SceneCatalog).</summary>
    public List<int> FindDeadSlots()
    {
        var result = new List<int>();
        var catalog = GameSceneManager.Catalog;
        if (catalog == null) return result;

        foreach (var character in FindObjectsByType<Character>(FindObjectsInactive.Include))
        {
            if (character == null || !character.IsDead || character.gameObject.scene != gameObject.scene) continue;

            string catalogName = character switch
            {
                SwordMan => swordManName,
                ArcherMan => archerName,
                Rogue => rogueName,
                _ => null
            };
            Sprite sprite = string.IsNullOrEmpty(catalogName) ? null : catalog.FindQuestCharacter(catalogName);
            if (sprite == null)
            {
                Debug.LogWarning("LevelCompleteCharacterIcons: no catalog icon for " + character.name + ".", this);
                continue;
            }

            for (int i = 0; i < activeCount; i++)
            {
                if (result.Contains(i) || slots[i].Sprite != sprite) continue;
                result.Add(i);
                break;
            }
        }

        result.Sort();
        return result;
    }

    /// <summary>progress 0 hides the stamp; 0..1 plays the landing; 1 is the final stamp.</summary>
    public void SetStamp(int index, float progress)
    {
        if (index < 0 || index >= activeCount) return;
        var stamp = slots[index].Stamp;

        if (starStampSprite == null)
        {
            if (!warnedMissingStamp)
            {
                warnedMissingStamp = true;
                Debug.LogWarning("LevelCompleteCharacterIcons: Star Stamp Sprite is not assigned.", this);
            }
            return;
        }
        if (progress <= 0f)
        {
            stamp.enabled = false;
            return;
        }

        float p = Mathf.Clamp01(progress);
        float eased = 1f - Mathf.Pow(1f - p, 3f);
        stamp.sprite = starStampSprite;
        stamp.enabled = true;
        stamp.rectTransform.localScale = Vector3.one * (Mathf.Lerp(stampStartScale, 1f, eased) * stampSize);
        Color color = stampColor;
        color.a *= Mathf.Clamp01(p / 0.3f);
        stamp.color = color;
    }

    private Sprite[] CatalogSprites()
    {
        var catalog = GameSceneManager.Catalog;
        string path = gameObject.scene.path;
        Sprite[] sprites = null;
        if (catalog != null)
        {
            sprites = catalog.FindLevelByScene(path)?.characterAvailable
                      ?? catalog.FindSecretLevelByScene(path)?.characterAvailable;
        }
        if (sprites == null)
            Debug.LogWarning("LevelCompleteCharacterIcons: no catalog level matches scene '" + path + "'.", this);
        return sprites;
    }

    private Slot CreateSlot()
    {
        var root = new GameObject("Character Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        root.layer = iconContainer.gameObject.layer;
        root.transform.SetParent(iconContainer, false);

        var outline = root.GetComponent<Image>();
        outline.preserveAspect = preserveAspect;
        outline.raycastTarget = false;
        root.AddComponent<SpriteOutlinePadding>();

        var portraitObject = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
        portraitObject.layer = root.layer;
        var portrait = portraitObject.GetComponent<Image>();
        var portraitRect = portrait.rectTransform;
        portraitRect.SetParent(root.transform, false);
        Stretch(portraitRect);
        portrait.preserveAspect = preserveAspect;
        portrait.raycastTarget = false;

        var stampObject = new GameObject("Star Stamp", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        stampObject.layer = root.layer;
        stampObject.GetComponent<LayoutElement>().ignoreLayout = true;
        var stamp = stampObject.GetComponent<Image>();
        Stretch(stamp.rectTransform);
        stamp.rectTransform.SetParent(root.transform, false);
        Stretch(stamp.rectTransform);
        stamp.preserveAspect = true;
        stamp.raycastTarget = false;
        stamp.enabled = false;

        return new Slot { Outline = outline, Stamp = stamp };
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
