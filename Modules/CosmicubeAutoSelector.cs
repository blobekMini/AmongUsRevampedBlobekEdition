using AmongUs.Data;
using System;
using System.Linq;

namespace AmongUsRevamped;

internal static class CosmicubeAutoSelector
{
    private sealed class CubeProgress
    {
        public CosmicubeData Cube { get; init; }
        public int TotalCost { get; init; }
        public int SpentCost { get; init; }
        public int RemainingCost { get; init; }
        public int AvailablePods { get; init; }
        public bool IsComplete { get; init; }
        public double CompletionRatio => TotalCost <= 0 ? (IsComplete ? 1d : 0d) : (double)SpentCost / TotalCost;
    }

    private static bool evaluationScheduled;

    public static void ScheduleEvaluation()
    {
        if (Main.AutoSwitchCosmicube?.Value != true || evaluationScheduled) return;

        evaluationScheduled = true;
        _ = new LateTask(() =>
        {
            evaluationScheduled = false;
            Evaluate();
        }, 0.75f, "AutoSwitchCosmicube", false);
    }

    private static void Evaluate()
    {
        if (Main.AutoSwitchCosmicube?.Value != true ||
            DataManager.Player?.Store == null || DataManager.Player.Purchases == null ||
            InventoryManager.Instance == null || CosmicubeManager.Instance == null || AmongUsClient.Instance == null)
            return;

        try
        {
            CosmicubeData[] unlockedCubes = CosmicubeManager.Instance.GetUnlockedCubes();
            if (unlockedCubes == null || unlockedCubes.Length == 0) return;

            CubeProgress[] progress = unlockedCubes
                .Where(cube => cube != null && !cube.IsEmpty)
                .Select(CreateProgress)
                .Where(value => value != null)
                .ToArray();
            if (progress.Length == 0) return;

            string activeCubeId = DataManager.Player.Store.ActiveCosmicube;
            CubeProgress activeCube = progress.FirstOrDefault(value => IsActiveCube(activeCubeId, value.Cube));
            if (activeCube != null && !activeCube.IsComplete && activeCube.AvailablePods < activeCube.RemainingCost)
                return;

            CubeProgress nextCube = progress
                .Where(value => !value.IsComplete && (activeCube == null || !ReferenceEquals(value.Cube, activeCube.Cube)))
                .OrderBy(value => value.CompletionRatio)
                .ThenBy(value => value.Cube.ProdId, StringComparer.Ordinal)
                .FirstOrDefault();

            if (nextCube == null)
            {
                if (progress.All(value => value.IsComplete) && !string.IsNullOrWhiteSpace(activeCubeId))
                {
                    AmongUsClient.Instance.SetActivePodType(string.Empty);
                    Logger.Info("Automatically deactivated the active Cosmicube because all unlocked Cosmicubes are complete.", "AutoSwitchCosmicube");
                }
                return;
            }

            if (IsActiveCube(activeCubeId, nextCube.Cube)) return;

            AmongUsClient.Instance.SetActivePodType(nextCube.Cube.podId);
            Logger.Info(
                $"Automatically switched active Cosmicube to {nextCube.Cube.GetCosmicubeName()} " +
                $"({nextCube.SpentCost}/{nextCube.TotalCost} pod cost; {nextCube.AvailablePods} pods available).",
                "AutoSwitchCosmicube");
        }
        catch (Exception ex)
        {
            Logger.Exception(ex, "AutoSwitchCosmicube");
        }
    }

    private static CubeProgress CreateProgress(CosmicubeData cube)
    {
        if (cube.allUnlockables == null) return null;

        int totalCost = 0;
        int spentCost = 0;
        int remainingCost = 0;
        bool allPurchased = true;

        foreach (UnlockableCosmetic unlockable in cube.allUnlockables)
        {
            if (unlockable == null) continue;

            int cost = Math.Max(0, unlockable.cost);
            totalCost += cost;
            bool purchased = DataManager.Player.Purchases.GetPurchase(unlockable.GetProdId(), cube.ProdId);
            if (purchased)
                spentCost += cost;
            else
            {
                remainingCost += cost;
                allPurchased = false;
            }
        }

        int availablePods = InventoryManager.Instance.GetPodCount(cube.podId);
        return new CubeProgress
        {
            Cube = cube,
            TotalCost = totalCost,
            SpentCost = spentCost,
            RemainingCost = remainingCost,
            AvailablePods = Math.Max(0, availablePods),
            IsComplete = allPurchased
        };
    }

    private static bool IsActiveCube(string activeCubeId, CosmicubeData cube)
    {
        if (string.IsNullOrWhiteSpace(activeCubeId) || cube == null) return false;
        return activeCubeId.Equals(cube.ProdId, StringComparison.OrdinalIgnoreCase) ||
            activeCubeId.Equals(cube.podId, StringComparison.OrdinalIgnoreCase);
    }
}

[HarmonyPatch(typeof(InventoryManager), nameof(InventoryManager.ChangePodCount))]
internal static class InventoryManagerChangePodCountPatch
{
    public static void Postfix()
    {
        CosmicubeAutoSelector.ScheduleEvaluation();
    }
}

[HarmonyPatch(typeof(InventoryManager), "CheckPods")]
internal static class InventoryManagerCheckPodsPatch
{
    public static void Postfix()
    {
        CosmicubeAutoSelector.ScheduleEvaluation();
    }
}

[HarmonyPatch(typeof(InnerNet.InnerNetClient), nameof(InnerNet.InnerNetClient.SetActivePodType))]
internal static class InnerNetClientSetActivePodTypePatch
{
    public static void Postfix()
    {
        CosmicubeAutoSelector.ScheduleEvaluation();
    }
}
