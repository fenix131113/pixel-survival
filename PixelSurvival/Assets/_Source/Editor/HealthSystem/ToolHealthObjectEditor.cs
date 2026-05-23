using GameAssembly.HealthSystem;
using GameAssembly.ItemsSystem.Data;
using UnityEditor;

namespace Editor.HealthSystem
{
    [CustomEditor(typeof(ToolHealthObject))]
    public class ToolHealthObjectEditor : UnityEditor.Editor
    {
        private const int MINIMUM_LEVEL_MODE_INDEX = 1;

        private SerializedProperty _needTypeProperty;
        private SerializedProperty _levelRequirementModeProperty;
        private SerializedProperty _minimumRequiredToolProperty;

        private void OnEnable()
        {
            _needTypeProperty = serializedObject.FindProperty("needType");
            _levelRequirementModeProperty = serializedObject.FindProperty("levelRequirementMode");
            _minimumRequiredToolProperty = serializedObject.FindProperty("minimumRequiredTool");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawPropertiesExcluding(serializedObject, "m_Script", "needType", "levelRequirementMode", "minimumRequiredTool");
            EditorGUILayout.PropertyField(_needTypeProperty);

            var needType = (ToolType)_needTypeProperty.enumValueIndex;
            if (needType is ToolType.PICKAXE or ToolType.AXE)
            {
                EditorGUILayout.PropertyField(_levelRequirementModeProperty);

                if (_levelRequirementModeProperty.enumValueIndex == MINIMUM_LEVEL_MODE_INDEX)
                {
                    EditorGUILayout.PropertyField(_minimumRequiredToolProperty);

                    if (_minimumRequiredToolProperty.objectReferenceValue is ToolItemDefinitionSO minimumTool &&
                        minimumTool.ToolType != needType)
                    {
                        EditorGUILayout.HelpBox("Minimum required tool type must match Need Type.",
                            MessageType.Warning);
                    }
                }
            }
            else
            {
                _levelRequirementModeProperty.enumValueIndex = 0;
                _minimumRequiredToolProperty.objectReferenceValue = null;
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
