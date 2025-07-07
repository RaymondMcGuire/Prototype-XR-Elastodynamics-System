using System;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Dynamic DLL function delegates for hot-reloading
/// </summary>
public class ElasticityDynamicDLL
{
    private DynamicDLLLoader dllLoader;
    
    // Function delegates
    public delegate int InitializeDelegate(float groundHeight);
    public delegate int UpdateDelegate(int handle);
    public delegate int GetVertexCountDelegate(int handle);
    public delegate int GetTriangleCountDelegate(int handle);
    public delegate int GetUVCountDelegate(int handle);
    public delegate int GetVertexPositionsDelegate(int handle, [Out] float[] buffer, int bufferSize);
    public delegate int GetTriangleIndicesDelegate(int handle, [Out] int[] buffer, int bufferSize);
    public delegate int GetVertexUVsDelegate(int handle, [Out] float[] buffer, int bufferSize);
    public delegate int GetVertexStressValuesDelegate(int handle, [Out] float[] buffer, int bufferSize);
    public delegate int ReleaseDelegate(int handle);
    public delegate int SetTimeStepParametersDelegate(int handle, int numSubsteps, int numIterations);
    public delegate int SetMaterialPropertiesDelegate(int handle, float youngsModulus, float poissonRatio, float snhCompliance);
    public delegate int SetFrictionParametersDelegate(int handle, float frictionStatic, float frictionDynamic, float friction);
    public delegate int SetCollisionParametersDelegate(int handle, float collisionMargin, int enableSelfCollision);
    public delegate int SetSolverParametersDelegate(int handle, float relaxationFactor, float damping);
    public delegate int SetStressVisualizationDelegate(int handle, int enable);
    public delegate int AddColliderDelegate(int handle, int type, float posX, float posY, float posZ, float scaleX, float scaleY, float scaleZ, float rotX, float rotY, float rotZ, float rotW);
    public delegate int UpdateColliderDelegate(int handle, int colliderID, float posX, float posY, float posZ, float scaleX, float scaleY, float scaleZ, float rotX, float rotY, float rotZ, float rotW);
    public delegate int RemoveColliderDelegate(int handle, int colliderID);
    public delegate int CreateSceneDelegate(int handle, float groundHeight);
    public delegate int SetMeshNameDelegate(int handle, string meshName);
    public delegate int SetBodyTransformDelegate(int handle, float posX, float posY, float posZ, float rotX, float rotY, float rotZ, float scale);
    public delegate int SetAttachedIndicesDelegate(int handle, string indicesStr);
    public delegate int SetResourcePathDelegate(int handle, string resourcePath);
    
#if ENABLE_GPU_PROFILING
    public delegate int EnableProfilingDelegate(int handle, int enable);
    public delegate int SetProfilerOutputPathDelegate(int handle, string outputPath);
    public delegate int ExportProfilingResultsDelegate(int handle);
    public delegate int ResetProfilingDelegate(int handle);
    public delegate int GetProfilingFrameCountDelegate(int handle);
#endif

    // Function instances
    public InitializeDelegate Initialize;
    public UpdateDelegate Update;
    public GetVertexCountDelegate GetVertexCount;
    public GetTriangleCountDelegate GetTriangleCount;
    public GetUVCountDelegate GetUVCount;
    public GetVertexPositionsDelegate GetVertexPositions;
    public GetTriangleIndicesDelegate GetTriangleIndices;
    public GetVertexUVsDelegate GetVertexUVs;
    public GetVertexStressValuesDelegate GetVertexStressValues;
    public ReleaseDelegate Release;
    public SetTimeStepParametersDelegate SetTimeStepParameters;
    public SetMaterialPropertiesDelegate SetMaterialProperties;
    public SetFrictionParametersDelegate SetFrictionParameters;
    public SetCollisionParametersDelegate SetCollisionParameters;
    public SetSolverParametersDelegate SetSolverParameters;
    public SetStressVisualizationDelegate SetStressVisualization;
    public AddColliderDelegate AddCollider;
    public UpdateColliderDelegate UpdateCollider;
    public RemoveColliderDelegate RemoveCollider;
    public CreateSceneDelegate CreateScene;
    public SetMeshNameDelegate SetMeshName;
    public SetBodyTransformDelegate SetBodyTransform;
    public SetAttachedIndicesDelegate SetAttachedIndices;
    public SetResourcePathDelegate SetResourcePath;
    
#if ENABLE_GPU_PROFILING
    public EnableProfilingDelegate EnableProfiling;
    public SetProfilerOutputPathDelegate SetProfilerOutputPath;
    public ExportProfilingResultsDelegate ExportProfilingResults;
    public ResetProfilingDelegate ResetProfiling;
    public GetProfilingFrameCountDelegate GetProfilingFrameCount;
#endif

    public bool IsLoaded { get; private set; }
    public event Action OnDllReloaded;

    public ElasticityDynamicDLL()
    {
        dllLoader = new DynamicDLLLoader("ElasticityDLL");
        dllLoader.OnDllReloaded += OnDllReloadedInternal;
    }

    public bool LoadDLL()
    {
        if (!dllLoader.LoadDLL())
        {
            Debug.LogError("Failed to load ElasticityDLL");
            return false;
        }

        return LoadFunctions();
    }

    private bool LoadFunctions()
    {
        try
        {
            Initialize = dllLoader.GetFunction<InitializeDelegate>("Elasticity_Initialize");
            Update = dllLoader.GetFunction<UpdateDelegate>("Elasticity_Update");
            GetVertexCount = dllLoader.GetFunction<GetVertexCountDelegate>("Elasticity_GetVertexCount");
            GetTriangleCount = dllLoader.GetFunction<GetTriangleCountDelegate>("Elasticity_GetTriangleCount");
            GetUVCount = dllLoader.GetFunction<GetUVCountDelegate>("Elasticity_GetUVCount");
            GetVertexPositions = dllLoader.GetFunction<GetVertexPositionsDelegate>("Elasticity_GetVertexPositions");
            GetTriangleIndices = dllLoader.GetFunction<GetTriangleIndicesDelegate>("Elasticity_GetTriangleIndices");
            GetVertexUVs = dllLoader.GetFunction<GetVertexUVsDelegate>("Elasticity_GetVertexUVs");
            GetVertexStressValues = dllLoader.GetFunction<GetVertexStressValuesDelegate>("Elasticity_GetVertexStressValues");
            Release = dllLoader.GetFunction<ReleaseDelegate>("Elasticity_Release");
            SetTimeStepParameters = dllLoader.GetFunction<SetTimeStepParametersDelegate>("Elasticity_SetTimeStepParameters");
            SetMaterialProperties = dllLoader.GetFunction<SetMaterialPropertiesDelegate>("Elasticity_SetMaterialProperties");
            SetFrictionParameters = dllLoader.GetFunction<SetFrictionParametersDelegate>("Elasticity_SetFrictionParameters");
            SetCollisionParameters = dllLoader.GetFunction<SetCollisionParametersDelegate>("Elasticity_SetCollisionParameters");
            SetSolverParameters = dllLoader.GetFunction<SetSolverParametersDelegate>("Elasticity_SetSolverParameters");
            SetStressVisualization = dllLoader.GetFunction<SetStressVisualizationDelegate>("Elasticity_SetStressVisualization");
            AddCollider = dllLoader.GetFunction<AddColliderDelegate>("Elasticity_AddCollider");
            UpdateCollider = dllLoader.GetFunction<UpdateColliderDelegate>("Elasticity_UpdateCollider");
            RemoveCollider = dllLoader.GetFunction<RemoveColliderDelegate>("Elasticity_RemoveCollider");
            CreateScene = dllLoader.GetFunction<CreateSceneDelegate>("Elasticity_CreateScene");
            SetMeshName = dllLoader.GetFunction<SetMeshNameDelegate>("Elasticity_SetMeshName");
            SetBodyTransform = dllLoader.GetFunction<SetBodyTransformDelegate>("Elasticity_SetBodyTransform");
            SetAttachedIndices = dllLoader.GetFunction<SetAttachedIndicesDelegate>("Elasticity_SetAttachedIndices");
            SetResourcePath = dllLoader.GetFunction<SetResourcePathDelegate>("Elasticity_SetResourcePath");

#if ENABLE_GPU_PROFILING
            EnableProfiling = dllLoader.GetFunction<EnableProfilingDelegate>("Elasticity_EnableProfiling");
            SetProfilerOutputPath = dllLoader.GetFunction<SetProfilerOutputPathDelegate>("Elasticity_SetProfilerOutputPath");
            ExportProfilingResults = dllLoader.GetFunction<ExportProfilingResultsDelegate>("Elasticity_ExportProfilingResults");
            ResetProfiling = dllLoader.GetFunction<ResetProfilingDelegate>("Elasticity_ResetProfiling");
            GetProfilingFrameCount = dllLoader.GetFunction<GetProfilingFrameCountDelegate>("Elasticity_GetProfilingFrameCount");
#endif

            IsLoaded = true;
            Debug.Log("All DLL functions loaded successfully");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to load DLL functions: {e.Message}");
            IsLoaded = false;
            return false;
        }
    }

    private void OnDllReloadedInternal()
    {
        Debug.Log("DLL reloaded, reloading functions...");
        if (LoadFunctions())
        {
            OnDllReloaded?.Invoke();
        }
    }

    public void UnloadDLL()
    {
        dllLoader?.UnloadDLL();
        IsLoaded = false;
    }

    public void Dispose()
    {
        dllLoader?.Dispose();
    }
}