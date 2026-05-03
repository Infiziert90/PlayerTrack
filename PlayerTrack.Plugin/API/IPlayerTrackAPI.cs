// ReSharper disable InconsistentNaming
namespace PlayerTrack.API;

/// <summary>
/// Interface to communicate with PlayerTrack.
/// </summary>
public interface IPlayerTrackAPI
{
    /// <summary>
    /// Gets api version.
    /// </summary>
    public int APIVersion { get; }

    /// <summary>
    /// Gets the player's current name and world.
    /// </summary>
    /// <param name="name">full player name at point in time.</param>
    /// <param name="worldId">player home world id at point in time.</param>
    /// <returns>string in the form of (name worldId).</returns>
    public string GetPlayerCurrentNameWorld(string name, uint worldId);

    /// <summary>
    /// Get notes for player.
    /// </summary>
    /// <param name="name">player's full name.</param>
    /// <param name="worldId">player home world id.</param>
    /// <returns>notes.</returns>
    public string GetPlayerNotes(string name, uint worldId);

    /// <summary>
    /// Retrieves all player names/world history records.
    /// </summary>
    /// <returns>tuple array of current (player name, world id) and an array of all previous (player name, world id) combos.</returns>
    public ((string, uint), (string, uint)[])[] GetAllPlayerNameWorldHistories();

    /// <summary>
    /// Assigns a category to a player identified by name and home world, updating both the
    /// database and the in-memory cache immediately so the change is reflected in the
    /// PlayerTrack UI without requiring a plugin restart.
    /// </summary>
    /// <param name="name">Player's full name.</param>
    /// <param name="worldId">Player's home world id.</param>
    /// <param name="categoryId">Id of the category to assign.</param>
    /// <returns>
    /// <c>true</c> if the category was assigned (or was already assigned);
    /// <c>false</c> if the player was not found in PlayerTrack's database or the
    /// category id is invalid.
    /// </returns>
    public bool AssignCategory(string name, uint worldId, uint categoryId);
}
