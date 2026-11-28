using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Fasx.Agent.Core.Flows;
using Fasx.Agent.Windows.Flows;

namespace Fasx.Agent.Windows;

public partial class FasxFlowsWindow : Window
{
    private readonly FasxFlowStore _store = new();
    private readonly List<FasxFlowStep> _draftSteps = [];
    private List<FasxFlow> _savedFlows = [];

    public FasxFlowsWindow()
    {
        InitializeComponent();
        _savedFlows = _store.Load().ToList();
        RefreshSavedFlows();
        Loaded += (_, _) => ActionType_SelectionChanged(null, null!);
    }

    private void ActionType_SelectionChanged(object? sender, SelectionChangedEventArgs? e)
    {
        if (ApplicationPickerPanel is null) return;

        var action = (ActionTypeComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        ApplicationPickerPanel.Visibility = action == nameof(FasxFlowAction.OpenBuiltInApp)
            ? Visibility.Visible : Visibility.Collapsed;
        WebsitePickerPanel.Visibility = action == nameof(FasxFlowAction.OpenHttpsWebsite)
            ? Visibility.Visible : Visibility.Collapsed;
        FolderActionHint.Visibility = action == nameof(FasxFlowAction.PickAndOpenFolder)
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void AddStep_Click(object sender, RoutedEventArgs e)
    {
        if (_draftSteps.Count >= FasxFlowPolicy.MaximumStepsPerFlow)
        {
            MessageBox.Show(this, $"A flow can contain at most {FasxFlowPolicy.MaximumStepsPerFlow} steps.",
                "FASX Flows", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var actionName = (ActionTypeComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        FasxFlowStep? step = actionName switch
        {
            nameof(FasxFlowAction.OpenBuiltInApp) =>
                new FasxFlowStep(
                    FasxFlowAction.OpenBuiltInApp,
                    (BuiltInAppComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString()),

            nameof(FasxFlowAction.OpenHttpsWebsite) =>
                new FasxFlowStep(FasxFlowAction.OpenHttpsWebsite, WebsiteTextBox.Text.Trim()),

            nameof(FasxFlowAction.PickAndOpenFolder) =>
                new FasxFlowStep(FasxFlowAction.PickAndOpenFolder),

            _ => null
        };

        if (step is null || !FasxFlowPolicy.IsValid(
            new FasxFlow(Guid.NewGuid(), "Draft", new[] { step })))
        {
            MessageBox.Show(this, "The selected action or its value is invalid. Use a registered app or an HTTPS website.",
                "FASX Flows", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _draftSteps.Add(step);
        RefreshDraftSteps();
    }

    private void RemoveStep_Click(object sender, RoutedEventArgs e)
    {
        var index = DraftStepsList.SelectedIndex;
        if (index < 0 || index >= _draftSteps.Count) return;
        _draftSteps.RemoveAt(index);
        RefreshDraftSteps();
    }

    private void SaveFlow_Click(object sender, RoutedEventArgs e)
    {
        var name = FlowNameTextBox.Text.Trim();
        if (_savedFlows.Count >= FasxFlowPolicy.MaximumFlows)
        {
            MessageBox.Show(this, $"FASX stores up to {FasxFlowPolicy.MaximumFlows} local flows.",
                "FASX Flows", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (_savedFlows.Any(flow => flow.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show(this, "A flow with that name already exists.", "FASX Flows",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var flow = new FasxFlow(Guid.NewGuid(), name, _draftSteps.ToArray());
        if (!FasxFlowPolicy.IsValid(flow))
        {
            MessageBox.Show(this, "Enter a name and add between 1 and 8 valid steps before saving.",
                "FASX Flows", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _savedFlows.Add(flow);
        try
        {
            _store.Save(_savedFlows);
            _draftSteps.Clear();
            FlowNameTextBox.Clear();
            RefreshDraftSteps();
            RefreshSavedFlows();
            MessageBox.Show(this, "The flow was saved on this Windows user account.",
                "FASX Flows", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            _savedFlows.Remove(flow);
            MessageBox.Show(this, "FASX could not save the flow.\n\n" + exception.Message,
                "FASX Flows", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RunFlow_Click(object sender, RoutedEventArgs e)
    {
        if (SavedFlowsList.SelectedItem is not FasxFlow flow)
        {
            MessageBox.Show(this, "Select a saved flow first.", "FASX Flows",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        LocalFlowExecutor.Run(flow, this);
    }

    private void DeleteFlow_Click(object sender, RoutedEventArgs e)
    {
        if (SavedFlowsList.SelectedItem is not FasxFlow flow) return;

        var result = MessageBox.Show(
            this,
            $"Delete the saved flow “{flow.Name}”?",
            "Delete FASX Flow",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;

        var remaining = _savedFlows.Where(item => item.Id != flow.Id).ToList();
        try
        {
            _store.Save(remaining);
            _savedFlows = remaining;
            RefreshSavedFlows();
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, "FASX could not update the local flow store.\n\n" + exception.Message,
                "FASX Flows", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RefreshDraftSteps()
    {
        DraftStepsList.Items.Clear();
        for (var index = 0; index < _draftSteps.Count; index++)
        {
            var step = _draftSteps[index];
            var description = step.Action switch
            {
                FasxFlowAction.OpenBuiltInApp => "Open " + step.Value,
                FasxFlowAction.OpenHttpsWebsite => "Open " + step.Value,
                FasxFlowAction.PickAndOpenFolder => "Choose and open a folder",
                _ => "Unsupported action"
            };
            DraftStepsList.Items.Add($"{index + 1}. {description}");
        }
    }

    private void RefreshSavedFlows()
    {
        SavedFlowsList.ItemsSource = null;
        SavedFlowsList.ItemsSource = _savedFlows;
    }
}
