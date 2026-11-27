using System.Collections.Generic;
using UnityEngine;

/// <summary>Overrides Outline_MAT's color per model without changing the shared material.</summary>
[DisallowMultipleComponent]
[AddComponentMenu("Rendering/AllOutline-Color")]
public sealed class AllOutlineColor : MonoBehaviour
{
    [System.Serializable]
    public sealed class OutlineEntry
    {
        [InspectorName("Object")]
        [Tooltip("Scene object whose meshes and child meshes receive this outline color. Meshes must already be on the Outline layer.")]
        public GameObject target;
        public Color color = Color.white;
    }

    [Tooltip("Assign scene objects and colors. If entries overlap, the last entry wins. Changes apply during Play mode.")]
    [SerializeField] private List<OutlineEntry> elements = new List<OutlineEntry>();

    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private readonly List<SavedBlock> savedBlocks = new List<SavedBlock>();
    private MaterialPropertyBlock workingBlock;
    private bool refreshRequested;

    private sealed class SavedBlock
    {
        public Renderer renderer;
        public int materialIndex;
        public MaterialPropertyBlock original;
    }

    private void OnEnable() => RefreshOutline();
    private void OnDisable() => RestoreBlocks();

    // Defer Renderer API calls out of OnValidate; previews update during Play mode.
    private void OnValidate() => refreshRequested = true;

    private void LateUpdate()
    {
        if (refreshRequested) RefreshOutline();
    }

    [ContextMenu("Refresh Outline (Play Mode)")]
    public void RefreshOutline()
    {
        if (!Application.isPlaying || !isActiveAndEnabled) return;
        refreshRequested = false;
        RestoreBlocks();
        if (workingBlock == null) workingBlock = new MaterialPropertyBlock();

        // Resolve duplicates before saving blocks, so each renderer is restored only once.
        var colors = new Dictionary<Renderer, Color>();
        foreach (OutlineEntry entry in elements)
        {
            if (entry == null || entry.target == null) continue;
            foreach (Renderer target in entry.target.GetComponentsInChildren<Renderer>(true))
            {
                if (target is MeshRenderer || target is SkinnedMeshRenderer)
                    colors[target] = entry.color;
            }
        }

        foreach (KeyValuePair<Renderer, Color> pair in colors)
        {
            Renderer target = pair.Key;
            Color color = pair.Value;
            SaveAndApply(target, -1, color);

            // Existing per-material blocks take priority over the renderer-wide block.
            for (int i = 0; i < target.sharedMaterials.Length; i++)
            {
                target.GetPropertyBlock(workingBlock, i);
                if (!workingBlock.isEmpty) SaveAndApply(target, i, color);
            }
        }
    }

    private void SaveAndApply(Renderer target, int index, Color color)
    {
        var original = new MaterialPropertyBlock();
        if (index < 0) target.GetPropertyBlock(original);
        else target.GetPropertyBlock(original, index);
        savedBlocks.Add(new SavedBlock { renderer = target, materialIndex = index, original = original });

        if (index < 0) target.GetPropertyBlock(workingBlock);
        else target.GetPropertyBlock(workingBlock, index);
        workingBlock.SetColor(OutlineColorId, color);
        if (index < 0) target.SetPropertyBlock(workingBlock);
        else target.SetPropertyBlock(workingBlock, index);
    }

    private void RestoreBlocks()
    {
        foreach (SavedBlock saved in savedBlocks)
        {
            if (saved.renderer == null) continue;
            MaterialPropertyBlock block = saved.original.isEmpty ? null : saved.original;
            if (saved.materialIndex < 0) saved.renderer.SetPropertyBlock(block);
            else saved.renderer.SetPropertyBlock(block, saved.materialIndex);
        }
        savedBlocks.Clear();
    }
}
