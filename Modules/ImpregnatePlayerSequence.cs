using System;
using UnityEngine;

namespace AmongUsRevamped;

internal static class ImpregnatePlayerSequence
{
    private const float DurationSeconds = 10f;
    private const float StepSeconds = 0.08f;
    private static Sequence activeSequence;

    private sealed class Sequence
    {
        public PlayerControl First { get; init; }
        public PlayerControl Second { get; init; }
        public PlayerControl Third { get; init; }
        public Vector2 FirstPosition { get; init; }
        public Vector2 SecondPosition { get; init; }
        public Vector2 ThirdPosition { get; init; }
        public bool FirstWasMoveable { get; init; }
        public bool SecondWasMoveable { get; init; }
        public bool ThirdWasMoveable { get; init; }
        public float Elapsed { get; set; }
        public bool IsFinished { get; set; }
    }

    public static bool TryStart(PlayerControl first, PlayerControl second, PlayerControl third, out string message)
    {
        message = string.Empty;
        if (activeSequence != null)
        {
            message = "An /impregnate sequence is already running.";
            return false;
        }
        if (!AmongUsClient.Instance || !AmongUsClient.Instance.AmHost || !Utils.InGame || Utils.IsMeeting ||
            MeetingHud.Instance != null || ShipStatus.Instance == null)
        {
            message = "/impregnate is available to the host during a game outside meetings.";
            return false;
        }
        if (!IsUsablePlayer(first) || !IsUsablePlayer(second) || !IsUsablePlayer(third) ||
            first.PlayerId == second.PlayerId || first.PlayerId == third.PlayerId || second.PlayerId == third.PlayerId)
        {
            message = "/impregnate requires three different connected, living players.";
            return false;
        }

        Sequence sequence = new()
        {
            First = first,
            Second = second,
            Third = third,
            FirstPosition = first.GetTruePosition(),
            SecondPosition = second.GetTruePosition(),
            ThirdPosition = third.GetTruePosition(),
            FirstWasMoveable = first.moveable,
            SecondWasMoveable = second.moveable,
            ThirdWasMoveable = third.moveable
        };

        try
        {
            activeSequence = sequence;
            first.moveable = false;
            second.moveable = false;
            second.NetTransform.RpcSnapTo(sequence.FirstPosition);
            EACR.BeginCommandGrace();
            ScheduleNextStep(sequence);
            message = $"Started /impregnate with {first.Data.PlayerName}, {second.Data.PlayerName}, and {third.Data.PlayerName}. The sequence lasts 10 seconds.";
            Logger.Info(message, "ImpregnateCommand");
            return true;
        }
        catch (Exception ex)
        {
            Restore(sequence);
            Logger.Exception(ex, "ImpregnateSequenceStart");
            message = "Could not start /impregnate; players were restored where possible.";
            return false;
        }
    }

    public static void CancelActive()
    {
        if (activeSequence == null) return;
        Restore(activeSequence);
    }

    private static void ScheduleNextStep(Sequence sequence)
    {
        _ = new LateTask(() => Advance(sequence), StepSeconds, "ImpregnatePlayerSequence", false);
    }

    private static void Advance(Sequence sequence)
    {
        if (sequence == null || sequence.IsFinished || !ReferenceEquals(activeSequence, sequence)) return;
        try
        {
            if (!AmongUsClient.Instance || !AmongUsClient.Instance.AmHost || !Utils.InGame || Utils.IsMeeting ||
                MeetingHud.Instance != null || !IsUsablePlayer(sequence.First) || !IsUsablePlayer(sequence.Second) ||
                !IsUsablePlayer(sequence.Third))
            {
                Restore(sequence);
                return;
            }

            sequence.Elapsed = Math.Min(DurationSeconds, sequence.Elapsed + StepSeconds);
            if (sequence.Elapsed >= DurationSeconds)
            {
                sequence.Third.moveable = false;
                Vector2 midpoint = (sequence.FirstPosition + sequence.SecondPosition) * 0.5f;
                sequence.Third.NetTransform.RpcSnapTo(midpoint);
                Restore(sequence);
                Logger.Info("Finished /impregnate; all three players were restored to their original movement states.", "ImpregnateCommand");
                return;
            }

            sequence.Second.NetTransform.RpcSnapTo(sequence.FirstPosition);
            bool moveTowardSecondPosition = ((int)(sequence.Elapsed / StepSeconds) & 1) == 0;
            sequence.First.NetTransform.RpcSnapTo(moveTowardSecondPosition ? sequence.SecondPosition : sequence.FirstPosition);
            ScheduleNextStep(sequence);
        }
        catch (Exception ex)
        {
            Restore(sequence);
            Logger.Exception(ex, "ImpregnateSequenceStep");
        }
    }

    private static bool IsUsablePlayer(PlayerControl player)
    {
        return player?.Data != null && !player.Data.Disconnected && !player.Data.IsDead && player.NetTransform != null;
    }

    private static void Restore(Sequence sequence)
    {
        if (sequence == null || sequence.IsFinished) return;
        sequence.IsFinished = true;
        if (ReferenceEquals(activeSequence, sequence)) activeSequence = null;

        try { if (sequence.First?.Data != null) sequence.First.moveable = sequence.FirstWasMoveable; } catch { }
        try { if (sequence.Second?.Data != null) sequence.Second.moveable = sequence.SecondWasMoveable; } catch { }
        try { if (sequence.Third?.Data != null) sequence.Third.moveable = sequence.ThirdWasMoveable; } catch { }
    }
}
