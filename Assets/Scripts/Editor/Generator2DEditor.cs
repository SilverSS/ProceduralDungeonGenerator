using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(Generator2D))]
public class Generator2DEditor : Editor {
    public override void OnInspectorGUI() {
        Generator2D generator = (Generator2D)target;

        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        EditorGUILayout.Space();

        if (GUILayout.Button("Generate New Dungeon", GUILayout.Height(30))) {
            generator.Generate();
        }

        if (GUILayout.Button("Clear Dungeon", GUILayout.Height(30))) {
            generator.Clear();
        }
    }
}