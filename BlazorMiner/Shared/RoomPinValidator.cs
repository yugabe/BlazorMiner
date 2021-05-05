using System.Text.RegularExpressions;

namespace BlazorMiner.Shared
{
    public class RoomPinValidator
    {
#pragma warning disable CA1822 // Mark members as static
        public bool IsValidPin(string pin) => pin != null && Regex.IsMatch(pin, @"^\d{4}$");
#pragma warning restore CA1822 // Mark members as static
    }
}