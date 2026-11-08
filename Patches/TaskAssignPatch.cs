using AmongUs.GameOptions;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Random = UnityEngine.Random;

namespace AmongUsRevamped;

//  https://github.com/Gurge44/EndlessHostRoles/blob/main/Patches/TaskAssignPatch.cs

[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.AddTasksFromList))]
internal static class AddTasksFromListPatch
{
    public static Dictionary<TaskTypes, OptionItem> DisableTasksSettings = new();

    public static void InitializeTaskSettings()
    {
        if (DisableTasksSettings.Count > 0) return;

        DisableTasksSettings = new()
        {
            [TaskTypes.SwipeCard] = Options.DisableSwipeCard,
            [TaskTypes.SubmitScan] = Options.DisableSubmitScan,
            [TaskTypes.UnlockSafe] = Options.DisableUnlockSafe,
            [TaskTypes.UploadData] = Options.DisableUploadData,
            [TaskTypes.StartReactor] = Options.DisableStartReactor,
            [TaskTypes.ResetBreakers] = Options.DisableResetBreaker,
            [TaskTypes.VentCleaning] = Options.DisableCleanVent,
            [TaskTypes.CalibrateDistributor] = Options.DisableCalibrateDistributor,
            [TaskTypes.ChartCourse] = Options.DisableChartCourse,
            [TaskTypes.StabilizeSteering] = Options.DisableStabilizeSteering,
            [TaskTypes.CleanO2Filter] = Options.DisableCleanO2Filter,
            [TaskTypes.UnlockManifolds] = Options.DisableUnlockManifolds,
            [TaskTypes.PrimeShields] = Options.DisablePrimeShields,
            [TaskTypes.MeasureWeather] = Options.DisableMeasureWeather,
            [TaskTypes.BuyBeverage] = Options.DisableBuyBeverage,
            [TaskTypes.AssembleArtifact] = Options.DisableAssembleArtifact,
            [TaskTypes.SortSamples] = Options.DisableSortSamples,
            [TaskTypes.ProcessData] = Options.DisableProcessData,
            [TaskTypes.RunDiagnostics] = Options.DisableRunDiagnostics,
            [TaskTypes.RepairDrill] = Options.DisableRepairDrill,
            [TaskTypes.AlignTelescope] = Options.DisableAlignTelescope,
            [TaskTypes.RecordTemperature] = Options.DisableRecordTemperature,
            [TaskTypes.FillCanisters] = Options.DisableFillCanisters,
            [TaskTypes.MonitorOxygen] = Options.DisableMonitorTree,
            [TaskTypes.StoreArtifacts] = Options.DisableStoreArtifacts,
            [TaskTypes.PutAwayPistols] = Options.DisablePutAwayPistols,
            [TaskTypes.PutAwayRifles] = Options.DisablePutAwayRifles,
            [TaskTypes.MakeBurger] = Options.DisableMakeBurger,
            [TaskTypes.CleanToilet] = Options.DisableCleanToilet,
            [TaskTypes.Decontaminate] = Options.DisableDecontaminate,
            [TaskTypes.SortRecords] = Options.DisableSortRecords,
            [TaskTypes.FixShower] = Options.DisableFixShower,
            [TaskTypes.PickUpTowels] = Options.DisablePickUpTowels,
            [TaskTypes.PolishRuby] = Options.DisablePolishRuby,
            [TaskTypes.DressMannequin] = Options.DisableDressMannequin,
            [TaskTypes.AlignEngineOutput] = Options.DisableAlignEngineOutput,
            [TaskTypes.InspectSample] = Options.DisableInspectSample,
            [TaskTypes.EmptyChute] = Options.DisableEmptyChute,
            [TaskTypes.ClearAsteroids] = Options.DisableClearAsteroids,
            [TaskTypes.WaterPlants] = Options.DisableWaterPlants,
            [TaskTypes.OpenWaterways] = Options.DisableOpenWaterways,
            [TaskTypes.ReplaceWaterJug] = Options.DisableReplaceWaterJug,
            [TaskTypes.RebootWifi] = Options.DisableRebootWifi,
            [TaskTypes.DevelopPhotos] = Options.DisableDevelopPhotos,
            [TaskTypes.RewindTapes] = Options.DisableRewindTapes,
            [TaskTypes.StartFans] = Options.DisableStartFans,
            [TaskTypes.FixWiring] = Options.DisableFixWiring,
            [TaskTypes.EnterIdCode] = Options.DisableEnterIdCode,
            [TaskTypes.InsertKeys] = Options.DisableInsertKeys,
            [TaskTypes.ScanBoardingPass] = Options.DisableScanBoardingPass,
            [TaskTypes.EmptyGarbage] = Options.DisableEmptyGarbage,
            [TaskTypes.FuelEngines] = Options.DisableFuelEngines,
            [TaskTypes.DivertPower] = Options.DisableDivertPower,
            [TaskTypes.FixWeatherNode] = Options.DisableActivateWeatherNodes,
            [TaskTypes.RoastMarshmallow] = Options.DisableRoastMarshmallow,
            [TaskTypes.CollectSamples] = Options.DisableCollectSamples,
            [TaskTypes.ReplaceParts] = Options.DisableReplaceParts,
            [TaskTypes.CollectVegetables] = Options.DisableCollectVegetables,
            [TaskTypes.MineOres] = Options.DisableMineOres,
            [TaskTypes.ExtractFuel] = Options.DisableExtractFuel,
            [TaskTypes.CatchFish] = Options.DisableCatchFish,
            [TaskTypes.PolishGem] = Options.DisablePolishGem,
            [TaskTypes.HelpCritter] = Options.DisableHelpCritter,
            [TaskTypes.HoistSupplies] = Options.DisableHoistSupplies,
            [TaskTypes.FixAntenna] = Options.DisableFixAntenna,
            [TaskTypes.BuildSandcastle] = Options.DisableBuildSandcastle,
            [TaskTypes.CrankGenerator] = Options.DisableCrankGenerator,
            [TaskTypes.MonitorMushroom] = Options.DisableMonitorMushroom,
            [TaskTypes.PlayVideogame] = Options.DisablePlayVideoGame,
            [TaskTypes.TuneRadio] = Options.DisableFindSignal,
            [TaskTypes.TestFrisbee] = Options.DisableThrowFisbee,
            [TaskTypes.LiftWeights] = Options.DisableLiftWeights,
            [TaskTypes.CollectShells] = Options.DisableCollectShells
        };

    }

    private static string GetMapName(OptionItem parent)
    {
        if (parent == Options.DisableMiraTasks) return "Mira HQ";
        if (parent == Options.DisablePolusTasks) return "Polus";
        if (parent == Options.DisableAirshipTasks) return "Airship";
        if (parent == Options.DisableFungleTasks) return "Fungle";
        return null;
    }

    private static string GetTaskCategory(OptionItem parent)
    {
        if (parent == Options.DisableMiscCommonTasks) return "Common Task";
        if (parent == Options.DisableMiscShortTasks) return "Short Task";
        if (parent == Options.DisableMiscLongTasks) return "Long Task";
        if (parent == Options.DisableMiscMixedTasks) return "Mixed Task";
        return "Task";
    }

    private static string HumanizeTaskType(string value)
    {
        System.Text.StringBuilder result = new(value.Length + 8);
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if (index > 0 && char.IsUpper(character) && !char.IsUpper(value[index - 1])) result.Append(' ');
            result.Append(character);
        }
        return result.ToString();
    }

    public static void Prefix([HarmonyArgument(4)] Il2CppSystem.Collections.Generic.List<NormalPlayerTask> unusedTasks)
    {
        if (!AmongUsClient.Instance.AmHost) return;

        InitializeTaskSettings();
        if (!Options.DisableMiraTasks.GetBool() && !Options.DisablePolusTasks.GetBool() &&
            !Options.DisableAirshipTasks.GetBool() && !Options.DisableFungleTasks.GetBool() &&
            !Options.DisableMiscCommonTasks.GetBool() && !Options.DisableMiscShortTasks.GetBool() &&
            !Options.DisableMiscLongTasks.GetBool() && !Options.DisableMiscMixedTasks.GetBool()) return;

        List<NormalPlayerTask> disabledTasks = new();
        foreach (NormalPlayerTask task in unusedTasks)
        {
            if (DisableTasksSettings.TryGetValue(task.TaskType, out OptionItem setting) && setting.GetBool())
                disabledTasks.Add(task);
        }

        foreach (NormalPlayerTask task in disabledTasks)
        {
            Logger.Msg($"Deleted assigned task: {task.TaskType}", "AddTask");
            unusedTasks.Remove(task);
        }
    }
}

// Task filtering is handled by the AddTasksFromListPatch declared above.

[HarmonyPatch(typeof(NetworkedPlayerInfo), nameof(NetworkedPlayerInfo.RpcSetTasks))]
internal static class RpcSetTasksPatch
{
    public static Il2CppStructArray<byte> GlobalTaskIds = null;

    public static void Prefix(NetworkedPlayerInfo __instance, [HarmonyArgument(0)] ref Il2CppStructArray<byte> taskTypeIds)
    {
        if (!AmongUsClient.Instance.AmHost) return;

        PlayerControl pc = __instance.Object;
        if (pc == null) return;

        if (Main.GM.Value && pc == PlayerControl.LocalPlayer)
        {
            taskTypeIds = new Il2CppStructArray<byte>(0);
            return;
        }

        if (!Options.AllPlayersSameTasks.GetBool())
        {
            RerollTasks(pc, ref taskTypeIds);
            return;
        }

        if (GlobalTaskIds == null)
        {
            GlobalTaskIds = new Il2CppStructArray<byte>(taskTypeIds.Length);
            for (int i = 0; i < taskTypeIds.Length; i++) GlobalTaskIds[i] = taskTypeIds[i];
            return;
        }

        taskTypeIds = new Il2CppStructArray<byte>(GlobalTaskIds.Length);
        for (int i = 0; i < GlobalTaskIds.Length; i++) taskTypeIds[i] = GlobalTaskIds[i];
    }

    private static void RerollTasks(PlayerControl pc, ref Il2CppStructArray<byte> taskTypeIds)
    {
        bool hasCommonTasks = taskTypeIds.Length > 0;
        int numCommonTasks = Main.NormalOptions.NumCommonTasks;
        int numLongTasks = Main.NormalOptions.NumLongTasks;
        int numShortTasks = Main.NormalOptions.NumShortTasks;

        if (!hasCommonTasks && numLongTasks == 0 && numShortTasks == 0) numShortTasks = 1;
        else if (hasCommonTasks && numLongTasks == Main.NormalOptions.NumLongTasks && numShortTasks == Main.NormalOptions.NumShortTasks) return;

        Il2CppSystem.Collections.Generic.List<byte> tasksList = new();
        Il2CppSystem.Collections.Generic.HashSet<TaskTypes> usedTaskTypes = new();
        int commonStart = 0;
        int longStart = 0;
        int shortStart = 0;

        Il2CppSystem.Collections.Generic.List<NormalPlayerTask> commonTasks = new();
        foreach (NormalPlayerTask task in ShipStatus.Instance.CommonTasks) commonTasks.Add(task);
        Shuffle(commonTasks);
        ShipStatus.Instance.AddTasksFromList(ref commonStart, numCommonTasks, tasksList, usedTaskTypes, commonTasks);

        Il2CppSystem.Collections.Generic.List<NormalPlayerTask> longTasks = new();
        foreach (NormalPlayerTask task in ShipStatus.Instance.LongTasks) longTasks.Add(task);
        Shuffle(longTasks);
        ShipStatus.Instance.AddTasksFromList(ref longStart, numLongTasks, tasksList, usedTaskTypes, longTasks);

        Il2CppSystem.Collections.Generic.List<NormalPlayerTask> shortTasks = new();
        foreach (NormalPlayerTask task in ShipStatus.Instance.ShortTasks) shortTasks.Add(task);
        Shuffle(shortTasks);
        ShipStatus.Instance.AddTasksFromList(ref shortStart, numShortTasks, tasksList, usedTaskTypes, shortTasks);

        taskTypeIds = new Il2CppStructArray<byte>(tasksList.Count);
        for (int i = 0; i < tasksList.Count; i++) taskTypeIds[i] = tasksList[i];
    }

    private static void Shuffle<T>(Il2CppSystem.Collections.Generic.List<T> list)
    {
        for (int i = 0; i < list.Count - 1; i++)
        {
            T temp = list[i];
            int randomIndex = Random.Range(i, list.Count);
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }
}