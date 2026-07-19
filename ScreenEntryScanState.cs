namespace DwellTargeting;

/// <summary>
/// Shared enter/exit scan scheduling — prevents identity-reset loops, empty-cache poison, and retry-forever storms.
/// </summary>
internal struct ScreenEntryScanState
{
    private ulong _entityId;
    private long _layoutReadyAtMs;
    private int _emptyRetryCount;
    private bool _scanPending;
    private bool _resolved;

    internal void OnHide()
    {
        _entityId = 0;
        _layoutReadyAtMs = 0;
        _emptyRetryCount = 0;
        _scanPending = false;
        _resolved = false;
    }

    /// <summary>Call when mode enters or user action requires a fresh snapshot.</summary>
    internal void ScheduleRescan(string logTag)
    {
        _scanPending = true;
        _resolved = false;
        _emptyRetryCount = 0;
        _layoutReadyAtMs = ScanRuntime.NowMs() + ScreenScanTiming.LayoutSettleMs;
        ScanRuntime.Info($"[{logTag}] rescan scheduled in {ScreenScanTiming.LayoutSettleMs}ms.");
    }

    /// <summary>
    /// Returns false when sync can skip (resolved or valid cache). True when a scan should run now.
    /// Sets waitForSettle when still inside the settle window.
    /// </summary>
    internal bool ShouldScanNow(ulong entityId, bool hasValidCache, out bool waitForSettle)
    {
        waitForSettle = false;

        if (_entityId != entityId)
        {
            bool firstBind = _entityId == 0;
            _entityId = entityId;
            _resolved = false;
            if (!firstBind || !_scanPending)
            {
                _scanPending = true;
                _emptyRetryCount = 0;
                _layoutReadyAtMs = ScanRuntime.NowMs() + ScreenScanTiming.LayoutSettleMs;
            }
        }

        if (_resolved && !_scanPending)
            return false;

        if (!_scanPending && hasValidCache)
            return false;

        if (ScanRuntime.NowMs() < _layoutReadyAtMs)
        {
            waitForSettle = true;
            return false;
        }

        return true;
    }

    /// <summary>Call after a scan attempt. Returns true when the scan cycle is complete (cache or give-up).</summary>
    internal bool RegisterScanResult(int resultCount, string logTag)
    {
        if (resultCount > 0)
        {
            _scanPending = false;
            _resolved = true;
            _emptyRetryCount = 0;
            return true;
        }

        if (_emptyRetryCount >= ScreenScanTiming.MaxEmptyRetries)
        {
            ScanRuntime.Warn($"[{logTag}] layout snapshot still empty after retries.");
            _scanPending = false;
            _resolved = true;
            return true;
        }

        _emptyRetryCount++;
        _layoutReadyAtMs = ScanRuntime.NowMs() + ScreenScanTiming.EmptyRetryMs;
        ScanRuntime.Info($"[{logTag}] layout snapshot empty — retry {_emptyRetryCount}/{ScreenScanTiming.MaxEmptyRetries}.");
        return false;
    }
}
