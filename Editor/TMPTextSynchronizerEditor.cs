#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Reflection;

namespace TMPTextSynchronizer.Editor
{
    [CustomEditor(typeof(FontSizeSynchronizer))]
    public class TMPTextSynchronizerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            GUILayout.Space(10);

            EditorGUILayout.LabelField("TMP Text Synchronizer Tools", EditorStyles.boldLabel);

            FontSizeSynchronizer synchronizer = (FontSizeSynchronizer)target;

            // Synchronize 버튼
            if (GUILayout.Button("🔄 Synchronize Now"))
            {
                synchronizer.SynchronizeFontSizes();

                // _hasInitialized 플래그를 true로 설정 (Reflection 사용)
                var fieldInfo = typeof(FontSizeSynchronizer).GetField("_hasInitialized",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (fieldInfo != null)
                {
                    fieldInfo.SetValue(synchronizer, true);
                }

                EditorUtility.SetDirty(target);
            }

            GUILayout.Space(5);

            // 현재 폰트 크기 표시
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Current Font Size:", GUILayout.Width(120));
            EditorGUILayout.LabelField($"{synchronizer.GetCurrentFontSize():F1} pt", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();
        }
    }

    public static class TMPSyncMenuItems
    {
        [MenuItem("Tools/TMP Text Synchronizer/Add to Selected", false, 0)]
        public static void AddSynchronizerToSelected()
        {
            foreach (GameObject obj in Selection.gameObjects)
            {
                if (obj.GetComponent<FontSizeSynchronizer>() == null)
                {
                    Undo.AddComponent<FontSizeSynchronizer>(obj);
                }
            }
        }

        [MenuItem("Tools/TMP Text Synchronizer/Remove from Selected", false, 1)]
        public static void RemoveSynchronizerFromSelected()
        {
            foreach (GameObject obj in Selection.gameObjects)
            {
                var comp = obj.GetComponent<FontSizeSynchronizer>();
                if (comp != null)
                {
                    Undo.DestroyObjectImmediate(comp);
                }
            }
        }

        [MenuItem("Tools/TMP Text Synchronizer/Synchronize All in Scene", false, 20)]
        public static void SynchronizeAllInScene()
        {
            var synchronizers = Object.FindObjectsByType<FontSizeSynchronizer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var sync in synchronizers)
            {
                sync.SynchronizeFontSizes();

                // _hasInitialized 플래그를 true로 설정
                var fieldInfo = typeof(FontSizeSynchronizer).GetField("_hasInitialized",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (fieldInfo != null)
                {
                    fieldInfo.SetValue(sync, true);
                }
            }
            Debug.Log($"Synchronized {synchronizers.Length} TMP Text Synchronizer(s)");
        }
    }
}
#endif