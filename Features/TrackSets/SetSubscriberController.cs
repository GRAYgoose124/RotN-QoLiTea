using System;
using System.Collections.Generic;
using UnityEngine;

namespace QoLiTea.Features.TrackSets;

public static class SetSubscriberController
{
    private static SetSubscriberOverlay _overlay;
    private static bool _busy;

    public static void TryOpenFromSetting()
    {
        if (_busy || (_overlay != null && _overlay.IsOpen))
            return;

        if (!Plugin.IsSetSubscriberActive)
            return;

        _busy = true;
        try
        {
            TrackSetStore.LoadFromDisk();
            List<TrackSetRecord> sets = TrackSetStore.SnapshotSetsNewestFirst();

            if (_overlay == null)
            {
                var go = new GameObject("QoLiTea_SetSubscriberOverlay");
                UnityEngine.Object.DontDestroyOnLoad(go);
                _overlay = go.AddComponent<SetSubscriberOverlay>();
            }

            _overlay.Open(sets, () => { _busy = false; });
        }
        catch (Exception e)
        {
            _busy = false;
            Plugin.Logger?.LogError($"SetSubscriber: failed: {e}");
        }
    }
}
