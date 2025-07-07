using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(ElasticityCollider))]
public class ElasticityColliderEditor : Editor
{
    public override void OnInspectorGUI()
    {
        ElasticityCollider collider = (ElasticityCollider)target;

        EditorGUI.BeginChangeCheck();

        collider.type = (ColliderType)EditorGUILayout.EnumPopup("Collider Type", collider.type);

        GUI.enabled = false;
        EditorGUILayout.IntField("Collider ID", collider.colliderID);
        GUI.enabled = true;

        if (EditorGUI.EndChangeCheck())
        {
            EditorUtility.SetDirty(collider);
        }

        if (GUILayout.Button("Refresh All Colliders"))
        {
            var elasticityIntegration = FindObjectOfType<ElasticityIntegration>();
            if (elasticityIntegration != null)
            {
                elasticityIntegration.RefreshColliders();
            }
        }
    }

    private void OnSceneGUI()
    {
        ElasticityCollider collider = (ElasticityCollider)target;

        switch (collider.type)
        {
            case ColliderType.Sphere:
                Handles.color = new Color(0, 1, 0, 0.2f);
                Handles.SphereHandleCap(0, collider.transform.position,
                                       collider.transform.rotation,
                                       collider.transform.lossyScale.x,
                                       EventType.Repaint);
                break;

            case ColliderType.Cube:
                Handles.color = new Color(0, 0, 1, 0.2f);
                Handles.matrix = collider.transform.localToWorldMatrix;
                Handles.DrawWireCube(Vector3.zero, Vector3.one);
                break;

            case ColliderType.Plane:
                Handles.color = new Color(1, 0, 0, 0.2f);
                Vector3 normal = collider.transform.up;
                Vector3 position = collider.transform.position;
                Handles.DrawSolidDisc(position, normal, 2.0f);
                break;
        }
    }
}