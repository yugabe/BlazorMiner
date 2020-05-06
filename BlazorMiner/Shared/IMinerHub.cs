using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BlazorMiner.Shared
{
    public interface IMinerHub
    {
        Task SendMessageToLobbyAsync(string message);
        Task<EnterRoomResult> EnterRoomAsync(Guid roomId, string pin);
        Task<Room> CreateRoomAsync(string pin, string name);
        Task DeleteRoomAsync(Guid roomId);
        Task<List<Room>> UpdateRoomsAsync();
        Task<string> GetUserIdAsync();
        Task UpdateGameStateAsync(Guid roomId);
        Task StartGameAsync(Guid roomId);
        Task MakeMoveAsync(Guid roomId, int x, int y);
    }
}
