using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace EditorJsonData;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public class Plugin : BaseUnityPlugin
{
    private const string PluginGuid = "Pikachu.EditorJsonData";
    public const string PluginName = "Editor Json Data";
    public const string PluginVersion = "1.0.3";

    // internal static Plugin Instance;
    // public static string PluginPath => Path.GetDirectoryName(Instance.Info.Location);
    internal static ManualLogSource Log;
    private static readonly Harmony Harmony = new(PluginGuid);

    private void Awake()
    {
        Harmony.PatchAll();

        // Instance = this;
        Log = Logger;
        Log.LogInfo($"Plugin {PluginName} is loaded!");
    }
}