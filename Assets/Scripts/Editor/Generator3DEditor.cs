using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(Generator3D))]
public class Generator3DEditor : Editor {
    public override void OnInspectorGUI() {
        // Generator3D 컴포넌트 참조
        Generator3D generator = (Generator3D)target;

        // 기본 인스펙터 그리기
        DrawDefaultInspector();

        // 구분선 추가
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        EditorGUILayout.Space();

        // Generate 버튼 추가
        if (GUILayout.Button("Generate New Dungeon", GUILayout.Height(30))) {
            // 새로운 던전 생성
            generator.Generate();
        }

        // Clear 버튼 추가
        if (GUILayout.Button("Clear New Dungeon", GUILayout.Height(30))) {
            // 기존 던전 오브젝트 제거
            generator.Clear();
        }
    }
}