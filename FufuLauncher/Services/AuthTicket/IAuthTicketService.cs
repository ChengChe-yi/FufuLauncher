/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
namespace FufuLauncher.Services.AuthTicket;

public interface IAuthTicketService
{
    Task<AuthTicketResult> CreateAuthTicketAsync(string accountId);
}
