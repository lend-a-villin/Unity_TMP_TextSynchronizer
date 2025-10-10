using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TMPTextSynchronizer
{
    public enum ResizeType
    {
        ResizeToMinSize,
        ResizeToMaxSize,
        ResizeToAvgSize
    }

    [ExecuteAlways]
    [AddComponentMenu("UI/TMP Text Synchronizer")]
    public class FontSizeSynchronizer : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private ResizeType _fontSizeCalculationMethod = ResizeType.ResizeToMinSize;
        [SerializeField] private bool _autoUpdateOnScreenSizeChange = true;
        [SerializeField] private float _minimumFontSize = 1f;
        [SerializeField] private float _maximumFontSize = 1024f;
        [Tooltip("Force override nested FontSizeSynchronizer components and manage all texts")]
        [SerializeField] private bool _forceOverrideNested = false;

        [Header("Performance")]
        [Tooltip("Maximum calculations per second (FPS limit for font size updates)")]
        [SerializeField][Range(1, 300)] private int _maxCalculationFPS = 20;
        [Tooltip("Apply FPS limit to manual SynchronizeFontSizes() calls")]
        [SerializeField] private bool _applyFPSLimitToManualCalls = false;

        [Header("Debug")]
        [SerializeField] private bool _showDebugLogs = false;

        // Cached components
        private HashSet<TMP_Text> _textComponents = new HashSet<TMP_Text>();
        private Dictionary<TMP_Text, float> _originalFontSizes = new Dictionary<TMP_Text, float>();
        private Vector2 _lastScreenSize;
        private bool _isProcessing = false;
        private bool _hasInitialized = false;
        private bool _isDirty = true;

        // Time tracking for FPS limiting
        private float _lastCalculationTime = 0f;

        // Optimization: Reusable collections
        private List<float> _optimalSizesCache = new List<float>(16);
        private List<FontSizeSynchronizer> _nestedSynchronizersCache = new List<FontSizeSynchronizer>(8);
        private List<TMP_Text> _componentsToRemoveCache = new List<TMP_Text>(8);
        private List<TMP_Text> _allTextsCache = new List<TMP_Text>(32);

        // Optimization: Hierarchy caching for IsChildOf optimization
        private Dictionary<FontSizeSynchronizer, HashSet<Transform>> _nestedHierarchyCache
            = new Dictionary<FontSizeSynchronizer, HashSet<Transform>>();
        private bool _hierarchyCacheDirty = true;

        // Optimization: Reusable StringBuilder
        private StringBuilder _pathBuilder = new StringBuilder(256);

        // Optimization: Cached coroutine YieldInstructions
        private WaitForEndOfFrame _waitForEndOfFrame;
        private WaitForSeconds _waitForLayout;

        // Events
        public System.Action<float> OnFontSizeSynchronized;

        #region Unity Lifecycle
        private void OnEnable()
        {
            if (!_hasInitialized)
            {
                Initialize();
            }
        }

        private void Awake()
        {
            // Cache YieldInstructions for reuse
            _waitForEndOfFrame = new WaitForEndOfFrame();
            _waitForLayout = new WaitForSeconds(0.05f);

            if (!_hasInitialized)
            {
                Initialize();
            }
        }

        private void Start()
        {
            if (Application.isPlaying && gameObject.activeInHierarchy)
            {
                StartCoroutine(DelayedSynchronization());
            }
        }

        private void Update()
        {
            if (!_autoUpdateOnScreenSizeChange || _isProcessing) return;

            // Check FPS limit
            if (!CanCalculateNow(false)) return;

            // Check for screen size changes
            if (_lastScreenSize.x != Screen.width || _lastScreenSize.y != Screen.height)
            {
                _lastScreenSize.Set(Screen.width, Screen.height);
                _lastCalculationTime = Time.unscaledTime;
                SynchronizeFontSizes();
            }
        }

        private void OnDestroy()
        {
#if UNITY_EDITOR
            EditorApplication.delayCall -= DelayedSynchronize;
#endif
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying && _hasInitialized)
            {
                MarkDirty();
                EditorApplication.delayCall -= DelayedSynchronize;
                EditorApplication.delayCall += DelayedSynchronize;
            }
        }

        private void DelayedSynchronize()
        {
            if (this != null)
            {
                RefreshTextComponents();
                SynchronizeFontSizes();
            }
        }
#endif
        #endregion

        #region Unity Coroutines
        private IEnumerator DelayedSynchronization()
        {
            // Use cached YieldInstructions
            yield return _waitForEndOfFrame;
            yield return _waitForLayout;

            SynchronizeFontSizes();
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Manually trigger font size synchronization
        /// </summary>
        public void SynchronizeFontSizes()
        {
            SynchronizeFontSizes(false);
        }

        /// <summary>
        /// Manually trigger font size synchronization with FPS limit bypass option
        /// </summary>
        /// <param name="ignoreFPSLimit">If true, bypass FPS limit for this call</param>
        public void SynchronizeFontSizes(bool ignoreFPSLimit)
        {
            if (_isProcessing) return;

            // Apply FPS limit based on settings
            if (!ignoreFPSLimit && _applyFPSLimitToManualCalls && !CanCalculateNow(true))
            {
                LogDebug("Skipped synchronization due to FPS limit");
                return;
            }

            RefreshTextComponents();

            if (_textComponents.Count == 0)
            {
                LogWarning($"No text components found on '{gameObject.name}'");
                return;
            }

            _isProcessing = true;

            try
            {
                float targetFontSize = CalculateOptimalFontSize();
                float clampedSize = Mathf.Clamp(targetFontSize, _minimumFontSize, _maximumFontSize);

                if (clampedSize != targetFontSize)
                {
                    LogWarning($"Calculated font size ({targetFontSize:F1}) is out of range. Clamping to {clampedSize:F1}pt (range: {_minimumFontSize}-{_maximumFontSize})");
                    targetFontSize = clampedSize;
                }

                ApplyFontSizeToAll(targetFontSize);
                OnFontSizeSynchronized?.Invoke(targetFontSize);

                // Update time even for manual calls
                if (_applyFPSLimitToManualCalls)
                {
                    _lastCalculationTime = Time.unscaledTime;
                }

                LogDebug($"Font size synchronized to: {targetFontSize:F1}");
            }
            catch (System.Exception ex)
            {
                LogError($"Error during font size synchronization: {ex.Message}");
            }
            finally
            {
                _isProcessing = false;
            }
        }

        /// <summary>
        /// Add a text component to synchronization Set
        /// </summary>
        public void AddTextComponent(TMP_Text textComponent)
        {
            if (textComponent == null) return;

            if (_textComponents.Add(textComponent))
            {
                if (!_originalFontSizes.TryGetValue(textComponent, out _))
                {
                    _originalFontSizes[textComponent] = textComponent.fontSize;
                }
                MarkDirty();
                SynchronizeFontSizes();
            }
        }

        /// <summary>
        /// Remove a text component from synchronization Set
        /// </summary>
        public void RemoveTextComponent(TMP_Text textComponent)
        {
            if (textComponent == null) return;

            if (_textComponents.Remove(textComponent))
            {
                _originalFontSizes.Remove(textComponent);
                MarkDirty();
                SynchronizeFontSizes();
            }
        }

        /// <summary>
        /// Get current synchronized font size
        /// </summary>
        public float GetCurrentFontSize()
        {
            foreach (var text in _textComponents)
            {
                if (!ReferenceEquals(text, null) && text) return text.fontSize;
            }

            foreach (var kvp in _originalFontSizes)
            {
                if (!ReferenceEquals(kvp.Key, null) && kvp.Key) return kvp.Value;
            }

            return _minimumFontSize;
        }

        /// <summary>
        /// Mark text components as dirty to force refresh on next synchronization
        /// </summary>
        public void MarkDirty()
        {
            _isDirty = true;
            _hierarchyCacheDirty = true;
        }

        /// <summary>
        /// Set maximum calculation FPS at runtime
        /// </summary>
        public void SetMaxCalculationFPS(int fps)
        {
            _maxCalculationFPS = Mathf.Clamp(fps, 1, 300);
            LogDebug($"Max calculation FPS set to: {_maxCalculationFPS}");
        }

        /// <summary>
        /// Get current maximum calculation FPS
        /// </summary>
        public int GetMaxCalculationFPS()
        {
            return _maxCalculationFPS;
        }
        #endregion

        #region Core Logic
        private void Initialize()
        {
            if (_hasInitialized) return;

            _lastScreenSize.Set(Screen.width, Screen.height);
            _lastCalculationTime = Time.unscaledTime;
            RefreshTextComponents();
            RemoveNestedSynchronizers();
            _hasInitialized = true;
        }

        /// <summary>
        /// FPS limit check: Verify if calculation can be performed
        /// </summary>
        private bool CanCalculateNow(bool isManualCall)
        {
            // Execute immediately without FPS limit in editor
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                return true;
            }
#endif

            float timeSinceLastCalculation = Time.unscaledTime - _lastCalculationTime;
            float minInterval = 1f / _maxCalculationFPS;

            return timeSinceLastCalculation >= minInterval;
        }

        private float CalculateOptimalFontSize()
        {
            _optimalSizesCache.Clear();

            // Check if Canvas update is needed
            bool needsCanvasUpdate = false;
            foreach (var text in _textComponents)
            {
                if (!ReferenceEquals(text, null) && text && text.enabled)
                {
                    needsCanvasUpdate = true;
                    break;
                }
            }

            if (needsCanvasUpdate)
            {
                Canvas.ForceUpdateCanvases();
            }

            foreach (var text in _textComponents)
            {
                if (ReferenceEquals(text, null) || !text) continue;

                float size = GetOptimalFontSizeForText(text);
                LogDebug($"{text.name}: optimal size = {size:F1}");
                _optimalSizesCache.Add(size);
            }

            return CalculateTargetFontSize(_optimalSizesCache);
        }

        private float GetOptimalFontSizeForText(TMP_Text textComponent)
        {
            bool originalAutoSizing = textComponent.enableAutoSizing;
            float originalFontSize = textComponent.fontSize;
            float originalMinSize = textComponent.fontSizeMin;
            float originalMaxSize = textComponent.fontSizeMax;

            try
            {
                float workingMinSize = originalMinSize > 0 ? originalMinSize : _minimumFontSize;
                float workingMaxSize = originalMaxSize > 0 ? originalMaxSize : _maximumFontSize;

                textComponent.enableAutoSizing = true;
                textComponent.fontSizeMin = workingMinSize;
                textComponent.fontSizeMax = workingMaxSize;

                textComponent.ForceMeshUpdate(true, true);

                float calculatedSize = textComponent.fontSize;

                LogDebug($"{textComponent.name}: autoSize calculated = {calculatedSize:F1} (min: {workingMinSize:F1}, max: {workingMaxSize:F1})");

                if (calculatedSize < workingMinSize)
                {
                    float fallbackSize = GetOriginalFontSize(textComponent);
                    if (fallbackSize > 0)
                    {
                        LogWarning($"{textComponent.name}: Calculated size ({calculatedSize:F1}) is invalid. Using original size ({fallbackSize:F1})");
                        return fallbackSize;
                    }
                }

                return calculatedSize;
            }
            catch (System.Exception ex)
            {
                LogError($"Error calculating font size for {textComponent.name}: {ex.Message}");
                return GetOriginalFontSize(textComponent);
            }
            finally
            {
                textComponent.enableAutoSizing = originalAutoSizing;
                textComponent.fontSize = originalFontSize;
                textComponent.fontSizeMin = originalMinSize;
                textComponent.fontSizeMax = originalMaxSize;
            }
        }

        private float GetOriginalFontSize(TMP_Text textComponent)
        {
            if (_originalFontSizes.TryGetValue(textComponent, out float originalSize))
            {
                return originalSize;
            }
            return textComponent.fontSize > 0 ? textComponent.fontSize : _minimumFontSize;
        }

        private float CalculateTargetFontSize(List<float> fontSizes)
        {
            if (fontSizes.Count == 0) return _minimumFontSize;

            float result = fontSizes[0];

            switch (_fontSizeCalculationMethod)
            {
                case ResizeType.ResizeToMaxSize:
                    for (int i = 1; i < fontSizes.Count; i++)
                    {
                        if (fontSizes[i] > result)
                            result = fontSizes[i];
                    }
                    break;

                case ResizeType.ResizeToMinSize:
                    for (int i = 1; i < fontSizes.Count; i++)
                    {
                        if (fontSizes[i] < result)
                            result = fontSizes[i];
                    }
                    break;

                case ResizeType.ResizeToAvgSize:
                    float sum = result;
                    for (int i = 1; i < fontSizes.Count; i++)
                    {
                        sum += fontSizes[i];
                    }
                    result = sum / fontSizes.Count;
                    break;

                default:
                    for (int i = 1; i < fontSizes.Count; i++)
                    {
                        if (fontSizes[i] < result)
                            result = fontSizes[i];
                    }
                    break;
            }

            return result;
        }

        private void ApplyFontSizeToAll(float targetFontSize)
        {
            foreach (var textComponent in _textComponents)
            {
                if (ReferenceEquals(textComponent, null) || !textComponent) continue;

                textComponent.enableAutoSizing = false;
                textComponent.fontSize = targetFontSize;
                textComponent.ForceMeshUpdate(false, false);
            }
        }
        #endregion

        #region Helper Methods
        private void RefreshTextComponents()
        {
            if (!_isDirty) return;

            // Store GetComponentsInChildren results in reusable list
            _allTextsCache.Clear();
            GetComponentsInChildren(true, _allTextsCache);

            // Clear existing HashSet instead of creating new one
            _textComponents.Clear();

            if (_forceOverrideNested)
            {
                foreach (var text in _allTextsCache)
                {
                    if (IsValidTextComponent(text))
                    {
                        _textComponents.Add(text);

                        if (!_originalFontSizes.TryGetValue(text, out _))
                        {
                            _originalFontSizes[text] = text.fontSize;
                        }
                    }
                }
            }
            else
            {
                // Refresh hierarchy cache
                _nestedSynchronizersCache.Clear();
                foreach (var sync in GetComponentsInChildren<FontSizeSynchronizer>(true))
                {
                    if (sync != this)
                    {
                        _nestedSynchronizersCache.Add(sync);
                    }
                }

                if (_hierarchyCacheDirty)
                {
                    RebuildHierarchyCache();
                    _hierarchyCacheDirty = false;
                }

                foreach (var text in _allTextsCache)
                {
                    if (IsValidTextComponent(text) && !IsUnderNestedSynchronizerCached(text))
                    {
                        _textComponents.Add(text);

                        if (!_originalFontSizes.TryGetValue(text, out _))
                        {
                            _originalFontSizes[text] = text.fontSize;
                        }
                    }
                }
            }

            // Clean up removed components from original sizes dictionary
            _componentsToRemoveCache.Clear();
            foreach (var kvp in _originalFontSizes)
            {
                if (!_textComponents.Contains(kvp.Key))
                {
                    _componentsToRemoveCache.Add(kvp.Key);
                }
            }

            foreach (var component in _componentsToRemoveCache)
            {
                _originalFontSizes.Remove(component);
            }

            _isDirty = false;

            LogDebug($"Found {_textComponents.Count} valid text components");
        }

        /// <summary>
        /// Rebuild hierarchy cache
        /// </summary>
        private void RebuildHierarchyCache()
        {
            _nestedHierarchyCache.Clear();

            foreach (var sync in _nestedSynchronizersCache)
            {
                if (ReferenceEquals(sync, null) || !sync || sync == this) continue;

                var hierarchy = new HashSet<Transform>();
                BuildHierarchyCache(sync.transform, hierarchy);
                _nestedHierarchyCache[sync] = hierarchy;
            }
        }

        /// <summary>
        /// Cache hierarchy in HashSet
        /// </summary>
        private void BuildHierarchyCache(Transform root, HashSet<Transform> cache)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                cache.Add(child);
                BuildHierarchyCache(child, cache);
            }
        }

        /// <summary>
        /// Use cached HashSet instead of IsChildOf
        /// </summary>
        private bool IsUnderNestedSynchronizerCached(TMP_Text textComponent)
        {
            Transform textTransform = textComponent.transform;

            foreach (var kvp in _nestedHierarchyCache)
            {
                if (kvp.Value.Contains(textTransform))
                {
                    LogDebug($"Excluding '{textComponent.name}' - under nested synchronizer");
                    return true;
                }
            }
            return false;
        }

        private bool IsValidTextComponent(TMP_Text textComponent)
        {
            return !ReferenceEquals(textComponent, null) && textComponent && textComponent.transform != transform;
        }

        private void RemoveNestedSynchronizers()
        {
            if (!_forceOverrideNested) return;

            var nestedSynchronizers = new List<FontSizeSynchronizer>();
            foreach (var sync in GetComponentsInChildren<FontSizeSynchronizer>(true))
            {
                if (sync != this && sync.transform != transform)
                {
                    nestedSynchronizers.Add(sync);
                }
            }

            foreach (var nested in nestedSynchronizers)
            {
                string objectPath = GetGameObjectPath(nested.transform);

                if (Application.isPlaying)
                {
                    LogWarning($"Nested FontSizeSynchronizer detected and removed from: {objectPath}");
                    Destroy(nested);
                }
#if UNITY_EDITOR
                else
                {
                    LogWarning($"Nested FontSizeSynchronizer detected from: {objectPath}");
                }
#endif
            }
        }

        private string GetGameObjectPath(Transform transform)
        {
            _pathBuilder.Clear();
            _pathBuilder.Append(transform.name);
            Transform parent = transform.parent;

            while (parent != null && parent != this.transform)
            {
                _pathBuilder.Insert(0, '/');
                _pathBuilder.Insert(0, parent.name);
                parent = parent.parent;
            }

            return _pathBuilder.ToString();
        }

        private void LogDebug(string message)
        {
            if (_showDebugLogs)
            {
                Debug.Log($"[TMP Text Synchronizer] {message}");
            }
        }

        private void LogWarning(string message)
        {
            Debug.LogWarning($"[TMP Text Synchronizer] {message}", this);
        }

        private void LogError(string message)
        {
            Debug.LogError($"[TMP Text Synchronizer] {message}", this);
        }
        #endregion

        #region Editor Utilities
#if UNITY_EDITOR
        [ContextMenu("Synchronize Font Sizes")]
        private void EditorSynchronizeFontSizes()
        {
            MarkDirty();
            SynchronizeFontSizes(true);
        }

        [ContextMenu("Show Text Components Info")]
        private void ShowTextComponentsInfo()
        {
            MarkDirty();
            RefreshTextComponents();

            Debug.Log($"=== Font Size Synchronizer Info ===");
            Debug.Log($"Calculation Method: {_fontSizeCalculationMethod}");
            Debug.Log($"Text Components Count: {_textComponents.Count}");
            Debug.Log($"Auto Update: {_autoUpdateOnScreenSizeChange}");
            Debug.Log($"Max Calculation FPS: {_maxCalculationFPS}");

            foreach (var text in _textComponents)
            {
                if (ReferenceEquals(text, null) || !text) continue;

                float originalSize = _originalFontSizes.TryGetValue(text, out float size) ? size : 0f;
                Debug.Log($"- {text.name}: fontSize={text.fontSize:F1}, originalSize={originalSize:F1}, autoSizing={text.enableAutoSizing}");
            }
        }

        [ContextMenu("Reset to Default Settings")]
        private void ResetToDefaults()
        {
            _fontSizeCalculationMethod = ResizeType.ResizeToMinSize;
            _autoUpdateOnScreenSizeChange = true;
            _maxCalculationFPS = 144;
            _applyFPSLimitToManualCalls = false;
            _showDebugLogs = false;
            MarkDirty();

            Debug.Log("[FontSizeSynchronizer] Settings reset to defaults");
        }
#endif
        #endregion
    }
}