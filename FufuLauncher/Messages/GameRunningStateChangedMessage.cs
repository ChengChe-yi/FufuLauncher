/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
namespace FufuLauncher.Messages
{
    public class GameRunningStateChangedMessage
    {
        public bool IsRunning
        {
            get;
        }

        public GameRunningStateChangedMessage(bool isRunning) => IsRunning = isRunning;
    }
}
