using UnityEngine;

public static class MatchContext
{
    /// Room code (invite or quickmatch)
    public static string RoomCode;

    /// True if this client created the room
    public static bool IsHost;

    /// Convenience nickname
    public static string NickName;

    /// Set true during an intentional exit to login/menu.
    /// Used to prevent matchmaker or any net code from loading the game scene again.
    public static bool ExitingToLogin;

    public static void Clear()
    {
        RoomCode = null;
        IsHost = false;
        NickName = null;
        ExitingToLogin = false;
    }
}
