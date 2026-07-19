using System.Text;
using Godot;

namespace DwellTargeting;

/// <summary>
/// On-demand discovery pass — logs what the mod can see on the current screen for AI debugging.
/// Triggered by dwelling on the Scan button beside the overlay visibility toggle.
/// During upgrade confirm/preview, only refreshes button lookups — never re-scans background cards.
/// </summary>
internal static class ManualRescanService
{
    internal static void Run()
    {
        bool confirmPhase = PileSelectOverlay.IsInConfirmOnlyPhase()
            || CardConfirmPhaseQuery.IsActive();

        BackButtonOverlay.InvalidateLookup();
        CardConfirmPhaseQuery.InvalidateCache();
        DeckViewOverlay.RefreshToggleLookup();

        if (confirmPhase)
        {
            PileSelectOverlay.RefreshConfirmPhaseLookups();
            ModLogger.Info("[ManualScan] confirm/preview phase — refreshed buttons only, cards stay suppressed.");
        }
        else
        {
            OverlayModeService.InvalidateCache();
            ViewScreenQuery.Invalidate();
            DeckViewOverlay.PrepareForEntry();
            RoomOverlay.PrepareForEntry();
            PileSelectOverlay.ForceRescan();
        }

        var mode = OverlayModeService.GetMode();
        var root = (Engine.GetMainLoop() as SceneTree)?.Root;
        var report = new StringBuilder();
        report.AppendLine($"mode={mode} confirmPhase={confirmPhase} snapshot={OverlayModeService.DebugSnapshot()}");

        if (root != null)
        {
            int holders = CardPickTargetQuery.FindHolders(root).Count;
            bool preview = PilePreviewQuery.HasLargeCenterPreviewCards(root);
            bool confirmBack = PilePreviewQuery.HasVisibleConfirmAndBack(root);
            var confirm = PilePreviewQuery.FindVisibleConfirm(root);
            var back = PilePreviewQuery.FindVisibleBack(root);
            bool confirmActive = CardConfirmPhaseQuery.IsActive();
            bool awaiting = PileSelectOverlay.IsAwaitingConfirm();
            int deckCards = DeckViewOverlay.CachedDeckCardCount;

            report.Append($"holders={holders} preview={preview} confirmBack={confirmBack} ");
            report.Append($"confirmActive={confirmActive} awaitingConfirm={awaiting} deckCards={deckCards} ");
            report.Append($"confirm={(confirm?.Name.ToString() ?? "null")} back={(back?.Name.ToString() ?? "null")} ");

            if (OverlayModeService.TryGetPileSelectScreen(out var pile))
            {
                bool pileVisible = pile is CanvasItem canvas && NodeQuery.IsVisible(canvas);
                report.Append($"pileType={pile.GetType().Name} pileVisible={pileVisible} ");
            }
        }

        ModLogger.Info("[ManualScan] " + report.ToString().Trim());
        ModHealthReporter.FlushNow();
    }
}
