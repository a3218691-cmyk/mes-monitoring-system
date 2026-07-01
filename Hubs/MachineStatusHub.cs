using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace MES.Hubs;

// 目前只需要 server → client 廣播(PlcSimulatorService 透過 IHubContext 推播),
// 不需要 client → server 方法。
[Authorize]
public class MachineStatusHub : Hub
{
}
