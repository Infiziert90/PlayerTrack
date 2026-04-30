using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;
using PlayerTrack.Enums;
using PlayerTrack.Resource;

namespace PlayerTrack.Windows;

public static class Helper
{
    /// <summary>
    /// An unformatted version for ImGui.TextColored
    /// </summary>
    /// <param name="color">color to be used</param>
    /// <param name="text">text to display</param>
    public static void TextColored(Vector4 color, string text)
    {
        using (ImRaii.PushColor(ImGuiCol.Text, color))
            ImGui.TextUnformatted(text);
    }

    /// <summary>
    /// An unformatted version for ImGui.SetTooltip
    /// </summary>
    /// <param name="tooltip">tooltip to display</param>
    public static void Tooltip(string tooltip)
    {
        using (ImRaii.Tooltip())
        using (ImRaii.TextWrapPos(ImGui.GetFontSize() * 35.0f))
            ImGui.TextUnformatted(tooltip);
    }

    /// <summary>
    /// An unformatted version for ImGui.TextWrapped
    /// </summary>
    /// <param name="text">text to display</param>
    public static void TextWrapped(string text)
    {
        using (ImRaii.TextWrapPos(0.0f))
            ImGui.TextUnformatted(text);
    }

    /// <summary>
    /// An unformatted version for ImGui.TextWrapped with color
    /// </summary>
    /// <param name="color">color to be used</param>
    /// <param name="text">text to display</param>
    public static void WrappedTextWithColor(Vector4 color, string text)
    {
        using (ImRaii.PushColor(ImGuiCol.Text, color))
            TextWrapped(text);
    }

    /// <summary>
    /// An unformatted version for ImGui.BulletText
    /// </summary>
    /// <param name="text">text to display</param>
    public static void BulletText(string text)
    {
        ImGui.Bullet();
        ImGui.SameLine();
        ImGui.TextUnformatted(text);
    }

    public static void CenterText(string text, float indent = 0.0f)
    {
        indent *= ImGuiHelpers.GlobalScale;
        ImGui.SameLine(((ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize(text).X) * 0.5f) + indent);
        ImGui.TextUnformatted(text);
    }

    /// <summary>
    /// Checkbox with better label and loopable key.
    /// </summary>
    /// <param name="key">localization key.</param>
    /// <param name="boolValue">local value reference.</param>
    /// <returns>Indicator if changed.</returns>
    public static bool Checkbox(string key, ref bool boolValue)
    {
        var result = ImGui.Checkbox($"###{key}_Checkbox", ref boolValue);
        ImGui.SameLine();
        ImGui.TextUnformatted(key);

        return result;
    }

    /// <summary>
    /// Checkbox with better label and loopable key.
    /// </summary>
    /// <param name="key">localization key.</param>
    /// <param name="suffix">suffix for key.</param>
    /// <param name="boolValue">local value reference.</param>
    /// <param name="useLabel">use localized label.</param>
    /// <returns>Indicator if changed.</returns>
    public static bool Checkbox(string key, string suffix, ref bool boolValue, bool useLabel = true)
    {
        var result = ImGui.Checkbox($"###{key}_{suffix}_Checkbox", ref boolValue);

        if (!useLabel)
            return result;

        ImGui.SameLine();
        ImGui.TextUnformatted(key);

        return result;
    }

    /// <summary>
    /// Return scaled width for combo boxes.
    /// </summary>
    /// <param name="max">max width.</param>
    /// <returns>width.</returns>
    public static float CalcScaledComboWidth(float max)
    {
        var maxWidth = max * ImGuiHelpers.GlobalScale;
        var relativeWidth = ImGui.GetWindowSize().X / 2;
        return relativeWidth < maxWidth ? relativeWidth : maxWidth;
    }

    /// <summary>
    /// Styled ComboBox with derived and localized options.
    /// </summary>
    /// <typeparam name="T">Enum for combobox list.</typeparam>
    /// <param name="key">primary key.</param>
    /// <param name="value">current selected index value.</param>
    /// <param name="comboWidth">width (default to fill).</param>
    /// <param name="padding">add scaled dummy to top for padding.</param>
    /// <param name="includeLabel">add label.</param>
    /// <returns>indicates if combo value is changed.</returns>
    public static bool Combo<T>(string key, ref T value, int comboWidth = 100, bool padding = true, bool includeLabel = true) where T : IConvertible
    {
        var isChanged = false;
        var options = Enum.GetNames(typeof(T));
        var localizedOptions = options.Select(Utils.GetLoc).ToList();

        if (padding)
            ImGuiHelpers.ScaledDummy(1f);

        var label = includeLabel ? key : $"###{key}_Combo";
        ImGui.SetNextItemWidth(comboWidth == -1 ? comboWidth : CalcScaledComboWidth(comboWidth));
        var val = Convert.ToInt32(value);
        if (ImGui.Combo(label, ref val, localizedOptions.ToArray()))
        {
            value = (T)(object)val;
            isChanged = true;
        }

        return isChanged;
    }

    /// <summary>
    /// Styled and localized ComboBox with loopable key.
    /// </summary>
    /// <param name="key">primary key.</param>
    /// <param name="suffix">suffix for key.</param>
    /// <param name="value">current selected index value.</param>
    /// <param name="options">keys for options.</param>
    /// <param name="comboWidth">width (default to fill).</param>
    /// <returns>indicates if combo box value was changed.</returns>
    public static bool Combo(string key, string suffix, ref int value, IEnumerable<string> options, int comboWidth = 100)
    {
        var isChanged = false;
        var localizedOptions = options.Select(Utils.GetLoc).ToList();

        ImGuiHelpers.ScaledDummy(1f);
        ImGui.SetNextItemWidth(CalcScaledComboWidth(comboWidth));
        if (ImGui.Combo($"{key}###{suffix}_Combo", ref value, localizedOptions.ToArray()))
            isChanged = true;

        return isChanged;
    }

    /// <summary>
    /// Localized Action Prompt for Delete/Restore.
    /// </summary>
    /// <typeparam name="T">Item type for action to be performed upon (e.g. delete, restore).</typeparam>
    /// <param name="item">current item being evaluated.</param>
    /// <param name="messageKey">confirmation message key to display.</param>
    /// <param name="request">tuple with action state and instance of item under review.</param>
    public static void Confirm<T>(T item, string messageKey, ref Tuple<ActionRequest, T>? request)
    {
        if (request == null)
            return;

        dynamic newItem = item!;
        dynamic savedItem = request.Item2!;
        if (newItem.Id != savedItem.Id)
            return;

        ImGui.SameLine();
        TextColored(ImGuiColors.DalamudYellow, messageKey);
        ImGui.SameLine();
        if (ImGui.SmallButton(Language.Cancel))
            request = new Tuple<ActionRequest, T>(ActionRequest.None, request.Item2);

        ImGui.SameLine();
        if (ImGui.SmallButton(Language.OK))
            request = new Tuple<ActionRequest, T>(ActionRequest.Confirmed, request.Item2);

        ImGui.SetCursorPosY(ImGui.GetCursorPosY() - (5.0f * ImGuiHelpers.GlobalScale));
    }

    /// <summary>
    /// Localized Action Prompt for Delete/Restore.
    /// </summary>
    /// <typeparam name="T">Item type for action to be performed upon (e.g. delete, restore).</typeparam>
    /// <param name="item">current item being evaluated.</param>
    /// <param name="icon">icon char code.</param>
    /// <param name="messageKey">confirmation message key to display.</param>
    /// <param name="request">tuple with action state and instance of item under review.</param>
    public static void Confirm<T>(T item, FontAwesomeIcon icon, string messageKey, ref Tuple<ActionRequest, T>? request)
    {
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            ImGui.TextUnformatted(icon.ToIconString());
            if (ImGui.IsItemClicked())
                request = new Tuple<ActionRequest, T>(ActionRequest.Pending, item);
        }

        if (request != null)
        {
            dynamic newItem = item!;
            dynamic savedItem = request.Item2!;
            if (newItem.Id == savedItem.Id)
            {
                ImGui.SameLine();
                TextColored(ImGuiColors.DalamudYellow, messageKey);
                ImGui.SameLine();
                if (ImGui.SmallButton(Language.Cancel))
                    request = new Tuple<ActionRequest, T>(ActionRequest.None, request.Item2);

                ImGui.SameLine();
                if (ImGui.SmallButton(Language.OK))
                    request = new Tuple<ActionRequest, T>(ActionRequest.Confirmed, request.Item2);
            }
        }
    }

    /// <summary>
    /// Styled and localized ComboBox.
    /// </summary>
    /// <param name="key">primary key.</param>
    /// <param name="value">current selected index value.</param>
    /// <param name="options">keys for options.</param>
    /// <param name="comboWidth">width (default to fill).</param>
    /// <param name="padding">add scaled dummy to top for padding.</param>
    /// <param name="includeLabel">add label.</param>
    /// <returns>indicates if combo box value was changed.</returns>
    public static bool Combo(string key, ref int value, IEnumerable<string> options, int comboWidth = 100, bool padding = true, bool includeLabel = true)
    {
        var isChanged = false;
        var localizedOptions = options.Select(Utils.GetLoc).ToList();

        if (padding)
            ImGuiHelpers.ScaledDummy(1f);

        var label = includeLabel ? key : $"###{key}";
        ImGui.SetNextItemWidth(comboWidth == -1 ? comboWidth : CalcScaledComboWidth(comboWidth));
        if (ImGui.Combo(label, ref value, localizedOptions.ToArray()))
            isChanged = true;

        return isChanged;
    }

    /// <summary>
    /// Styled and localized ComboBox with suffix.
    /// </summary>
    /// <typeparam name="T">Enum for combobox list.</typeparam>
    /// <param name="key">primary key.</param>
    /// <param name="value">current selected index value.</param>
    /// <param name="suffix">suffix for key.</param>
    /// <returns>indicates if combo value is changed.</returns>
    public static bool Combo<T>(string key, ref T value, string suffix) where T : Enum
    {
        var isChanged = false;
        var localizedOptions = Enum.GetNames(typeof(T)).Select(Utils.GetLoc).ToList();

        var currentValueIndex = Convert.ToInt32(value);
        var activeValueDisplay = $"{localizedOptions[currentValueIndex]} {suffix}";

        ImGui.SetNextItemWidth(-1);
        using var combo = ImRaii.Combo($"###{key}_Combo", activeValueDisplay);
        if (!combo.Success)
            return isChanged;

        for (var i = 0; i < localizedOptions.Count; i++)
        {
            var isSelected = i == currentValueIndex;
            if (ImGui.Selectable(localizedOptions[i], isSelected))
            {
                value = (T)(object)i;
                isChanged = true;
            }

            if (isSelected)
                ImGui.SetItemDefaultFocus();
        }

        return isChanged;
    }

    /// <summary>
    /// Color picker with popup.
    /// </summary>
    /// <param name="key">primary key.</param>
    /// <param name="colorId">color id.</param>
    /// <param name="color">current color.</param>
    /// <param name="includeLabel">indicator whether to include label.</param>
    /// <returns>indicator whether color changed.</returns>
    public static bool SimpleUiColorPicker(string key, uint colorId, ref Vector4 color, bool includeLabel = true)
    {
        var id = $"###{key}";
        var originalColor = color;
        var itemsPerRow = (int)Math.Sqrt(Sheets.UiColor.Count);
        var currentItemCount = 0;
        var addedColors = new HashSet<Vector4>();

        if (ImGui.ColorButton($"{id}_UIColorButton", color, ImGuiColorEditFlags.NoTooltip))
            ImGui.OpenPopup($"{id}_UIColorButton_Popup");

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip($"UIColor #{colorId}");

        if (ImGui.BeginPopup($"{id}_UIColorButton_Popup"))
        {
            var sortedColors = Sheets.UiColor
                                     .OrderBy(pair => Utils.ColorToHue(Sheets.GetUiColorAsVector4(pair.Value.Id)))
                                     .ToList();

            foreach (var uiColorPair in sortedColors)
            {
                var buttonColor = Sheets.GetUiColorAsVector4(uiColorPair.Value.Id);
                if (addedColors.Any(existing => Utils.AreColorsSimilar(existing, buttonColor)))
                    continue;

                if (ImGui.ColorButton($"{id}_UIColorButton_{uiColorPair.Value.Id}", buttonColor))
                {
                    color = buttonColor;
                    ImGui.CloseCurrentPopup();
                }

                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip($"UIColor #{uiColorPair.Value.Id}");

                currentItemCount++;
                if (currentItemCount % itemsPerRow != 0)
                    ImGui.SameLine();

                addedColors.Add(buttonColor);
            }

            ImGui.EndPopup();
        }

        if (includeLabel)
        {
            ImGui.SameLine();
            ImGui.TextUnformatted(key);
        }

        return color != originalColor;
    }

    /// <summary>
    /// Simple FontAwesomeIcon IconPicker.
    /// </summary>
    /// <param name="key">primary key.</param>
    /// <param name="icon">icon char code.</param>
    /// <param name="iconCodes">keys for options.</param>
    /// <param name="iconNames">add scaled dummy to top for padding.</param>
    /// <param name="includeLabel">indicator whether to include label.</param>
    /// <returns>indicator whether icon changed.</returns>
    /// <summary>
    /// Renders a combo picker for selecting icons.
    /// </summary>
    public static bool IconPicker(string key, ref char icon, char[] iconCodes, string[] iconNames, bool includeLabel = true)
    {
        var isChanged = false;
        var tempIcon = icon;
        var sortedIconNames = (string[])iconNames.Clone();
        var sortedIconCodes = (char[])iconCodes.Clone();

        Array.Sort(sortedIconNames, sortedIconCodes);

        var iconIndex = Array.FindIndex(sortedIconCodes, c => c == tempIcon);
        if (iconIndex == -1)
            iconIndex = 0;

        var currentIcon = ((FontAwesomeIcon)sortedIconCodes[iconIndex]).ToIconString();
        var currentIconName = sortedIconNames[iconIndex];

        if (ImGui.Button(key))
            ImGui.OpenPopup($"##ComboPopup{key}");

        if (ImGui.BeginPopup($"##ComboPopup{key}"))
        {
            var childHeight = 5 * 20.0f * ImGuiHelpers.GlobalScale;
            var childWidth = 200.0f * ImGuiHelpers.GlobalScale;
            using (var child = ImRaii.Child("##IconChild", new Vector2(childWidth, childHeight), true))
            {
                if (child.Success)
                {
                    for (var i = 0; i < sortedIconNames.Length; i++)
                    {
                        var isSelected = iconIndex == i;
                        var spacing = 30.0f * ImGuiHelpers.GlobalScale;

                        ImGui.PushFont(UiBuilder.IconFont);
                        ImGui.TextUnformatted(((FontAwesomeIcon)sortedIconCodes[i]).ToIconString());
                        ImGui.PopFont();

                        ImGui.SameLine(ImGui.GetCursorPosX() + spacing);
                        if (ImGui.Selectable($"{sortedIconNames[i]}##{i}", isSelected))
                        {
                            iconIndex = i;
                            icon = sortedIconCodes[iconIndex];
                            isChanged = true;
                            ImGui.CloseCurrentPopup();
                        }

                        if (isSelected)
                            ImGui.SetItemDefaultFocus();
                    }
                }
            }

            ImGui.EndPopup();
        }

        ImGui.SameLine();
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            ImGui.TextUnformatted(currentIcon);
        }

        if (includeLabel)
        {
            ImGui.SameLine();
            ImGui.TextUnformatted(currentIconName);
        }

        return isChanged;
    }

    public struct EndUnconditionally(Action endAction, bool success) : IDisposable
    {
        public bool Success { get; } = success;
        private bool Disposed { get; set; } = false;
        private Action EndAction { get; } = endAction;
        public void Dispose()
        {
            if (!Disposed)
            {
                EndAction();
                Disposed = true;
            }
        }
    }

    // Use end-function only on success.
    public struct EndConditionally(Action endAction, bool success) : IDisposable
    {
        public bool Success { get; } = success;
        private bool Disposed { get; set; } = false;
        private Action EndAction { get; } = endAction;
        public void Dispose()
        {
            if (Disposed)
                return;
            if (Success)
                EndAction();
            Disposed = true;
        }
    }

    public static EndConditionally Menu(string label)
    {
        return new EndConditionally(ImGui.EndMenu, ImGui.BeginMenu(label));
    }

    // Used to avoid pops if condition is false for Push.
    private static void Nop() { }
}

/// <summary>
/// Filterable combo box.
/// </summary>
public class FilterComboBox
{
    private readonly IReadOnlyList<string> Items;
    private readonly string FilterTextHint;
    private readonly string NoMatchFoundText;
    private List<string> FilteredItems;
    private string Filter;
    private string SelectedItem;
    private bool ComboOpened;

    /// <summary>
    /// Initializes a new instance of the <see cref="FilterComboBox"/> class.
    /// </summary>
    /// <param name="items">Items to display.</param>
    /// <param name="filterTextHint">Filter text hint.</param>
    /// <param name="noMatchFoundText">No match found text.</param>
    public FilterComboBox(IReadOnlyList<string> items, string filterTextHint, string noMatchFoundText)
    {
        FilterTextHint = filterTextHint;
        NoMatchFoundText = noMatchFoundText;
        Items = items;
        FilteredItems = items.ToList();
        SelectedItem = string.Empty;
        Filter = string.Empty;
    }

    /// <summary>
    /// Draw the combo box.
    /// </summary>
    /// <param name="label">Label.</param>
    /// <param name="width">Width.</param>
    /// <param name="itemHeight">Item height.</param>
    /// <param name="flags">Flags.</param>
    /// <returns>Selected index.</returns>
    public int? Draw(string label, float width = 100f, float itemHeight = 30f, ImGuiComboFlags flags = ImGuiComboFlags.None)
    {

        var previewValue = !string.IsNullOrEmpty(SelectedItem) ? SelectedItem : string.Empty;
        int? selectedIndex = null;

        var localizedLabel = label;
        ImGui.SetNextItemWidth(width);
        using (var combo = ImRaii.Combo(localizedLabel, previewValue, flags | ImGuiComboFlags.HeightLargest))
        {
            if (combo.Success)
            {
                if (!ComboOpened)
                    ImGui.SetKeyboardFocusHere();

                ComboOpened = true;
                DrawFilter();
                ImGui.Separator();
                selectedIndex = DrawItems(itemHeight);
                if (selectedIndex.HasValue)
                {
                    SelectedItem = FilteredItems[selectedIndex.Value];
                    ImGui.CloseCurrentPopup();
                }
            }
            else if (ComboOpened)
            {
                ComboOpened = false;
                Filter = string.Empty;
                UpdateFilter();
            }
        }

        return selectedIndex.HasValue ? Items.ToList().FindIndex(item => item == SelectedItem) : null;
    }

    private void DrawFilter()
    {
        using (ImRaii.ItemWidth(ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X))
        {
            if (ImGui.InputTextWithHint("###FilterText", FilterTextHint, ref Filter, 256))
                UpdateFilter();
        }
    }

    private int? DrawItems(float itemHeight)
    {
        using var pushedStyle = ImRaii.PushStyle(ImGuiStyleVar.ChildRounding, 0);

        var totalItemsHeight = FilteredItems.Count * itemHeight;
        var maxHeight = 10 * itemHeight;
        var minHeight = itemHeight * 1.2f;
        var childHeight = Math.Max(totalItemsHeight, minHeight);
        childHeight = Math.Min(childHeight, maxHeight);

        using var child = ImRaii.Child("##ItemList", ImGuiHelpers.ScaledVector2(0, childHeight), true, ImGuiWindowFlags.AlwaysVerticalScrollbar);
        if (!child.Success)
            return null;

        int? selectedIndex = null;
        if (FilteredItems.Count == 0)
        {
            ImGui.TextUnformatted(NoMatchFoundText);
        }
        else
        {
            using var clipper = new ListClipper(FilteredItems.Count);
            foreach (var i in clipper.Rows)
            {
                var value = FilteredItems[i];
                var isSelected = value == SelectedItem;
                if (ImGui.Selectable(value, isSelected))
                    selectedIndex = i;

                if (isSelected)
                    ImGui.SetItemDefaultFocus();
            }
        }

        return selectedIndex;
    }

    private void UpdateFilter()
    {
        FilteredItems = string.IsNullOrEmpty(Filter)
            ? Items.ToList()
            : Items.Where(item => item.Contains(Filter, StringComparison.CurrentCultureIgnoreCase)).ToList();

        if (!FilteredItems.Contains(SelectedItem))
            SelectedItem = string.Empty;
    }
}

/// <summary>
/// A wrapper around ImGui.ListClipper with `using` support
/// </summary>
public unsafe class ListClipper : IEnumerable<(int, int)>, IDisposable
{
    private ImGuiListClipperPtr Clipper;
    private readonly int CurrentRows;
    private readonly int CurrentColumns;
    private readonly bool TwoDimensional;
    private readonly int ItemRemainder;

    public int FirstRow { get; private set; } = -1;
    public int CurrentRow { get; private set; }

    public bool Step() => Clipper.Step();
    public int DisplayStart => Clipper.DisplayStart;
    public int DisplayEnd => Clipper.DisplayEnd;

    public IEnumerable<int> Rows
    {
        get
        {
            while (Clipper.Step()) // Supposedly this calls End()
            {
                if (Clipper.ItemsHeight > 0 && FirstRow < 0)
                    FirstRow = (int)(ImGui.GetScrollY() / Clipper.ItemsHeight);

                for (var i = Clipper.DisplayStart; i < Clipper.DisplayEnd; i++)
                {
                    CurrentRow = i;
                    yield return TwoDimensional ? i : i * CurrentColumns;
                }
            }
        }
    }

    public IEnumerable<int> Columns
    {
        get
        {
            var cols = (ItemRemainder == 0 || CurrentRows != DisplayEnd || CurrentRow != DisplayEnd - 1) ? CurrentColumns : ItemRemainder;
            for (var j = 0; j < cols; j++)
                yield return j;
        }
    }

    public ListClipper(int items, int cols = 1, bool twoD = false, float itemHeight = 0)
    {
        TwoDimensional = twoD;
        CurrentColumns = cols;
        CurrentRows = TwoDimensional ? items : (int)MathF.Ceiling((float)items / CurrentColumns);
        ItemRemainder = !TwoDimensional ? items % CurrentColumns : 0;
        Clipper = new ImGuiListClipperPtr(ImGuiNative.ImGuiListClipper());
        Clipper.Begin(CurrentRows, itemHeight);
    }

    public IEnumerator<(int, int)> GetEnumerator() => (from i in Rows from j in Columns select (i, j)).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void Dispose()
    {
        Clipper.Destroy(); // This also calls End() but I'm calling it anyway just in case
        GC.SuppressFinalize(this);
    }
}
