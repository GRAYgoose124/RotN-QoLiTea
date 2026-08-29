using System;
using System.Collections.Generic;
using System.Reflection;
using QoLiTea.Features.ResultsDivergence;
using Shared;
using Shared.MenuOptions;
using Shared.SceneLoading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace QoLiTea.Features.WorstSectionPractice;

internal static class WorstSectionPracticeUi
{
    private const string ButtonName = "TextButton - AutoPractice";
    private const string ButtonLabel = "Auto";

    internal static void EnsureButton(ScoreResultsView view, IReadOnlyList<PracticeSpan> spans)
    {
        if (view?._retryButton == null || spans == null || spans.Count == 0)
            return;

        var stock = view._retryButton;
        var parent = stock.transform.parent;
        var controller = view._menuOptionsInputController;

        var existing = parent.Find(ButtonName);
        if (existing != null)
        {
            var oldOpt = existing.GetComponent<TextButtonOption>();
            if (oldOpt != null && controller != null)
                controller.RemoveOption(oldOpt);
            UnityEngine.Object.Destroy(existing.gameObject);
        }

        var go = UnityEngine.Object.Instantiate(stock.gameObject, parent);
        go.name = ButtonName;
        go.transform.SetSiblingIndex(stock.transform.GetSiblingIndex() + 1);
        var button = go.GetComponent<TextButtonOption>();
        if (button == null)
            return;

        StripLocalizationComponents(go);
        ApplyLabel(button, go);

        ClearSubmitHandlers(button);
        button.OnSubmit += () => OnAutoPracticeSubmitted(view, spans);
        button.gameObject.SetActive(true);

        if (controller != null)
        {
            int insertAt = controller.Options.IndexOf(stock);
            if (insertAt < 0)
                insertAt = -1;
            else
                insertAt += 1;
            controller.TryAddOption(button, insertAt);
        }

        view._horizontalOrVerticalLayoutGroupHelper?.AdjustLayoutGroupSettings();
    }

    internal static void HideButton(ScoreResultsView view)
    {
        if (view?._retryButton == null)
            return;
        var existing = view._retryButton.transform.parent?.Find(ButtonName);
        if (existing == null)
            return;

        var opt = existing.GetComponent<TextButtonOption>();
        if (opt != null && view._menuOptionsInputController != null)
            view._menuOptionsInputController.RemoveOption(opt);

        existing.gameObject.SetActive(false);
        view._horizontalOrVerticalLayoutGroupHelper?.AdjustLayoutGroupSettings();
    }

    private static void StripLocalizationComponents(GameObject go)
    {
        foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb == null)
                continue;
            string typeName = mb.GetType().FullName ?? string.Empty;
            if (typeName.Contains("Localizer", StringComparison.Ordinal))
                UnityEngine.Object.DestroyImmediate(mb);
        }
    }

    private static void ApplyLabel(TextButtonOption button, GameObject go)
    {
        if (button._textLabels != null)
        {
            foreach (var label in button._textLabels)
            {
                if (label != null)
                    label.text = ButtonLabel;
            }
        }

        foreach (var tmp in go.GetComponentsInChildren<TMP_Text>(true))
        {
            if (tmp != null)
                tmp.text = ButtonLabel;
        }

        foreach (var legacy in go.GetComponentsInChildren<Text>(true))
        {
            if (legacy != null)
                legacy.text = ButtonLabel;
        }
    }

    private static void OnAutoPracticeSubmitted(ScoreResultsView view, IReadOnlyList<PracticeSpan> spans)
    {
        if (spans == null || spans.Count == 0)
            return;

        PracticeSectionQueue.SetQueue(spans);
        // One continuous Auto session: stock practice window first→last section;
        // mid-run seeks (clear board + 8-beat warm-up) jump gaps between sections.
        var first = spans[0];
        var last = spans[spans.Count - 1];
        if (!SceneLoadData.ModifyActiveMetaDataPracticeModeStatus(true, first.StartBeat, last.EndBeat))
        {
            Plugin.Logger?.LogWarning("AutoPractice: could not set practice metadata");
            PracticeSectionQueue.Clear();
            return;
        }

        view.HandleRetryOptionSubmitted();
    }

    private static void ClearSubmitHandlers(TextButtonOption button)
    {
        if (button == null)
            return;
        var field = typeof(TextButtonOption).GetField(
            "OnSubmit",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null && typeof(Delegate).IsAssignableFrom(field.FieldType))
            field.SetValue(button, null);
    }
}
