namespace BlazorMiner.Shared
{
    public enum EnterRoomResult
    {
        InvalidRoomId = 1,
        InvalidPin = 2,
        Ok = 3,
        GameAlreadyInProgress = 4,
        UserAlreadyInRoom = 5
    }
}