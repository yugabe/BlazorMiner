using System.Text.RegularExpressions;

namespace BlazorMiner.Shared
{
    public class RoomPinValidator
    {
        public bool IsValidPin(string pin) => pin != null && Regex.IsMatch(pin, @"^\d{4}$");
    }
}
