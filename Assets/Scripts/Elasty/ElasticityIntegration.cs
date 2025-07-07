using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.IO;

/// <summary>
/// Integrates the native C++ elasticity simulation with Unity and GPU performance profiling
/// </summary>
public class ElasticityIntegration : MonoBehaviour
{
    [Header("=== Simulation Settings ===")]
    [Tooltip("Height of the ground plane for the simulation")]
    public float groundHeight = 0.0f;

    [Tooltip("Whether to visualize stress on the model")]
    public bool visualizeStress = true;

    [Tooltip("Whether to load uv on the model")]
    public bool useProvidedUV = true;

    public float maxStress = 20000.0f; // Maximum stress value for visualization

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

    // ==================== GPU PROFILER SETTINGS ====================
    // Use ENABLE_GPU_PROFILING to enable/disable GPU profiling features
#if ENABLE_GPU_PROFILING
    [Header("=== GPU Performance Profiler ===")]
    [SerializeField]
    [Tooltip("Enable GPU performance profiling")]
    private bool enableGPUProfiling = true;

    [SerializeField]
    [Tooltip("Frame interval for automatic result export")]
    private int autoExportFrameInterval = 1000;

    [SerializeField]
    [Tooltip("Output filename (without extension)")]
    private string profilingOutputFileName = "gpu_profiling_results";

    [SerializeField]
    [Tooltip("Whether to add timestamp to filename")]
    private bool addTimestampToFilename = true;

    [SerializeField]
    [Tooltip("Save to StreamingAssets folder (for easy access)")]
    private bool saveToStreamingAssets = true;

    [SerializeField]
    [Tooltip("Show verbose logs in console")]
    private bool verboseProfilingLogs = true;

    [SerializeField]
    [Tooltip("Auto-stop after specified frames (0=don't stop)")]
    private int autoStopAfterFrames = 0;

    [SerializeField]
    [Tooltip("Show on-screen status information")]
    private bool showOnScreenStatus = true;
#endif

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

    // ==================== GPU PROFILER VARIABLES ====================
#if ENABLE_GPU_PROFILING
    private bool isProfilingActive = false;
    private string fullProfilingOutputPath;
    private int lastExportedFrame = 0;
    private bool hasAutoStopped = false;
    private GUIStyle guiStyle;
#endif

    // DLL imports
    #region DLLImports
    [DllImport("ElasticityDLL")]
    private static extern int Elasticity_Initialize(float groundHeight);

    [DllImport("ElasticityDLL")]
    private static extern int Elasticity_Update(int handle);

    [DllImport("ElasticityDLL")]
    private static extern int Elasticity_GetVertexCount(int handle);

    [DllImport("ElasticityDLL")]
    private static extern int Elasticity_GetTriangleCount(int handle);

    [DllImport("ElasticityDLL")]
    private static extern int Elasticity_GetUVCount(int handle);

    [DllImport("ElasticityDLL")]
    private static extern int Elasticity_GetVertexPositions(int handle, [Out] float[] buffer, int bufferSize);

    [DllImport("ElasticityDLL")]
    private static extern int Elasticity_GetTriangleIndices(int handle, [Out] int[] buffer, int bufferSize);

    [DllImport("ElasticityDLL")]
    private static extern int Elasticity_GetVertexUVs(int handle, [Out] float[] buffer, int bufferSize);

    [DllImport("ElasticityDLL")]
    private static extern int Elasticity_GetVertexStressValues(int handle, [Out] float[] buffer, int bufferSize);

    [DllImport("ElasticityDLL")]
    private static extern int Elasticity_Release(int handle);

    // Parameter control
    [DllImport("ElasticityDLL")]
    private static extern int Elasticity_SetTimeStepParameters(int handle, int numSubsteps, int numIterations);

    [DllImport("ElasticityDLL")]
    private static extern int Elasticity_SetMaterialProperties(int handle, float youngsModulus,
                                                          float poissonRatio, float snhCompliance);

    [DllImport("ElasticityDLL")]
    private static extern int Elasticity_SetFrictionParameters(int handle, float frictionStatic,
                                                          float frictionDynamic, float friction);

    [DllImport("ElasticityDLL")]
    private static extern int Elasticity_SetCollisionParameters(int handle, float collisionMargin,
                                                           int enableSelfCollision);

    [DllImport("ElasticityDLL")]
    private static extern int Elasticity_SetSolverParameters(int handle, float relaxationFactor,
                                                        float damping);

    [DllImport("ElasticityDLL")]
    private static extern int Elasticity_SetStressVisualization(int handle, int enable);

    [DllImport("ElasticityDLL")]
    private static extern int Elasticity_AddCollider(int handle, int type,
                                                   float posX, float posY, float posZ,
                                                   float scaleX, float scaleY, float scaleZ,
                                                   float rotX, float rotY, float rotZ, float rotW);

    [DllImport("ElasticityDLL")]
    private static extern int Elasticity_UpdateCollider(int handle, int colliderID,
                                                      float posX, float posY, float posZ,
                                                      float scaleX, float scaleY, float scaleZ,
                                                      float rotX, float rotY, float rotZ, float rotW);

    [DllImport("ElasticityDLL")]
    private static extern int Elasticity_RemoveCollider(int handle, int colliderID);

    [DllImport("ElasticityDLL")]
    private static extern int Elasticity_CreateScene(int handle, float groundHeight);

    [DllImport("ElasticityDLL")]
    private static extern int Elasticity_SetMeshName(int handle, string meshName);

    [DllImport("ElasticityDLL")]
    private static extern int Elasticity_SetBodyTransform(int handle,
                                                         float posX, float posY, float posZ,
                                                         float rotX, float rotY, float rotZ,
                                                         float scale);

    [DllImport("ElasticityDLL")]
    private static extern int Elasticity_SetAttachedIndices(int handle, string indicesStr);

    // ==================== RESOURCE PATH SETTING DLL IMPORTS ====================
    [DllImport("ElasticityDLL")]
    private static extern int Elasticity_SetResourcePath(int handle, string resourcePath);

    // ==================== GPU PROFILER DLL IMPORTS ====================
#if ENABLE_GPU_PROFILING
    [DllImport("ElasticityDLL")]
    private static extern int Elasticity_EnableProfiling(int handle, int enable);

    [DllImport("ElasticityDLL")]
    private static extern int Elasticity_SetProfilerOutputPath(int handle, string outputPath);

    [DllImport("ElasticityDLL")]
    private static extern int Elasticity_ExportProfilingResults(int handle);

    [DllImport("ElasticityDLL")]
    private static extern int Elasticity_ResetProfiling(int handle);

    [DllImport("ElasticityDLL")]
    private static extern int Elasticity_GetProfilingFrameCount(int handle);
#endif

    #endregion

    void Awake()
    {
        // Create mesh components if they don't exist
        if (meshFilter == null)
            meshFilter = gameObject.AddComponent<MeshFilter>();

        if (meshRenderer == null)
            meshRenderer = gameObject.AddComponent<MeshRenderer>();

        meshRenderer.material = defaultMaterial;

        // Initialize GUI style for on-screen display
#if ENABLE_GPU_PROFILING
        InitializeGUIStyle();
#endif
    }

    void Start()
    {
        // Create a parent GameObject to mirror the mesh
        GameObject parent = new GameObject("MirrorParent");
        transform.parent = parent.transform;
        parent.transform.localScale = new Vector3(-1, 1, 1);

        // Initialize profiler output path
#if ENABLE_GPU_PROFILING
        InitializeProfilingOutputPath();
#endif

        // Initialize the simulation
        simulationHandle = Elasticity_Initialize(groundHeight);

        if (simulationHandle == 0)
        {
            Debug.LogError("Failed to initialize elasticity simulation!");
            enabled = false;
            return;
        }

        // Apply initial parameters
        ApplyAllParameters();

        // Initialize GPU profiling if enabled
#if ENABLE_GPU_PROFILING
        if (enableGPUProfiling)
        {
            StartGPUProfiling();
        }
#endif

        // Create the mesh
        CreateMesh();

        // Update the material based on visualization mode
        UpdateMaterial();

        // Store initial parameter values for change detection
        StoreCurrentParameters();

        RefreshColliders();

        Debug.Log("Elasticity simulation initialized successfully.");

        isInitialized = true;
        OnInitialized?.Invoke();

        // Start monitoring coroutine
#if ENABLE_GPU_PROFILING
        if (enableGPUProfiling)
        {
            StartCoroutine(MonitorProfiling());
        }
#endif
    }

    void Update()
    {
        // Check for parameter changes
        if (DetectParameterChanges())
        {
            ApplyChangedParameters();
            StoreCurrentParameters();
        }

        UpdateColliders();

        // Update the simulation
        if (Elasticity_Update(simulationHandle) == 0)
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

        // Handle keyboard shortcuts for profiling
#if ENABLE_GPU_PROFILING
        HandleProfilingKeyboardShortcuts();
#endif
    }

    void OnDestroy()
    {
        // Export final results if profiling is active
#if ENABLE_GPU_PROFILING
        if (isProfilingActive)
        {
            ExportProfilingResults();
        }
#endif

        // Release the simulation when component is destroyed
        if (simulationHandle != 0)
        {
            Elasticity_Release(simulationHandle);
            simulationHandle = 0;
#if ENABLE_GPU_PROFILING
            LogProfilingMessage("Elasticity simulation released.");
#else
            Debug.Log("Elasticity simulation released.");
#endif
        }

        activeColliders.Clear();
    }

#if ENABLE_GPU_PROFILING
    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus && isProfilingActive)
        {
            LogProfilingMessage("Application paused, exporting current results...");
            ExportProfilingResults();
        }
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus && isProfilingActive)
        {
            LogProfilingMessage("Application lost focus, exporting current results...");
            ExportProfilingResults();
        }
    }

    void OnGUI()
    {
        if (!showOnScreenStatus || !isProfilingActive) return;

        var status = GetProfilingStatus();

        string displayText = $"GPU Profiler Status:\n" +
                           $"Active: {status.IsActive}\n" +
                           $"Frames: {status.CurrentFrames:N0}\n" +
                           $"Progress: {CalculateProgress():F1}%\n" +
                           $"Output: {Path.GetFileName(status.OutputPath)}";

        GUI.Label(new Rect(10, 10, 300, 120), displayText, guiStyle);

        // Show controls
        string controlsText = "Controls:\n" +
                            "Space - Export Results\n" +
                            "R - Reset Data\n" +
                            "P - Toggle Profiling";

        GUI.Label(new Rect(10, 140, 300, 80), controlsText, guiStyle);
    }
#endif

    // ==================== GPU PROFILER METHODS ====================
#if ENABLE_GPU_PROFILING
    private void InitializeGUIStyle()
    {
        guiStyle = new GUIStyle();
        guiStyle.fontSize = 14;
        guiStyle.normal.textColor = Color.white;
        guiStyle.normal.background = CreateColorTexture(new Color(0, 0, 0, 0.7f));
        guiStyle.padding = new RectOffset(10, 10, 5, 5);
    }

    private Texture2D CreateColorTexture(Color color)
    {
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }

    private void InitializeProfilingOutputPath()
    {
        string fileName = profilingOutputFileName;

        if (addTimestampToFilename)
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            fileName = $"{profilingOutputFileName}_{timestamp}";
        }

        fileName += ".json";

        string directory;
        if (saveToStreamingAssets)
        {
            directory = Path.Combine(Application.streamingAssetsPath, "ProfilingResults");
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }
        else
        {
            directory = Application.persistentDataPath;
        }

        fullProfilingOutputPath = Path.Combine(directory, fileName);
        LogProfilingMessage($"GPU profiler output path: {fullProfilingOutputPath}");
    }

    private void StartGPUProfiling()
    {
        if (simulationHandle == 0)
        {
            Debug.LogError("Cannot start profiling: Simulation not initialized");
            return;
        }

        // Set output path
        int pathResult = Elasticity_SetProfilerOutputPath(simulationHandle, fullProfilingOutputPath);
        if (pathResult == 0)
        {
            Debug.LogError("Failed to set profiler output path");
            return;
        }

        // Enable profiling
        int enableResult = Elasticity_EnableProfiling(simulationHandle, 1);
        if (enableResult == 1)
        {
            isProfilingActive = true;
            LogProfilingMessage("GPU profiling started successfully");
            LogProfilingMessage($"Will auto-export every {autoExportFrameInterval} frames");

            if (autoStopAfterFrames > 0)
            {
                LogProfilingMessage($"Will auto-stop after {autoStopAfterFrames} frames");
            }
        }
        else
        {
            Debug.LogError("Failed to enable GPU profiling");
        }
    }

    private IEnumerator MonitorProfiling()
    {
        while (true)
        {
            yield return new WaitForSeconds(1.0f);

            if (!isProfilingActive || simulationHandle == 0) continue;

            int currentFrames = Elasticity_GetProfilingFrameCount(simulationHandle);

            // Check auto export
            if (currentFrames > 0 && currentFrames % autoExportFrameInterval == 0 && currentFrames != lastExportedFrame)
            {
                LogProfilingMessage($"Auto-exporting results after {currentFrames} frames");
                ExportProfilingResults();
                lastExportedFrame = currentFrames;
            }

            // Check auto stop
            if (autoStopAfterFrames > 0 && currentFrames >= autoStopAfterFrames && !hasAutoStopped)
            {
                LogProfilingMessage($"Reached target frames ({autoStopAfterFrames}), stopping simulation...");
                hasAutoStopped = true;
                ExportProfilingResults();
                StopSimulation();
                break;
            }

            // Show progress
            if (verboseProfilingLogs && currentFrames > 0 && currentFrames % 100 == 0)
            {
                float progress = CalculateProgress();
                LogProfilingMessage($"Progress: {currentFrames} frames completed ({progress:F1}% to next export)");
            }
        }
    }

    private void HandleProfilingKeyboardShortcuts()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            ExportProfilingResults();
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetProfilingData();
        }

        if (Input.GetKeyDown(KeyCode.P))
        {
            ToggleProfiling();
        }
    }

    private float CalculateProgress()
    {
        if (autoExportFrameInterval <= 0) return 0f;

        int currentFrames = GetCurrentProfilingFrameCount();
        return (float)(currentFrames % autoExportFrameInterval) / autoExportFrameInterval * 100f;
    }

    private void StopSimulation()
    {
        if (simulationHandle != 0)
        {
            Elasticity_Release(simulationHandle);
            simulationHandle = 0;
            LogProfilingMessage("Simulation stopped");
        }

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private void LogProfilingMessage(string message)
    {
        if (verboseProfilingLogs)
        {
            Debug.Log($"[GPU Profiler] {message}");
        }
    }

#endif

    // ==================== PUBLIC PROFILER METHODS ====================
#if ENABLE_GPU_PROFILING
    [ContextMenu("Export Profiling Results")]
    public void ExportProfilingResults()
    {
        if (simulationHandle == 0) return;

        int result = Elasticity_ExportProfilingResults(simulationHandle);
        if (result == 1)
        {
            LogProfilingMessage($"Results exported successfully to: {fullProfilingOutputPath}");

            if (File.Exists(fullProfilingOutputPath))
            {
                FileInfo fileInfo = new FileInfo(fullProfilingOutputPath);
                LogProfilingMessage($"File size: {fileInfo.Length / 1024.0f:F2} KB");
            }
        }
        else
        {
            Debug.LogError("Failed to export profiling results");
        }
    }

    [ContextMenu("Reset Profiling Data")]
    public void ResetProfilingData()
    {
        if (simulationHandle == 0) return;

        int result = Elasticity_ResetProfiling(simulationHandle);
        if (result == 1)
        {
            LogProfilingMessage("Profiling data reset");
            lastExportedFrame = 0;
        }
        else
        {
            Debug.LogError("Failed to reset profiling data");
        }
    }

    [ContextMenu("Toggle Profiling")]
    public void ToggleProfiling()
    {
        if (isProfilingActive)
        {
            DisableProfiling();
        }
        else
        {
            EnableProfiling();
        }
    }

    public void EnableProfiling()
    {
        if (simulationHandle == 0) return;

        int result = Elasticity_EnableProfiling(simulationHandle, 1);
        if (result == 1)
        {
            isProfilingActive = true;
            LogProfilingMessage("GPU profiling enabled");
        }
    }

    public void DisableProfiling()
    {
        if (simulationHandle == 0) return;

        int result = Elasticity_EnableProfiling(simulationHandle, 0);
        if (result == 1)
        {
            isProfilingActive = false;
            LogProfilingMessage("GPU profiling disabled");
        }
    }

    public int GetCurrentProfilingFrameCount()
    {
        if (simulationHandle == 0) return 0;
        return Elasticity_GetProfilingFrameCount(simulationHandle);
    }

    public ProfilingStatus GetProfilingStatus()
    {
        return new ProfilingStatus
        {
            IsActive = isProfilingActive,
            CurrentFrames = GetCurrentProfilingFrameCount(),
            OutputPath = fullProfilingOutputPath,
            SimulationHandle = simulationHandle
        };
    }
#endif

    // ==================== RESOURCE PATH CONFIGURATION ====================

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
            int result = Elasticity_SetResourcePath(simulationHandle, resourcePath);
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

    // ==================== ORIGINAL ELASTICITY METHODS (UNCHANGED) ====================

    public void SetStressVisualization(bool enabled)
    {
        visualizeStress = enabled;

        if (simulationHandle != 0)
        {
            Elasticity_SetStressVisualization(simulationHandle, enabled ? 1 : 0);
        }

        UpdateMaterial();
    }

    private void ApplyAllParameters()
    {
        // Configure resource paths if custom paths are enabled
        if (useCustomResourcePaths)
        {
            ConfigureResourcePaths();
        }

        Elasticity_SetStressVisualization(simulationHandle, visualizeStress ? 1 : 0);
        Elasticity_SetTimeStepParameters(simulationHandle, numSubsteps, numIterations);
        Elasticity_SetMaterialProperties(simulationHandle, youngsModulus, poissonRatio, snhCompliance);
        Elasticity_SetFrictionParameters(simulationHandle, frictionStatic, frictionDynamic, friction);
        Elasticity_SetCollisionParameters(simulationHandle, collisionMargin, enableSelfCollision ? 1 : 0);
        Elasticity_SetSolverParameters(simulationHandle, relaxationFactor, damping);

        Elasticity_SetMeshName(simulationHandle, meshName);
        Elasticity_SetBodyTransform(simulationHandle,
                                   -bodyPosition.x, bodyPosition.y, bodyPosition.z,
                                   bodyRotation.x, bodyRotation.y, bodyRotation.z,
                                   bodyScale);

        if (!string.IsNullOrEmpty(attachedIndicesStr))
        {
            if (Elasticity_SetAttachedIndices(simulationHandle, attachedIndicesStr) == 0)
            {
                Debug.LogError("Failed to set attached indices!");
            }
            else
            {
                Debug.Log("Successfully set attached indices: " + attachedIndicesStr);
            }
        }

        Elasticity_CreateScene(simulationHandle, groundHeight);
    }

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
        if (lastNumSubsteps != numSubsteps || lastNumIterations != numIterations)
        {
            Elasticity_SetTimeStepParameters(simulationHandle, numSubsteps, numIterations);
        }

        if (lastYoungsModulus != youngsModulus ||
            lastPoissonRatio != poissonRatio ||
            lastSnhCompliance != snhCompliance)
        {
            Elasticity_SetMaterialProperties(simulationHandle, youngsModulus, poissonRatio, snhCompliance);
        }

        if (lastFrictionStatic != frictionStatic ||
            lastFrictionDynamic != frictionDynamic ||
            lastFriction != friction)
        {
            Elasticity_SetFrictionParameters(simulationHandle, frictionStatic, frictionDynamic, friction);
        }

        if (lastCollisionMargin != collisionMargin || lastEnableSelfCollision != enableSelfCollision)
        {
            Elasticity_SetCollisionParameters(simulationHandle, collisionMargin, enableSelfCollision ? 1 : 0);
        }

        if (lastRelaxationFactor != relaxationFactor || lastDamping != damping)
        {
            Elasticity_SetSolverParameters(simulationHandle, relaxationFactor, damping);
        }

        if (lastMeshName != meshName)
        {
            if (Elasticity_SetMeshName(simulationHandle, meshName) == 0)
            {
                Debug.LogError("Failed to set mesh name!");
            }
        }

        if (lastBodyPosition != bodyPosition ||
            lastBodyRotation != bodyRotation ||
            lastBodyScale != bodyScale)
        {
            if (Elasticity_SetBodyTransform(simulationHandle,
                                           -bodyPosition.x, bodyPosition.y, bodyPosition.z,
                                           bodyRotation.x, bodyRotation.y, bodyRotation.z,
                                           bodyScale) == 0)
            {
                Debug.LogError("Failed to set body transform!");
            }
        }
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

    private void CreateMesh()
    {
        int vertexCount = Elasticity_GetVertexCount(simulationHandle);
        int triangleCount = Elasticity_GetTriangleCount(simulationHandle);

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

        if (Elasticity_GetVertexPositions(simulationHandle, positionBuffer, positionBuffer.Length) == 0)
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

        if (Elasticity_GetTriangleIndices(simulationHandle, triangles, triangles.Length) == 0)
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
            int uvCount = Elasticity_GetUVCount(simulationHandle);
            float[] uvBuffer = new float[uvCount * 2];
            if (Elasticity_GetVertexUVs(simulationHandle, uvBuffer, uvBuffer.Length) == 1)
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

    private void UpdateMesh()
    {
        if (mesh == null || vertices == null)
            return;

        int vertexCount = vertices.Length;
        float[] positionBuffer = new float[vertexCount * 3];

        if (Elasticity_GetVertexPositions(simulationHandle, positionBuffer, positionBuffer.Length) == 0)
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
        if (mesh == null || stressValues == null || colors == null)
            return;

        int vertexCount = vertices.Length;

        if (Elasticity_GetVertexStressValues(simulationHandle, stressValues, stressValues.Length) == 0)
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

    public void RefreshColliders()
    {
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
        if (simulationHandle == 0 || collider == null)
            return;

        Vector3 position = collider.GetPosition();
        Vector3 scale = collider.GetScale();
        Quaternion rotation = collider.GetRotation();

        int colliderID = Elasticity_AddCollider(
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
        if (simulationHandle == 0)
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
                        Elasticity_RemoveCollider(simulationHandle, colliderID);
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

                int colliderUpdate = Elasticity_UpdateCollider(
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

    public void AddCollider(ElasticityCollider collider)
    {
        if (!activeColliders.Contains(collider))
        {
            RegisterCollider(collider);
            activeColliders.Add(collider);
        }
    }

    public void RemoveCollider(ElasticityCollider collider)
    {
        if (simulationHandle == 0 || collider == null)
            return;

        if (collider.colliderID >= 0)
        {
            Elasticity_RemoveCollider(simulationHandle, collider.colliderID);
            activeColliders.Remove(collider);
        }
    }
}

#if ENABLE_GPU_PROFILING
/// <summary>
/// Performance profiling status information
/// </summary>
[System.Serializable]
public struct ProfilingStatus
{
    public bool IsActive;
    public int CurrentFrames;
    public string OutputPath;
    public int SimulationHandle;
}
#endif