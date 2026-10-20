namespace FufuLauncher.Messages
{
    public class MemoryReleaseEnabledChangedMessage
    {
        public bool IsEnabled
        {
            get;
        }

        public MemoryReleaseEnabledChangedMessage(bool isEnabled) => IsEnabled = isEnabled;
    }
}