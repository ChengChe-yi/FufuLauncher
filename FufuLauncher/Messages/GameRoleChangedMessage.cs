/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
namespace FufuLauncher.Messages;

public sealed record GameRoleChangedMessage(string AccountId, string Uid, string Region);
public sealed record GameRolesUpdatedMessage(string AccountId);
public sealed record FeatureGameRoleChangedMessage(FufuLauncher.Services.GameRoleScope Scope);
