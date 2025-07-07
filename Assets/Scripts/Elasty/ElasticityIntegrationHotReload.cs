using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.IO;

/// <summary>
/// Hot-reloadable version of ElasticityIntegration using dynamic DLL loading
/// </summary>
public class ElasticityIntegrationHotReload : MonoBehaviour
{
    [Header("=== Hot Reload Settings ===")]
    [Tooltip("Enable hot reloading of the DLL")]
    public bool enableHotReload = true;
    
    [Tooltip("Show reload notifications in console")]
    public bool showReloadNotifications = true;
    
    // All other fields remain the same as original ElasticityIntegration
    [Header("=== Simulation Settings ===")]
    [Tooltip("Height of the ground plane for the simulation")]
    public float groundHeight = 0.0f;

    [Tooltip("Whether to visualize stress on the model")]
    public bool visualizeStress = true;

    [Tooltip("Whether to load uv on the model")]
    public bool useProvidedUV = true;

    public float maxStress = 20000.0f;

    [Tooltip("Material to use for normal rendering")]
    public Material defaultMaterial;

    [Tooltip("Material to use for stress visualization")]
    public Material stressMaterial;

    // Time stepping parameters
    [Header("Time Stepping Parameters")]
    [Range(1, 100)]
    public int numSubsteps = 20;
    [Range(1, 20)]
    public int numIterations = 5;

    // Material parameters
    [Header("Material Properties")]
    [Range(1000.0f, 500000.0f)]
    public float youngsModulus = 200000.0f;
    [Range(0.0f, 0.5f)]
    public float poissonRatio = 0.49999f;
    [Range(0.1f, 10.0f)]
    public float snhCompliance = 2.0f;

    // Friction parameters
    [Header("Friction Parameters")]
    [Range(0.0f, 1.0f)]
    public float frictionStatic = 0.3f;
    [Range(0.0f, 1.0f)]
    public float frictionDynamic = 0.8f;
    [Range(10.0f, 500.0f)]
    public float friction = 171.0f;

    // Collision parameters
    [Header("Collision Parameters")]
    [Range(0.001f, 0.1f)]
    public float collisionMargin = 0.01f;
    public bool enableSelfCollision = true;

    // Solver parameters
    [Header("Solver Parameters")]
    [Range(0.1f, 2.0f)]
    public float relaxationFactor = 1.2f;
    [Range(0.0f, 10.0f)]
    public float damping = 3.0f;

    [Header("Elastic Body Configuration")]
    [Tooltip("Name of the mesh to use (without extension)")]
    public string meshName = "collapsed_body";

    [Tooltip("Position of the elastic body")]
    public Vector3 bodyPosition = new Vector3(0.0f, 2.0f, 0.0f);

    [Tooltip("Rotation of the elastic body (Euler angles)")]
    public Vector3 bodyRotation = Vector3.zero;

    [Tooltip("Uniform scale of the elastic body")]
    public float bodyScale = 0.9f;

    [Tooltip("Comma-separated list of vertex indices to attach")]
    public string attachedIndicesStr = "";

    [Header("Resource Path Configuration")]
    [Tooltip("Use custom resource paths instead of default paths")]
    public bool useCustomResourcePaths = false;

    [Tooltip("Root path to the resources directory (leave empty to use StreamingAssets/ElasticityData)")]
    public string customResourcePath = "";

    // Dynamic DLL loader
    private ElasticityDynamicDLL dll;
    
    // Simulation handle from native DLL
    private int simulationHandle = 0;

    // Mesh data
    private Mesh mesh;
    private Vector3[] vertices;
    private int[] triangles;
    private float[] stressValues;
    private Color[] colors;

    // GameObject components
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;

    // For parameter change detection
    private int lastNumSubsteps;
    private int lastNumIterations;
    private float lastYoungsModulus;
    private float lastPoissonRatio;
    private float lastSnhCompliance;
    private float lastFrictionStatic;
    private float lastFrictionDynamic;
    private float lastFriction;
    private float lastCollisionMargin;
    private bool lastEnableSelfCollision;
    private float lastRelaxationFactor;
    private float lastDamping;

    private List<ElasticityCollider> activeColliders = new List<ElasticityCollider>();

    private string lastMeshName;
    private Vector3 lastBodyPosition;
    private Vector3 lastBodyRotation;
    private float lastBodyScale;

    public bool isInitialized { get; private set; } = false;
    public event Action OnInitialized;

    void Awake()
    {
        // Create mesh components if they don't exist
        if (meshFilter == null)
            meshFilter = gameObject.AddComponent<MeshFilter>();

        if (meshRenderer == null)
            meshRenderer = gameObject.AddComponent<MeshRenderer>();

        meshRenderer.material = defaultMaterial;
        
        // Initialize dynamic DLL loader
        dll = new ElasticityDynamicDLL();
        if (enableHotReload)
        {
            dll.OnDllReloaded += OnDllReloaded;
        }
    }

    void Start()
    {
        // Create a parent GameObject to mirror the mesh
        GameObject parent = new GameObject("MirrorParent");
        transform.parent = parent.transform;
        parent.transform.localScale = new Vector3(-1, 1, 1);

        // Load the DLL
        if (!dll.LoadDLL())
        {
            Debug.LogError("Failed to load ElasticityDLL!");
            enabled = false;
            return;
        }

        // Initialize the simulation
        simulationHandle = dll.Initialize(groundHeight);

        if (simulationHandle == 0)
        {
            Debug.LogError("Failed to initialize elasticity simulation!");
            enabled = false;
            return;
        }

        // Apply initial parameters
        ApplyAllParameters();

        // Create the mesh
        CreateMesh();

        // Update the material based on visualization mode
        UpdateMaterial();

        // Store initial parameter values for change detection
        StoreCurrentParameters();

        RefreshColliders();

        Debug.Log("Elasticity simulation initialized successfully with hot reload support.");

        isInitialized = true;
        OnInitialized?.Invoke();
    }

    void Update()
    {
        if (!dll.IsLoaded || simulationHandle == 0)
            return;

        // Check for parameter changes
        if (DetectParameterChanges())
        {
            ApplyChangedParameters();
            StoreCurrentParameters();
        }

        UpdateColliders();

        // Update the simulation
        if (dll.Update(simulationHandle) == 0)
        {
            Debug.LogError("Error during simulation update!");
            return;
        }

        // Update the mesh
        UpdateMesh();

        // Update stress visualization if enabled
        if (visualizeStress)
        {
            UpdateStressVisualization();
        }
    }

    void OnDestroy()
    {
        // Release the simulation when component is destroyed
        if (simulationHandle != 0 && dll != null && dll.IsLoaded)
        {
            dll.Release(simulationHandle);
            simulationHandle = 0;
            Debug.Log("Elasticity simulation released.");
        }

        dll?.Dispose();
        activeColliders.Clear();
    }

    private void OnDllReloaded()
    {
        if (showReloadNotifications)
        {
            Debug.Log("DLL reloaded! Reinitializing simulation...");
        }
        
        // Store current state
        bool wasInitialized = isInitialized;
        
        // Reinitialize simulation if it was previously initialized
        if (wasInitialized && simulationHandle != 0)
        {
            // Create new simulation handle
            int newHandle = dll.Initialize(groundHeight);
            if (newHandle != 0)
            {
                simulationHandle = newHandle;
                
                // Reapply all parameters
                ApplyAllParameters();
                
                // Recreate mesh
                CreateMesh();
                UpdateMaterial();
                
                // Refresh colliders
                RefreshColliders();
                
                if (showReloadNotifications)
                {
                    Debug.Log("Simulation reinitialized successfully after DLL reload.");
                }
            }
            else
            {
                Debug.LogError("Failed to reinitialize simulation after DLL reload!");
            }
        }
    }

    // Implementation of all original methods with dll. prefix
    private void ApplyAllParameters()
    {
        if (!dll.IsLoaded || simulationHandle == 0) return;

        // Configure resource paths if custom paths are enabled
        if (useCustomResourcePaths)
        {
            ConfigureResourcePaths();
        }

        dll.SetStressVisualization(simulationHandle, visualizeStress ? 1 : 0);
        dll.SetTimeStepParameters(simulationHandle, numSubsteps, numIterations);
        dll.SetMaterialProperties(simulationHandle, youngsModulus, poissonRatio, snhCompliance);
        dll.SetFrictionParameters(simulationHandle, frictionStatic, frictionDynamic, friction);
        dll.SetCollisionParameters(simulationHandle, collisionMargin, enableSelfCollision ? 1 : 0);
        dll.SetSolverParameters(simulationHandle, relaxationFactor, damping);

        dll.SetMeshName(simulationHandle, meshName);
        dll.SetBodyTransform(simulationHandle,
                           -bodyPosition.x, bodyPosition.y, bodyPosition.z,
                           bodyRotation.x, bodyRotation.y, bodyRotation.z,
                           bodyScale);

        if (!string.IsNullOrEmpty(attachedIndicesStr))
        {
            if (dll.SetAttachedIndices(simulationHandle, attachedIndicesStr) == 0)
            {
                Debug.LogError("Failed to set attached indices!");
            }
            else
            {
                Debug.Log("Successfully set attached indices: " + attachedIndicesStr);
            }
        }

        dll.CreateScene(simulationHandle, groundHeight);
    }

    private void ConfigureResourcePaths()
    {
        if (simulationHandle == 0)
        {
            Debug.LogWarning("Cannot configure resource paths: Simulation not initialized");
            return;
        }

        // Set root resource path
        string resourcePath = GetResourcePath();
        if (!string.IsNullOrEmpty(resourcePath))
        {
            int result = dll.SetResourcePath(simulationHandle, resourcePath);
            if (result == 1)
            {
                Debug.Log($"Resource path set to: {resourcePath}");
            }
            else
            {
                Debug.LogError($"Failed to set resource path: {resourcePath}");
            }
        }
    }

    private string GetResourcePath()
    {
        if (!string.IsNullOrEmpty(customResourcePath))
        {
            return ResolvePath(customResourcePath);
        }
        else
        {
            // Default to StreamingAssets/ElasticityData
            return Path.Combine(Application.streamingAssetsPath, "ElasticityData");
        }
    }

    private string ResolvePath(string path)
    {
        // Handle relative paths by combining with StreamingAssets
        if (Path.IsPathRooted(path))
        {
            return path;
        }
        else
        {
            return Path.Combine(Application.streamingAssetsPath, path);
        }
    }

    // All other methods remain the same, just replace static DLL calls with dll.MethodName
    private void StoreCurrentParameters()
    {
        lastNumSubsteps = numSubsteps;
        lastNumIterations = numIterations;
        lastYoungsModulus = youngsModulus;
        lastPoissonRatio = poissonRatio;
        lastSnhCompliance = snhCompliance;
        lastFrictionStatic = frictionStatic;
        lastFrictionDynamic = frictionDynamic;
        lastFriction = friction;
        lastCollisionMargin = collisionMargin;
        lastEnableSelfCollision = enableSelfCollision;
        lastRelaxationFactor = relaxationFactor;
        lastDamping = damping;
        lastMeshName = meshName;
        lastBodyPosition = bodyPosition;
        lastBodyRotation = bodyRotation;
        lastBodyScale = bodyScale;
    }

    private bool DetectParameterChanges()
    {
        return lastNumSubsteps != numSubsteps ||
               lastNumIterations != numIterations ||
               lastYoungsModulus != youngsModulus ||
               lastPoissonRatio != poissonRatio ||
               lastSnhCompliance != snhCompliance ||
               lastFrictionStatic != frictionStatic ||
               lastFrictionDynamic != frictionDynamic ||
               lastFriction != friction ||
               lastCollisionMargin != collisionMargin ||
               lastEnableSelfCollision != enableSelfCollision ||
               lastRelaxationFactor != relaxationFactor ||
               lastDamping != damping ||
               lastMeshName != meshName ||
               lastBodyPosition != bodyPosition ||
               lastBodyRotation != bodyRotation ||
               lastBodyScale != bodyScale;
    }

    private void ApplyChangedParameters()
    {
        if (!dll.IsLoaded || simulationHandle == 0) return;

        if (lastNumSubsteps != numSubsteps || lastNumIterations != numIterations)
        {
            dll.SetTimeStepParameters(simulationHandle, numSubsteps, numIterations);
        }

        if (lastYoungsModulus != youngsModulus ||
            lastPoissonRatio != poissonRatio ||
            lastSnhCompliance != snhCompliance)
        {
            dll.SetMaterialProperties(simulationHandle, youngsModulus, poissonRatio, snhCompliance);
        }

        if (lastFrictionStatic != frictionStatic ||
            lastFrictionDynamic != frictionDynamic ||
            lastFriction != friction)
        {
            dll.SetFrictionParameters(simulationHandle, frictionStatic, frictionDynamic, friction);
        }

        if (lastCollisionMargin != collisionMargin || lastEnableSelfCollision != enableSelfCollision)
        {
            dll.SetCollisionParameters(simulationHandle, collisionMargin, enableSelfCollision ? 1 : 0);
        }

        if (lastRelaxationFactor != relaxationFactor || lastDamping != damping)
        {
            dll.SetSolverParameters(simulationHandle, relaxationFactor, damping);
        }

        if (lastMeshName != meshName)
        {
            if (dll.SetMeshName(simulationHandle, meshName) == 0)
            {
                Debug.LogError("Failed to set mesh name!");
            }
        }

        if (lastBodyPosition != bodyPosition ||
            lastBodyRotation != bodyRotation ||
            lastBodyScale != bodyScale)
        {
            if (dll.SetBodyTransform(simulationHandle,
                                   -bodyPosition.x, bodyPosition.y, bodyPosition.z,
                                   bodyRotation.x, bodyRotation.y, bodyRotation.z,
                                   bodyScale) == 0)
            {
                Debug.LogError("Failed to set body transform!");
            }
        }
    }

    private void CreateMesh()
    {
        if (!dll.IsLoaded || simulationHandle == 0) return;

        int vertexCount = dll.GetVertexCount(simulationHandle);
        int triangleCount = dll.GetTriangleCount(simulationHandle);

        if (vertexCount == 0 || triangleCount == 0)
        {
            Debug.LogError("Received invalid mesh data from simulation!");
            return;
        }

        vertices = new Vector3[vertexCount];
        triangles = new int[triangleCount * 3];
        stressValues = new float[vertexCount];
        colors = new Color[vertexCount];

        float[] positionBuffer = new float[vertexCount * 3];

        if (dll.GetVertexPositions(simulationHandle, positionBuffer, positionBuffer.Length) == 0)
        {
            Debug.LogError("Failed to get vertex positions from simulation!");
            return;
        }

        for (int i = 0; i < vertexCount; i++)
        {
            vertices[i] = new Vector3(
                positionBuffer[i * 3],
                positionBuffer[i * 3 + 1],
                positionBuffer[i * 3 + 2]
            );
        }

        if (dll.GetTriangleIndices(simulationHandle, triangles, triangles.Length) == 0)
        {
            Debug.LogError("Failed to get triangle indices from simulation!");
            return;
        }

        mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = vertices;
        mesh.triangles = triangles;

        if (useProvidedUV)
        {
            int uvCount = dll.GetUVCount(simulationHandle);
            float[] uvBuffer = new float[uvCount * 2];
            if (dll.GetVertexUVs(simulationHandle, uvBuffer, uvBuffer.Length) == 1)
            {
                Vector2[] uvs = new Vector2[vertexCount];
                for (int i = 0; i < vertexCount; i++)
                {
                    uvs[i] = new Vector2(uvBuffer[i * 2], uvBuffer[i * 2 + 1]);
                }
                mesh.uv = uvs;
            }
            else
            {
                Debug.LogWarning("cannot load uv");
                GenerateDefaultUV();
            }

            Texture2D tex = Resources.Load<Texture2D>("Texture/" + meshName);
            if (tex != null)
            {
                defaultMaterial.mainTexture = tex;
                Debug.Log("Default material texture set to: " + meshName);
            }
            else
            {
                Debug.LogWarning("Texture with name " + meshName + " was not found in Resources!");
            }
        }
        else
        {
            GenerateDefaultUV();
        }

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        meshFilter.mesh = mesh;
    }

    private void GenerateDefaultUV()
    {
        Vector2[] uvs = new Vector2[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            uvs[i] = new Vector2(vertices[i].x, vertices[i].z);
        }
        mesh.uv = uvs;
    }

    private void UpdateMesh()
    {
        if (mesh == null || vertices == null || !dll.IsLoaded)
            return;

        int vertexCount = vertices.Length;
        float[] positionBuffer = new float[vertexCount * 3];

        if (dll.GetVertexPositions(simulationHandle, positionBuffer, positionBuffer.Length) == 0)
        {
            Debug.LogWarning("Failed to get updated vertex positions!");
            return;
        }

        for (int i = 0; i < vertexCount; i++)
        {
            vertices[i] = new Vector3(
                positionBuffer[i * 3],
                positionBuffer[i * 3 + 1],
                positionBuffer[i * 3 + 2]
            );
        }

        mesh.vertices = vertices;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    private void UpdateStressVisualization()
    {
        if (mesh == null || stressValues == null || colors == null || !dll.IsLoaded)
            return;

        int vertexCount = vertices.Length;

        if (dll.GetVertexStressValues(simulationHandle, stressValues, stressValues.Length) == 0)
        {
            Debug.LogWarning("Failed to get stress values!");
            return;
        }

        float minStress = float.MaxValue;
        for (int i = 0; i < vertexCount; i++)
            minStress = Mathf.Min(minStress, stressValues[i]);

        float range = maxStress - minStress;

        for (int i = 0; i < vertexCount; i++)
        {
            float normalizedStress = range > 0 ?
                (stressValues[i] - minStress) / range : 0;
            colors[i] = Color.Lerp(Color.blue, Color.red, normalizedStress);
        }

        mesh.colors = colors;
    }

    private void UpdateMaterial()
    {
        if (meshRenderer == null)
            return;

        if (visualizeStress)
        {
            if (stressMaterial != null)
                meshRenderer.material = stressMaterial;
        }
        else
        {
            if (defaultMaterial != null)
                meshRenderer.material = defaultMaterial;
        }
    }

    // Collider management methods (simplified for brevity)
    public void RefreshColliders()
    {
        // Similar to original implementation
        for (int i = activeColliders.Count - 1; i >= 0; i--)
        {
            if (activeColliders[i] == null)
            {
                activeColliders.RemoveAt(i);
            }
        }

        ElasticityCollider[] sceneColliders = FindObjectsByType<ElasticityCollider>(FindObjectsSortMode.None);

        foreach (var collider in sceneColliders)
        {
            if (!activeColliders.Contains(collider))
            {
                RegisterCollider(collider);
                activeColliders.Add(collider);
            }
        }
    }

    private void RegisterCollider(ElasticityCollider collider)
    {
        if (simulationHandle == 0 || collider == null || !dll.IsLoaded)
            return;

        Vector3 position = collider.GetPosition();
        Vector3 scale = collider.GetScale();
        Quaternion rotation = collider.GetRotation();

        int colliderID = dll.AddCollider(
            simulationHandle,
            (int)collider.type,
            -position.x, position.y, position.z,
            scale.x, scale.y, scale.z,
            rotation.x, rotation.y, rotation.z, rotation.w
        );

        if (colliderID >= 0)
        {
            collider.colliderID = colliderID;
        }
        else
        {
            Debug.LogError($"Failed to register collider {collider.name}");
        }
    }

    private void UpdateColliders()
    {
        if (simulationHandle == 0 || !dll.IsLoaded)
            return;

        for (int i = activeColliders.Count - 1; i >= 0; i--)
        {
            var collider = activeColliders[i];

            if (collider == null)
            {
                if (i < activeColliders.Count)
                {
                    int colliderID = activeColliders[i].colliderID;
                    if (colliderID >= 0)
                    {
                        dll.RemoveCollider(simulationHandle, colliderID);
                    }
                    activeColliders.RemoveAt(i);
                }
                continue;
            }

            if (collider.colliderID >= 0)
            {
                Vector3 position = collider.GetPosition();
                Vector3 scale = collider.GetScale();
                Quaternion rotation = collider.GetRotation();

                int colliderUpdate = dll.UpdateCollider(
                    simulationHandle,
                    collider.colliderID,
                    -position.x, position.y, position.z,
                    scale.x, scale.y, scale.z,
                    rotation.x, rotation.y, rotation.z, rotation.w
                );

                if (colliderUpdate < 0)
                {
                    Debug.LogError($"Failed to update collider {collider.name} with ID: {collider.colliderID}");
                }
            }
        }
    }

    // Public methods for manual control
    [ContextMenu("Reload DLL")]
    public void ReloadDLL()
    {
        if (dll != null)
        {
            dll.UnloadDLL();
            dll.LoadDLL();
        }
    }

    public void SetStressVisualization(bool enabled)
    {
        visualizeStress = enabled;

        if (simulationHandle != 0 && dll != null && dll.IsLoaded)
        {
            dll.SetStressVisualization(simulationHandle, enabled ? 1 : 0);
        }

        UpdateMaterial();
    }
}