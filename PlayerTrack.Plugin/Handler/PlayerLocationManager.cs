using System.Collections.Generic;
using PlayerTrack.Data;

namespace PlayerTrack.Handler;

/// <summary>
/// Manages player locations based on territory changes and game data.
/// Provides events for location start and end points.
/// </summary>
public class PlayerLocationManager
{
    private ushort CurrentTerritoryType;

    public delegate void LocationDelegate(LocationData toadLocation);

    public event LocationDelegate? OnLocationStarted;
    public event LocationDelegate? OnLocationEnded;

    /// <summary>
    /// Starts the location manager and begins processing territory changes.
    /// </summary>
    public void Start()
    {
        Plugin.ClientStateHandler.TerritoryChanged += ProcessTerritoryChange;
        Plugin.ClientStateHandler.Logout += OnLogout;
        if (Plugin.ClientStateHandler.IsLoggedIn)
            ProcessTerritoryChange((ushort)Plugin.ClientStateHandler.TerritoryType);
    }

    /// <summary>
    /// Retrieves the current location based on the territory type.
    /// </summary>
    /// <returns>The current location as a <see cref="LocationData" />.</returns>
    public LocationData? GetCurrentLocation() =>
        Sheets.Locations.GetValueOrDefault(CurrentTerritoryType);

    /// <summary>
    /// Disposes the location manager and stops processing territory changes.
    /// </summary>
    public void Dispose()
    {
        Plugin.ClientStateHandler.TerritoryChanged -= ProcessTerritoryChange;
        Plugin.ClientStateHandler.Logout -= OnLogout;
        OnLocationStarted = null;
        OnLocationEnded = null;
    }



    private void ProcessTerritoryChange(uint newTerritoryType)
    {
        if (CurrentTerritoryType != 0)
            OnLocationEnded?.Invoke(Sheets.Locations[CurrentTerritoryType]);

        if (newTerritoryType != 0)
            OnLocationStarted?.Invoke(Sheets.Locations[(ushort)newTerritoryType]);

        CurrentTerritoryType = (ushort)newTerritoryType;
    }

    private void OnLogout(int type, int code)
    {
        OnLocationEnded?.Invoke(Sheets.Locations[CurrentTerritoryType]);
        ProcessTerritoryChange(0);
    }
}
