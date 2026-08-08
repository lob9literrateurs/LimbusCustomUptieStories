using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using Lethe;
using SimpleJSON;
using UnityEngine;

namespace CustomMirror.Patches;

public static class CustomMirrorStoryHelper
{
    private static readonly Dictionary<int, int> StoryPositions =
        new();

    // ============================================================
    // Custom story metadata
    // ============================================================

    public static bool TryGetCustomStoryId(
        int mirrorWorldId,
        int personalityId,
        out string storyId)
    {
        storyId = null;

        foreach (string metaPath in EnumerateCustomStoryMetaFiles())
        {
            try
            {
                JSONNode meta =
                    JSON.Parse(File.ReadAllText(metaPath));

                if (meta == null)
                    continue;

                if (!int.TryParse(
                        meta["mirrorWorldID"]?.Value,
                        out int metadataWorldId))
                {
                    continue;
                }

                if (!int.TryParse(
                        meta["mirrorPersonality"]?.Value,
                        out int metadataPersonality))
                {
                    continue;
                }

                if (metadataWorldId != mirrorWorldId ||
                    metadataPersonality != personalityId)
                {
                    continue;
                }

                string metadataStoryId =
                    meta["id"]?.Value;

                if (string.IsNullOrWhiteSpace(metadataStoryId))
                {
                    LetheCustomDebug.LogWarning(
                        $"[CustomMirror] Matched metadata has no " +
                        $"story id: {metaPath}"
                    );

                    continue;
                }

                storyId = metadataStoryId;

                return true;
            }
            catch (Exception ex)
            {
                LetheCustomDebug.LogWarning(
                    $"[CustomMirror] Failed reading " +
                    $"{metaPath}: {ex.Message}"
                );
            }
        }

        return false;
    }

    /*
     * Gets custom story metadata by personality.
     *
     * mirrorWorldId:
     *
     *     > 0  = normal mirror-world custom story
     *     -1   = standalone custom story
     *
     * A standalone custom story is identified by:
     *
     *     mirrorWorldID missing
     *     OR
     *     mirrorWorldID == -1
     *
     * and a valid mirrorPersonality.
     *
     * Standalone stories do NOT require mirrorPos.
     */
    public static bool TryGetCustomStoryForPersonality(
        int personalityId,
        out int mirrorWorldId,
        out int mirrorPos,
        out string storyId,
        out string storyName,
        out bool dynamicNumber)
    {
        mirrorWorldId = 0;
        mirrorPos = 0;
        storyId = null;
        storyName = null;
        dynamicNumber = false;

        foreach (string metaPath in EnumerateCustomStoryMetaFiles())
        {
            try
            {
                JSONNode meta =
                    JSON.Parse(File.ReadAllText(metaPath));

                if (meta == null)
                    continue;

                /*
                 * mirrorPersonality is required for all custom
                 * stories.
                 */
                if (!int.TryParse(
                        meta["mirrorPersonality"]?.Value,
                        out int metadataPersonality))
                {
                    continue;
                }

                if (metadataPersonality != personalityId)
                    continue;

                /*
                 * mirrorWorldID is optional.
                 *
                 * Missing = standalone.
                 * -1      = standalone.
                 * > 0     = mirror-world story.
                 */
                int worldId = -1;

                string worldValue =
                    meta["mirrorWorldID"]?.Value;

                if (!string.IsNullOrWhiteSpace(worldValue))
                {
                    if (!int.TryParse(
                            worldValue,
                            out worldId))
                    {
                        continue;
                    }

                    /*
                     * Only -1 is considered standalone.
                     * Other non-positive values are invalid.
                     */
                    if (worldId == 0)
                        continue;

                    if (worldId < -1)
                        continue;
                }

                /*
                 * mirrorPos is required only for actual
                 * mirror-world stories.
                 */
                int position = -1;

                if (worldId > 0)
                {
                    if (!int.TryParse(
                            meta["mirrorPos"]?.Value,
                            out position))
                    {
                        continue;
                    }
                }
                else
                {
                    /*
                     * Standalone stories have no mirror-world
                     * position.
                     */
                    position = -1;
                }

                string id =
                    meta["id"]?.Value;

                if (string.IsNullOrWhiteSpace(id))
                    continue;

                string name =
                    meta["title"]?.Value;

                if (string.IsNullOrWhiteSpace(name))
                {
                    LetheCustomDebug.LogWarning(
                        $"[CustomMirror] Custom story '{id}' " +
                        $"has no storyName: {metaPath}"
                    );

                    continue;
                }

                bool metadataDynamicNumber = false;

                string dynamicNumberValue =
                    meta["dynamicNumber"]?.Value;

                if (!string.IsNullOrWhiteSpace(
                        dynamicNumberValue))
                {
                    bool.TryParse(
                        dynamicNumberValue,
                        out metadataDynamicNumber
                    );
                }

                mirrorWorldId = worldId;
                mirrorPos = position;
                storyId = id;
                storyName = name;
                dynamicNumber = metadataDynamicNumber;

                return true;
            }
            catch (Exception ex)
            {
                LetheCustomDebug.LogWarning(
                    $"[CustomMirror] Failed reading custom story " +
                    $"metadata '{metaPath}': {ex.Message}"
                );
            }
        }

        return false;
    }

    /*
     * Returns true when a custom personality represents a
     * standalone story rather than a mirror-world story.
     */
    public static bool IsStandaloneCustomStory(
        int personalityId,
        out string storyId)
    {
        storyId = null;

        if (!TryGetCustomStoryForPersonality(
                personalityId,
                out int mirrorWorldId,
                out _,
                out storyId,
                out _,
                out _))
        {
            return false;
        }

        return mirrorWorldId == -1;
    }

    public static StoryTheaterPersonalityInfo CreateStoryInfo(
        string storyId)
    {
        if (string.IsNullOrWhiteSpace(storyId))
            return null;

        try
        {
            return JsonUtility.FromJson<StoryTheaterPersonalityInfo>(
                $"{{\"storyId\":\"{EscapeJson(storyId)}\"}}"
            );
        }
        catch (Exception ex)
        {
            LetheCustomDebug.LogWarning(
                $"[CustomMirror] Failed creating " +
                $"StoryTheaterPersonalityInfo for '{storyId}': " +
                $"{ex.Message}"
            );

            return null;
        }
    }

    public static IEnumerable<string> EnumerateCustomStoryMetaFiles()
    {
        string modsPath;

        try
        {
            if (LetheMain.modsPath == null)
                yield break;

            modsPath =
                LetheMain.modsPath.FullPath;
        }
        catch
        {
            yield break;
        }

        if (string.IsNullOrWhiteSpace(modsPath) ||
            !Directory.Exists(modsPath))
        {
            yield break;
        }

        string[] modDirectories;

        try
        {
            modDirectories =
                Directory.GetDirectories(modsPath);
        }
        catch (Exception ex)
        {
            LetheCustomDebug.LogWarning(
                $"[CustomMirror] Failed enumerating mods: " +
                $"{ex.Message}"
            );

            yield break;
        }

        foreach (string modDirectory in modDirectories)
        {
            string modName =
                Path.GetFileName(modDirectory);

            if (modName.StartsWith(
                    "DISABLED",
                    StringComparison.OrdinalIgnoreCase) ||
                modName.StartsWith(
                    "FULLDISABLED",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string customStoriesPath =
                Path.Combine(
                    modDirectory,
                    "custom_stories"
                );

            if (!Directory.Exists(customStoriesPath))
                continue;

            string[] storyDirectories;

            try
            {
                storyDirectories =
                    Directory.GetDirectories(
                        customStoriesPath
                    );
            }
            catch
            {
                continue;
            }

            foreach (string storyDirectory in storyDirectories)
            {
                string metaPath =
                    Path.Combine(
                        storyDirectory,
                        "meta.json"
                    );

                if (File.Exists(metaPath))
                    yield return metaPath;
            }
        }
    }

    private static string EscapeJson(string value)
    {
        if (value == null)
            return string.Empty;

        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"");
    }

    // ============================================================
    // Mirror-world injection
    // ============================================================

    public static bool IsPersonalityInjected(
        int mirrorWorldId,
        int personalityId)
    {
        /*
         * Standalone stories are deliberately never considered
         * injected into a mirror world.
         */
        if (mirrorWorldId <= 0)
            return false;

        foreach (string metaPath in
                 EnumerateCustomStoryMetaFiles())
        {
            try
            {
                JSONNode meta =
                    JSON.Parse(
                        File.ReadAllText(metaPath)
                    );

                if (meta == null)
                    continue;

                if (!int.TryParse(
                        meta["mirrorWorldID"]?.Value,
                        out int worldId))
                {
                    continue;
                }

                if (!int.TryParse(
                        meta["mirrorPersonality"]?.Value,
                        out int metadataPersonality))
                {
                    continue;
                }

                if (worldId == mirrorWorldId &&
                    metadataPersonality == personalityId)
                {
                    return true;
                }
            }
            catch
            {
            }
        }

        return false;
    }

    public static bool EnsurePersonalityInjected(
        MirrorWorldStoryTheaterData mirrorWorld,
        int personalityId,
        int mirrorPos)
    {
        if (mirrorWorld == null)
            return false;

        if (mirrorWorld.MirrorWorldId <= 0)
            return false;

        Il2CppSystem.Collections.Generic.List<int> personalityList =
            mirrorWorld.NeedsPersonalityIdList;

        if (personalityList == null)
        {
            LetheCustomDebug.LogWarning(
                $"[CustomMirror] Personality list is null " +
                $"for mirrorWorldId {mirrorWorld.MirrorWorldId}."
            );

            return false;
        }

        if (personalityList.Contains(personalityId))
            return false;

        int insertPosition =
            Math.Max(
                0,
                Math.Min(
                    mirrorPos,
                    personalityList.Count
                )
            );

        personalityList.Insert(
            insertPosition,
            personalityId
        );

        return true;
    }

    public static void CaptureStoryPositions(
        MirrorWorldStoryTheaterData mirrorWorld)
    {
        if (mirrorWorld == null)
            return;

        Il2CppSystem.Collections.Generic.List<int> personalityList =
            mirrorWorld.NeedsPersonalityIdList;

        if (personalityList == null)
            return;

        for (int i = 0; i < personalityList.Count; i++)
        {
            int personalityId =
                personalityList[i];

            StoryPositions[personalityId] = i;
        }
    }

    public static bool TryGetStoryPosition(
        int personalityId,
        out int position)
    {
        return StoryPositions.TryGetValue(
            personalityId,
            out position
        );
    }

    public static bool TryGetCurrentStoryPosition(
        MainUI.MirrorStoryNodeSelectUI instance,
        int personalityId,
        out int position)
    {
        position = -1;

        if (instance == null)
            return false;

        MirrorWorldStoryTheaterData storyData =
            instance._currentStoryTheaterData;

        if (storyData == null)
            return false;

        Il2CppSystem.Collections.Generic.List<int> personalityList =
            storyData.NeedsPersonalityIdList;

        if (personalityList == null)
            return false;

        for (int i = 0; i < personalityList.Count; i++)
        {
            if (personalityList[i] == personalityId)
            {
                position = i;
                return true;
            }
        }

        return false;
    }

    public static string ToRomanNumeral(int number)
    {
        if (number <= 0)
            return string.Empty;

        string[] thousands =
        {
            "",
            "M",
            "MM",
            "MMM"
        };

        string[] hundreds =
        {
            "",
            "C",
            "CC",
            "CCC",
            "CD",
            "D",
            "DC",
            "DCC",
            "DCCC",
            "CM"
        };

        string[] tens =
        {
            "",
            "X",
            "XX",
            "XXX",
            "XL",
            "L",
            "LX",
            "LXX",
            "LXXX",
            "XC"
        };

        string[] ones =
        {
            "",
            "I",
            "II",
            "III",
            "IV",
            "V",
            "VI",
            "VII",
            "VIII",
            "IX"
        };

        if (number >= 4000)
            return number.ToString();

        return
            thousands[number / 1000] +
            hundreds[(number % 1000) / 100] +
            tens[(number % 100) / 10] +
            ones[number % 10];
    }

    public static void InjectIntoDataList(
        MirrorWorldStoryTheaterDataList dataList)
    {
        if (dataList == null)
            return;

        StoryPositions.Clear();

        Il2CppSystem.Collections.Generic.List<
            MirrorWorldStoryTheaterData> mirrorWorlds =
            dataList.GetData();

        if (mirrorWorlds == null)
        {
            LetheCustomDebug.LogWarning(
                "[CustomMirror] MirrorWorldStoryTheaterDataList " +
                "returned null."
            );

            return;
        }

        foreach (string metaPath in
                 EnumerateCustomStoryMetaFiles())
        {
            try
            {
                JSONNode meta =
                    JSON.Parse(
                        File.ReadAllText(metaPath)
                    );

                if (meta == null)
                    continue;

                /*
                 * Standalone stories are intentionally skipped
                 * from mirror-world injection.
                 */
                if (!int.TryParse(
                        meta["mirrorWorldID"]?.Value,
                        out int metadataWorldId))
                {
                    continue;
                }

                if (metadataWorldId <= 0)
                    continue;

                if (!int.TryParse(
                        meta["mirrorPersonality"]?.Value,
                        out int mirrorPersonality))
                {
                    continue;
                }

                if (!int.TryParse(
                        meta["mirrorPos"]?.Value,
                        out int mirrorPos))
                {
                    continue;
                }

                foreach (MirrorWorldStoryTheaterData mirrorWorld
                         in mirrorWorlds)
                {
                    if (mirrorWorld == null)
                        continue;

                    if (mirrorWorld.MirrorWorldId !=
                        metadataWorldId)
                    {
                        continue;
                    }

                    EnsurePersonalityInjected(
                        mirrorWorld,
                        mirrorPersonality,
                        mirrorPos
                    );
                }
            }
            catch (Exception ex)
            {
                LetheCustomDebug.LogWarning(
                    $"[CustomMirror] Failed processing " +
                    $"{metaPath}: {ex.Message}"
                );
            }
        }

        foreach (MirrorWorldStoryTheaterData mirrorWorld
                 in mirrorWorlds)
        {
            CaptureStoryPositions(mirrorWorld);
        }
    }
}


// ============================================================
// MirrorWorldStoryTheaterDataList
// ============================================================

[HarmonyPatch(typeof(MirrorWorldStoryTheaterDataList))]
public static class MirrorWorldStoryTheaterDataListPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(
        nameof(MirrorWorldStoryTheaterDataList.Init)
    )]
    public static void InitPostfix(
        MirrorWorldStoryTheaterDataList __instance)
    {
        if (__instance == null)
            return;

        CustomMirrorStoryHelper.InjectIntoDataList(
            __instance
        );
    }
}


// ============================================================
// MirrorWorldStoryTheaterData sorting
// ============================================================

[HarmonyPatch(typeof(MirrorWorldStoryTheaterData))]
public static class MirrorWorldStoryTheaterDataSortPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(
        nameof(MirrorWorldStoryTheaterData.SortByOrder)
    )]
    public static bool SortByOrderPrefix(
        int x,
        int y,
        ref int __result)
    {
        bool xIsCustom =
            CustomMirrorStoryHelper.TryGetCustomStoryForPersonality(
                x,
                out _,
                out _,
                out _,
                out _,
                out _
            );

        bool yIsCustom =
            CustomMirrorStoryHelper.TryGetCustomStoryForPersonality(
                y,
                out _,
                out _,
                out _,
                out _,
                out _
            );

        if (!xIsCustom && !yIsCustom)
            return true;

        bool xHasPosition =
            CustomMirrorStoryHelper.TryGetStoryPosition(
                x,
                out int xPosition
            );

        bool yHasPosition =
            CustomMirrorStoryHelper.TryGetStoryPosition(
                y,
                out int yPosition
            );

        if (xHasPosition && yHasPosition)
        {
            __result =
                xPosition.CompareTo(yPosition);

            if (__result == 0)
                __result = x.CompareTo(y);

            return false;
        }

        if (xHasPosition)
        {
            __result = -1;
            return false;
        }

        if (yHasPosition)
        {
            __result = 1;
            return false;
        }

        return true;
    }
}


// ============================================================
// MirrorStoryNodeSelectUI
// ============================================================

[HarmonyPatch(typeof(MainUI.MirrorStoryNodeSelectUI))]
public static class MirrorStoryNodeSelectUIPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(
        nameof(MainUI.MirrorStoryNodeSelectUI.OpenNodeStorySelectUI)
    )]
    public static void OpenNodeStorySelectUIPrefix(
        MainUI.MirrorStoryNodeSelectUI __instance,
        int personalityId)
    {
        if (__instance == null)
            return;

        LetheCustomDebug.LogInfo(
            $"[CustomMirror] >>> OpenNodeStorySelectUI " +
            $"personality={personalityId}"
        );
    }

    [HarmonyPostfix]
    [HarmonyPatch(
        nameof(MainUI.MirrorStoryNodeSelectUI.OpenNodeStorySelectUI)
    )]
    public static void OpenNodeStorySelectUIPostfix(
        MainUI.MirrorStoryNodeSelectUI __instance,
        int personalityId)
    {
        if (__instance == null)
            return;

        ApplyStoryName(
            __instance,
            personalityId
        );
    }

    [HarmonyPostfix]
    [HarmonyPatch(
        nameof(MainUI.MirrorStoryNodeSelectUI.OnStorySelect)
    )]
    public static void OnStorySelectPostfix(
        MainUI.MirrorStoryNodeSelectUI __instance)
    {
        if (__instance == null)
            return;

        int personalityId =
            __instance._currentPersonalityId;

        if (personalityId <= 0)
        {
            personalityId =
                __instance._currentUnitinfoPersonalityId;
        }

        if (personalityId <= 0)
            return;

        ApplyStoryName(
            __instance,
            personalityId
        );
    }

    private static void ApplyStoryName(
        MainUI.MirrorStoryNodeSelectUI instance,
        int personalityId)
    {
        if (instance == null)
            return;

        if (instance._rightStoryNameText == null)
            return;

        /*
         * Get custom metadata first.
         *
         * IMPORTANT:
         *
         * There are six out parameters after personalityId.
         */
        bool isCustom =
            CustomMirrorStoryHelper.TryGetCustomStoryForPersonality(
                personalityId,
                out int mirrorWorldId,
                out _,
                out _,
                out string customTitle,
                out bool customDynamicNumber
            );

        /*
         * Standalone stories don't participate in the mirror-world
         * UI, so there is no position to use for numbering.
         */
        if (isCustom && mirrorWorldId == -1)
        {
            instance._rightStoryNameText.text =
                customTitle ?? string.Empty;

            return;
        }

        /*
         * We need the actual position in the current story list
         * for mirror-world stories and native stories.
         */
        if (!CustomMirrorStoryHelper.TryGetCurrentStoryPosition(
                instance,
                personalityId,
                out int currentPosition))
        {
            /*
             * If we cannot determine the position:
             *
             * Custom -> display configured title.
             * Native  -> leave game's text untouched.
             */
            if (isCustom)
            {
                instance._rightStoryNameText.text =
                    customTitle ?? string.Empty;
            }

            return;
        }

        /*
         * The list position is zero-based.
         *
         * 0 -> I
         * 1 -> II
         * 2 -> III
         *
         * Therefore:
         *
         *     currentPosition + 1
         */
        int displayNumber =
            currentPosition + 1;

        string romanNumber =
            CustomMirrorStoryHelper.ToRomanNumeral(
                displayNumber
            );

        string displayTitle;

        if (isCustom)
        {
            /*
             * CUSTOM MIRROR-WORLD STORY
             *
             * Always start from metadata title.
             *
             * This prevents an existing UI title such as:
             *
             *     "Story I"
             *
             * from being reused on the next OnStorySelect call.
             */
            displayTitle =
                customTitle ?? string.Empty;

            /*
             * Custom numbering is opt-in.
             */
            if (customDynamicNumber)
            {
                displayTitle =
                    RemoveTrailingRomanNumberChain(
                        displayTitle
                    );

                displayTitle =
                    AppendNumberIfNeeded(
                        displayTitle,
                        romanNumber
                    );
            }
        }
        else
        {
            /*
             * NATIVE STORY
             *
             * Native stories are ALWAYS dynamically numbered.
             *
             * The game can call OnStorySelect repeatedly, meaning
             * the existing text could become:
             *
             *     Story I
             *     Story I II
             *     Story I II III
             *
             * Strip every trailing Roman numeral first.
             */
            string nativeTitle =
                instance._rightStoryNameText.text;

            displayTitle =
                RemoveTrailingRomanNumberChain(
                    nativeTitle
                );

            displayTitle =
                AppendNumberIfNeeded(
                    displayTitle,
                    romanNumber
                );
        }

        instance._rightStoryNameText.text =
            displayTitle;

        LetheCustomDebug.LogInfo(
            $"[CustomMirror] Applied story name: " +
            $"personality={personalityId}, " +
            $"custom={isCustom}, " +
            $"mirrorWorld={mirrorWorldId}, " +
            $"dynamicNumber={(isCustom ? customDynamicNumber : true)}, " +
            $"position={currentPosition}, " +
            $"number={displayNumber}, " +
            $"display='{displayTitle}'"
        );
    }

    /*
     * Adds exactly one Roman numeral.
     */
    private static string AppendNumberIfNeeded(
        string title,
        string romanNumber)
    {
        if (string.IsNullOrWhiteSpace(romanNumber))
            return title ?? string.Empty;

        if (string.IsNullOrWhiteSpace(title))
            return romanNumber;

        string trimmedTitle =
            title.TrimEnd();

        string suffix =
            " " + romanNumber;

        if (trimmedTitle.EndsWith(
                suffix,
                StringComparison.Ordinal))
        {
            return trimmedTitle;
        }

        return trimmedTitle + suffix;
    }

    /*
     * Removes one trailing Roman numeral.
     */
    private static string RemoveTrailingRomanNumber(
        string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return title ?? string.Empty;

        string trimmedTitle =
            title.TrimEnd();

        int lastSpace =
            trimmedTitle.LastIndexOf(' ');

        if (lastSpace < 0)
            return trimmedTitle;

        string finalToken =
            trimmedTitle.Substring(
                lastSpace + 1
            );

        if (!IsRomanNumeralToken(finalToken))
            return trimmedTitle;

        return trimmedTitle
            .Substring(
                0,
                lastSpace
            )
            .TrimEnd();
    }

    /*
     * Removes an entire trailing chain of Roman numerals.
     *
     * Examples:
     *
     *     Story I
     *     Story I II
     *     Story I II III
     *     Story II III
     *
     * all become:
     *
     *     Story
     */
    private static string RemoveTrailingRomanNumberChain(
        string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return title ?? string.Empty;

        string result =
            title.TrimEnd();

        while (true)
        {
            string stripped =
                RemoveTrailingRomanNumber(result);

            if (string.Equals(
                    stripped,
                    result,
                    StringComparison.Ordinal))
            {
                break;
            }

            result = stripped;
        }

        return result;
    }

    /*
     * Determines whether a token consists entirely of Roman
     * numeral characters.
     */
    private static bool IsRomanNumeralToken(
        string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        for (int i = 0; i < value.Length; i++)
        {
            char c =
                value[i];

            if (c != 'I' &&
                c != 'V' &&
                c != 'X' &&
                c != 'L' &&
                c != 'C' &&
                c != 'D' &&
                c != 'M')
            {
                return false;
            }
        }

        return true;
    }
}


// ============================================================
// MirrorWorldStoryTheaterData.GetStoryIdByPersonalityId
// ============================================================

[HarmonyPatch(typeof(MirrorWorldStoryTheaterData))]
public static class MirrorWorldStoryTheaterDataPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(
        nameof(
            MirrorWorldStoryTheaterData.GetStoryIdByPersonalityId
        )
    )]
    public static bool GetStoryIdByPersonalityIdPrefix(
        MirrorWorldStoryTheaterData __instance,
        int personalityId,
        ref StoryTheaterPersonalityInfo __result)
    {
        if (__instance == null)
            return true;

        int mirrorWorldId =
            __instance.MirrorWorldId;

        /*
         * Only mirror-world custom stories are handled here.
         *
         * Standalone custom stories have no mirror world and are
         * launched directly through PlayClickSound instead.
         */
        if (mirrorWorldId <= 0)
            return true;

        if (!CustomMirrorStoryHelper.TryGetCustomStoryId(
                mirrorWorldId,
                personalityId,
                out string storyId))
        {
            return true;
        }

        StoryTheaterPersonalityInfo storyInfo =
            CustomMirrorStoryHelper.CreateStoryInfo(
                storyId
            );

        if (storyInfo == null)
        {
            LetheCustomDebug.LogWarning(
                $"[CustomMirror] Could not create " +
                $"StoryTheaterPersonalityInfo for '{storyId}'. " +
                $"Allowing original method to run."
            );

            return true;
        }

        __result = storyInfo;

        LetheCustomDebug.LogInfo(
            $"[CustomMirror] Returning custom " +
            $"StoryTheaterPersonalityInfo: " +
            $"world={mirrorWorldId}, " +
            $"personality={personalityId}, " +
            $"story={storyId}"
        );

        return false;
    }
}


// ============================================================
// Generic StoryButton
// ============================================================

[HarmonyPatch(
    typeof(UIButton),
    nameof(UIButton.PlayClickSound),
    typeof(bool)
)]
public static class UIButtonPlayClickSoundPatch
{
    [HarmonyPostfix]
    public static void PlayClickSoundPostfix(
        UIButton __instance,
        bool isLong)
    {
        if (__instance == null)
            return;

        GameObject buttonObject =
            __instance.gameObject;

        if (buttonObject == null)
            return;

        if (!string.Equals(
                buttonObject.name,
                "[Button]StoryButton",
                StringComparison.Ordinal))
        {
            return;
        }

        Transform parent =
            __instance.transform.parent?
                .parent?
                .parent;

        GameObject parentObject =
            parent != null
                ? parent.gameObject
                : null;

        if (parentObject == null)
            return;

        BattleUI.Information.UnitInformationController controller =
            parentObject.GetComponentInChildren<
                BattleUI.Information.UnitInformationController>();

        if (controller == null ||
            controller._infoDataManager == null)
        {
            return;
        }

        int personalityId =
            controller._infoDataManager._contentId;

        if (personalityId <= 0)
            return;

        /*
         * Look up the custom story.
         *
         * Six out parameters are required after personalityId.
         */
        if (!CustomMirrorStoryHelper.TryGetCustomStoryForPersonality(
                personalityId,
                out int mirrorWorldId,
                out _,
                out string storyId,
                out _,
                out _))
        {
            return;
        }

        /*
         * ========================================================
         * STANDALONE CUSTOM STORY
         * ========================================================
         *
         * mirrorWorldID missing or -1 means this story does not
         * belong to the mirror-world system.
         *
         * Therefore we bypass:
         *
         *     MirrorStoryNodeSelectUI
         *     StoryUIPanel
         *     personality injection
         *     mirror-world lookup
         *
         * and launch the custom story directly.
         */
        if (mirrorWorldId == -1)
        {
            try
            {
                Lethe.Patches.CustomStories.LaunchStory(
                    storyId
                );

                LetheCustomDebug.LogInfo(
                    $"[CustomMirror] Standalone custom story " +
                    $"launched directly: " +
                    $"personality={personalityId}, " +
                    $"story={storyId}"
                );
            }
            catch (Exception ex)
            {
                LetheCustomDebug.LogError(
                    $"[CustomMirror] Failed launching standalone " +
                    $"custom story: " +
                    $"personality={personalityId}, " +
                    $"story={storyId}, " +
                    $"{ex}"
                );
            }

            return;
        }

        /*
         * ========================================================
         * NORMAL MIRROR-WORLD CUSTOM STORY
         * ========================================================
         */
        if (!CustomMirrorStoryHelper.IsPersonalityInjected(
                mirrorWorldId,
                personalityId))
        {
            return;
        }

        MainUI.MirrorStoryNodeSelectUI[] uis =
            UnityEngine.Resources.FindObjectsOfTypeAll<
                MainUI.MirrorStoryNodeSelectUI>();

        MainUI.StoryUIPanel[] storyPanels =
            UnityEngine.Resources.FindObjectsOfTypeAll<
                MainUI.StoryUIPanel>();

        if (uis == null ||
            uis.Length == 0 ||
            storyPanels == null ||
            storyPanels.Length == 0)
        {
            return;
        }

        MainUI.MirrorStoryNodeSelectUI ui =
            uis[0];

        MainUI.StoryUIPanel storyPanel =
            storyPanels[0];

        if (ui == null ||
            storyPanel == null)
        {
            return;
        }

        ui.gameObject.SetActive(true);
        storyPanel.gameObject.SetActive(true);

        try
        {
            storyPanel.OpenMirrorStoryNodeSelectUI(
                personalityId,
                200
            );

            if (CustomMirrorStoryHelper.TryGetCurrentStoryPosition(
                    ui,
                    personalityId,
                    out int currentPosition))
            {
                ui.OnStorySelect(
                    currentPosition
                );
            }

            LetheCustomDebug.LogInfo(
                $"[CustomMirror] Generic StoryButton activation: " +
                $"personality={personalityId}, " +
                $"world={mirrorWorldId}, " +
                $"story={storyId}"
            );
        }
        catch (Exception ex)
        {
            LetheCustomDebug.LogWarning(
                $"[CustomMirror] Failed opening custom mirror story " +
                $"for personality={personalityId}: {ex.Message}"
            );
        }
    }
}


// ============================================================
// StoryUIPresenter
// ============================================================

[HarmonyPatch(typeof(MainUI.StoryUIPresenter))]
public static class StoryUIPresenterPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(
        nameof(MainUI.StoryUIPresenter.EnterStoryScene)
    )]
    public static bool EnterStoryScenePrefix(
        MainUI.StoryUIPresenter __instance,
        string storyId,
        int personalityId)
    {
        if (!IsCustomStory(storyId))
            return true;

        LetheCustomDebug.LogInfo(
            $"[CustomMirror] Intercepting custom story launch: " +
            $"story={storyId}, " +
            $"personality={personalityId}"
        );

        try
        {
            Lethe.Patches.CustomStories.LaunchStory(
                storyId
            );
        }
        catch (Exception ex)
        {
            LetheCustomDebug.LogError(
                $"[CustomMirror] Custom story launch failed: " +
                $"{ex}"
            );

            return true;
        }

        return false;
    }

    private static bool IsCustomStory(
        string storyId)
    {
        if (string.IsNullOrWhiteSpace(storyId))
            return false;

        foreach (string metaPath in
                 CustomMirrorStoryHelper.EnumerateCustomStoryMetaFiles())
        {
            try
            {
                JSONNode meta =
                    JSON.Parse(
                        File.ReadAllText(metaPath)
                    );

                if (meta == null)
                    continue;

                string id =
                    meta["id"]?.Value;

                if (string.Equals(
                        id,
                        storyId,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }
            catch
            {
            }
        }

        return false;
    }
}