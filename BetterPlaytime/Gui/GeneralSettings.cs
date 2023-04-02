using System;
using System.IO;
using System.Numerics;
using BetterPlaytime.Data;
using CheapLoc;
using ImGuiNET;

namespace BetterPlaytime.Gui;

public class GeneralSettings
{
    private Plugin plugin;
    private PlaytimeTracker playtimeTracker;

    private int _currentNumber;

    public GeneralSettings(Plugin plugin, PlaytimeTracker playtimeTracker)
    {
        this.plugin = plugin;
        this.playtimeTracker = playtimeTracker;

        _currentNumber = plugin.Configuration.BiasValue;
    }

    public void RenderGeneralSettings()
    {
        if (ImGui.BeginTabItem($"General###general-tab"))
        {
            ImGui.Dummy(new Vector2(0,0));

            ImGui.Dummy(new Vector2(0.0f, 5.0f));
            ImGui.Text(Loc.Localize("Config - Header Bias", "Bias-:"));

            var autoSaveEnabled = plugin.Configuration.BiasEnabled;
            if (ImGui.Checkbox($"{Loc.Localize("Config - Enabled", "Enabled")}##biasEnabled", ref autoSaveEnabled))
            {
                plugin.ReloadConfig();
                plugin.Configuration.BiasEnabled = autoSaveEnabled;
                plugin.Configuration.Save();
            }

            ImGui.SliderInt($"{Loc.Localize("Config - Bias", "Bias")}##bias_quantity", ref _currentNumber, 0, 100);
            if (ImGui.IsItemDeactivatedAfterEdit())
            {
                _currentNumber = Math.Clamp(_currentNumber, 0, 100);
                if (_currentNumber != plugin.Configuration.BiasValue)
                {
                    plugin.ReloadConfig();
                    plugin.Configuration.BiasValue = _currentNumber;
                    plugin.Configuration.Save();
                }
            }

            ImGui.EndTabItem();
        }
    }
}