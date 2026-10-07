/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using CommunityToolkit.Mvvm.Messaging.Messages;

namespace FufuLauncher.Messages;

public class ConstraintStateChangedMessage : ValueChangedMessage<bool>
{
    public ConstraintStateChangedMessage(bool isRestricted) : base(isRestricted)
    {
    }
}