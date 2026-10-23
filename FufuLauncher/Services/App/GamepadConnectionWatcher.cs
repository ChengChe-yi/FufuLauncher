using System.Diagnostics;
using System.Runtime.InteropServices;
using FufuLauncher.Models;
using Windows.Gaming.Input;
using FufuLauncher.Helpers;

namespace FufuLauncher.Services;

public sealed class GamepadConnectionWatcher : IDisposable
{
    private readonly Action _onChanged;

    public GamepadConnectionWatcher(Action onChanged)
    {
        _onChanged = onChanged;
        RawGameController.RawGameControllerAdded += OnRawChanged;
        RawGameController.RawGameControllerRemoved += OnRawChanged;
        Gamepad.GamepadAdded += OnGamepadChanged;
        Gamepad.GamepadRemoved += OnGamepadChanged;
    }

    private void OnRawChanged(object? sender, RawGameController controller) => _onChanged();
    private void OnGamepadChanged(object? sender, Gamepad controller) => _onChanged();

    public static GamepadFamilies ReadConnectedFamilies()
    {
        var families = GamepadFamilies.None;
        try
        {
            for (uint index = 0; index < 4; index++)
            {
                var capabilities = new XInputCapabilities();
                if (XInputGetCapabilities(index, 1, ref capabilities) == 0) families |= GamepadFamilies.Xbox;
            }
        }
        catch (Exception ex)
        {
            DebugLog.Info("GamepadConnectionWatcher", $"[GamepadChoices] XInput enumeration unavailable: {ex.Message}");
        }

        try
        {
            foreach (var controller in RawGameController.RawGameControllers.ToArray())
            {
                try
                {
                    if (GamepadButtonCatalog.IsDualSense(controller.HardwareVendorId, controller.HardwareProductId))
                        families |= GamepadFamilies.DualSense;
                }
                catch (Exception ex)
                {
                    DebugLog.Info("GamepadConnectionWatcher", $"[GamepadChoices] Device disappeared: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            DebugLog.Info("GamepadConnectionWatcher", $"[GamepadChoices] HID enumeration unavailable: {ex.Message}");
        }

        return families;
    }

    public void Dispose()
    {
        RawGameController.RawGameControllerAdded -= OnRawChanged;
        RawGameController.RawGameControllerRemoved -= OnRawChanged;
        Gamepad.GamepadAdded -= OnGamepadChanged;
        Gamepad.GamepadRemoved -= OnGamepadChanged;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputGamepad
    {
        public ushort Buttons;
        public byte LeftTrigger, RightTrigger;
        public short LeftX, LeftY, RightX, RightY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputCapabilities
    {
        public byte Type, SubType;
        public ushort Flags;
        public XInputGamepad Gamepad;
        public ushort LeftMotorSpeed, RightMotorSpeed;
    }

    [DllImport("xinput1_4.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint XInputGetCapabilities(uint index, uint flags, ref XInputCapabilities capabilities);
}