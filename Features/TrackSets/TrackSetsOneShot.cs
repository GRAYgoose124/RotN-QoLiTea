using System;

namespace QoLiTea.Features.TrackSets;

/// <summary>Watch NecroManager one-shot bools and reset them after firing.</summary>
public static class TrackSetsOneShot
{
    private static bool _bulkPrev;
    private static bool _noImpPrev;
    private static bool _setSubPrev;
    private static bool _primed;

    public static void Tick()
    {
        try
        {
            TickCore();
        }
        catch (InvalidOperationException)
        {
            // Setting not bound yet (or unbound on unload).
        }
    }

    private static void TickCore()
    {
        if (!Plugin.Enabled)
        {
            SyncPrev();
            return;
        }

        bool bulk = Plugin.RunBulkUnsubscriber.Entry.Value;
        bool noImp = Plugin.RunUnsubNoImpossible.Entry.Value;
        bool setSub = Plugin.OpenSetSubscriber.Entry.Value;

        if (!_primed)
        {
            _bulkPrev = bulk;
            _noImpPrev = noImp;
            _setSubPrev = setSub;
            _primed = true;
            // Clear stuck true from last session.
            if (bulk)
                Plugin.RunBulkUnsubscriber.Entry.Value = false;
            if (noImp)
                Plugin.RunUnsubNoImpossible.Entry.Value = false;
            if (setSub)
                Plugin.OpenSetSubscriber.Entry.Value = false;
            SyncPrev();
            return;
        }

        if (bulk && !_bulkPrev && Plugin.IsBulkUnsubscriberActive)
        {
            Plugin.RunBulkUnsubscriber.Entry.Value = false;
            BulkUnsubController.TryRunFromSetting();
        }

        if (noImp && !_noImpPrev && Plugin.IsBulkUnsubscriberActive)
        {
            Plugin.RunUnsubNoImpossible.Entry.Value = false;
            BulkUnsubController.TryRunNoImpossibleFromSetting();
        }

        if (setSub && !_setSubPrev && Plugin.IsSetSubscriberActive)
        {
            Plugin.OpenSetSubscriber.Entry.Value = false;
            SetSubscriberController.TryOpenFromSetting();
        }

        SyncPrev();
    }

    private static void SyncPrev()
    {
        _bulkPrev = Plugin.RunBulkUnsubscriber.Entry.Value;
        _noImpPrev = Plugin.RunUnsubNoImpossible.Entry.Value;
        _setSubPrev = Plugin.OpenSetSubscriber.Entry.Value;
    }
}
