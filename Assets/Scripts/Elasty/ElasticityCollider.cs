using UnityEngine;
using System;

public enum ColliderType
{
    Plane = 0,
    Sphere = 1,
    Cube = 2
}

[Serializable]
public class ElasticityCollider : MonoBehaviour
{
    public ColliderType type = ColliderType.Sphere;

    private Transform targetTransform;

    [HideInInspector]
    public int colliderID = 0;

    void Awake()
    {
        targetTransform = transform;
    }

    // Obtener la matriz de transformación actual
    public Matrix4x4 GetTransformMatrix()
    {
        return targetTransform.localToWorldMatrix;
    }

    // Obtener datos de posición y escala
    public Vector3 GetPosition()
    {
        return targetTransform.position;
    }

    public Vector3 GetScale()
    {
        return targetTransform.lossyScale;
    }

    public Quaternion GetRotation()
    {
        return targetTransform.rotation;
    }
}