using System;
using System.Runtime.InteropServices;
using UnityEngine;
using System.IO;

/// <summary>
/// Dynamic DLL loader for hot-reloading Unity plugins
/// </summary>
public class DynamicDLLLoader
{
    private IntPtr dllHandle = IntPtr.Zero;
    private string dllPath;
    private string dllName;
    private FileSystemWatcher fileWatcher;
    private bool isDllLoaded = false;
    
    // Windows API imports
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LoadLibrary(string lpFileName);
    
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);
    
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeLibrary(IntPtr hModule);
    
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern int GetLastError();
    
    public event Action OnDllReloaded;
    
    public DynamicDLLLoader(string dllName)
    {
        this.dllName = dllName;
        this.dllPath = Path.Combine(Application.dataPath, "Plugins", dllName + ".dll");
        
        SetupFileWatcher();
    }
    
    private void SetupFileWatcher()
    {
        string watchDirectory = Path.GetDirectoryName(dllPath);
        if (!Directory.Exists(watchDirectory))
        {
            Directory.CreateDirectory(watchDirectory);
        }
        
        fileWatcher = new FileSystemWatcher(watchDirectory);
        fileWatcher.Filter = "*.dll";
        fileWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size;
        fileWatcher.Changed += OnDllFileChanged;
        fileWatcher.EnableRaisingEvents = true;
        
        Debug.Log($"DLL file watcher setup for: {watchDirectory}");
    }
    
    private void OnDllFileChanged(object sender, FileSystemEventArgs e)
    {
        if (e.FullPath.EndsWith(dllName + ".dll"))
        {
            Debug.Log($"DLL file changed: {e.FullPath}");
            // Delay reload to ensure file is fully written
            UnityMainThreadDispatcher.Instance.Enqueue(() => {
                System.Threading.Thread.Sleep(100);
                ReloadDLL();
            });
        }
    }
    
    public bool LoadDLL()
    {
        if (isDllLoaded)
            return true;
            
        if (!File.Exists(dllPath))
        {
            Debug.LogError($"DLL not found at: {dllPath}");
            return false;
        }
        
        dllHandle = LoadLibrary(dllPath);
        if (dllHandle == IntPtr.Zero)
        {
            int error = GetLastError();
            Debug.LogError($"Failed to load DLL: {dllPath}, Error: {error}");
            return false;
        }
        
        isDllLoaded = true;
        Debug.Log($"Successfully loaded DLL: {dllPath}");
        return true;
    }
    
    public void UnloadDLL()
    {
        if (dllHandle != IntPtr.Zero)
        {
            FreeLibrary(dllHandle);
            dllHandle = IntPtr.Zero;
            isDllLoaded = false;
            Debug.Log($"Unloaded DLL: {dllName}");
        }
    }
    
    public void ReloadDLL()
    {
        Debug.Log($"Reloading DLL: {dllName}");
        UnloadDLL();
        
        if (LoadDLL())
        {
            OnDllReloaded?.Invoke();
            Debug.Log($"Successfully reloaded DLL: {dllName}");
        }
        else
        {
            Debug.LogError($"Failed to reload DLL: {dllName}");
        }
    }
    
    public IntPtr GetFunctionPointer(string functionName)
    {
        if (dllHandle == IntPtr.Zero)
        {
            Debug.LogError("DLL not loaded");
            return IntPtr.Zero;
        }
        
        IntPtr funcPtr = GetProcAddress(dllHandle, functionName);
        if (funcPtr == IntPtr.Zero)
        {
            Debug.LogError($"Function not found: {functionName}");
        }
        
        return funcPtr;
    }
    
    public T GetFunction<T>(string functionName) where T : class
    {
        IntPtr funcPtr = GetFunctionPointer(functionName);
        if (funcPtr == IntPtr.Zero)
            return null;
            
        return Marshal.GetDelegateForFunctionPointer<T>(funcPtr);
    }
    
    public void Dispose()
    {
        fileWatcher?.Dispose();
        UnloadDLL();
    }
    
    public bool IsLoaded => isDllLoaded;
}

/// <summary>
/// Helper class to execute actions on Unity main thread
/// </summary>
public class UnityMainThreadDispatcher : MonoBehaviour
{
    private static UnityMainThreadDispatcher instance;
    private static readonly object lockObject = new object();
    private readonly System.Collections.Generic.Queue<Action> actionQueue = new System.Collections.Generic.Queue<Action>();
    
    public static UnityMainThreadDispatcher Instance
    {
        get
        {
            if (instance == null)
            {
                lock (lockObject)
                {
                    if (instance == null)
                    {
                        GameObject go = new GameObject("UnityMainThreadDispatcher");
                        instance = go.AddComponent<UnityMainThreadDispatcher>();
                        DontDestroyOnLoad(go);
                    }
                }
            }
            return instance;
        }
    }
    
    public void Enqueue(Action action)
    {
        lock (actionQueue)
        {
            actionQueue.Enqueue(action);
        }
    }
    
    private void Update()
    {
        while (actionQueue.Count > 0)
        {
            Action action;
            lock (actionQueue)
            {
                action = actionQueue.Dequeue();
            }
            action?.Invoke();
        }
    }
}