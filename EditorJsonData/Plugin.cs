using System.Reflection;
using BepInEx;
using HarmonyLib;

namespace EditorJsonData;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "Pikachu.EditorJsonData";
    public const string PluginName = "Editor Json Data";
    public const string PluginVersion = "1.0.1";

    private void Awake()
    {
        Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
        Logger.LogInfo("Plugin [Editor Json Data] is loaded!");
    }
}