using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Lethe.Patches;

namespace CustomMirror;

[BepInPlugin(GUID, NAME, VERSION)]
public class Main : BasePlugin
{
    public const string GUID = $"{AUTHOR}.{NAME}";
    public const string NAME = "CustomMirror";
    public const string VERSION = "0.0.1";
    public const string AUTHOR = "lob";

    public override void Load()
    {
        Harmony harmony = new Harmony(NAME);

        // Load all Harmony patches in this assembly.
        harmony.PatchAll();

        Log.LogInfo("[Lethe] Harmony patches loaded.");
    }
}