using System.Text.Json;

namespace ManagerEmployee.Application.Employees.Commands.SyncEmployee;

public sealed record SyncEmployeeCommand(string Provider, JsonElement Data);
