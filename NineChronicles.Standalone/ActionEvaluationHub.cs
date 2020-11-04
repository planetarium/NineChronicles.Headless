using System.Threading.Tasks;
using MagicOnion.Server.Hubs;
using Nekoyume.Shared.Hubs;

namespace NineChronicles.Standalone
{
    public class ActionEvaluationHub : StreamingHubBase<IActionEvaluationHub, IActionEvaluationHubReceiver>, IActionEvaluationHub
    {
        private IGroup group;

        public async Task JoinAsync()
        {
            group = await Group.AddAsync(string.Empty);
        }

        public async Task LeaveAsync()
        {
            await group.RemoveAsync(Context);
        }

        public async Task BroadcastRenderAsync(byte[] outputStates)
        {
            Broadcast(group).OnRender(outputStates);
            await Task.CompletedTask;
        }

        public async Task BroadcastUnrenderAsync(byte[] outputStates)
        {
            Broadcast(group).OnUnrender(outputStates);
            await Task.CompletedTask;
        }

        public async Task BroadcastRenderBlockAsync(byte[] oldTip, byte[] newTip)
        {
            Broadcast(group).OnRenderBlock(oldTip, newTip);
            await Task.CompletedTask;
        }
        
        public async Task ReportReorgAsync(byte[] oldTip, byte[] newTip, byte[] branchpoint)
        {
            Broadcast(group).OnReorged(oldTip, newTip, branchpoint);
            await Task.CompletedTask;
        }
        
        public async Task ReportReorgEndAsync(byte[] oldTip, byte[] newTip, byte[] branchpoint)
        {
            Broadcast(group).OnReorgEnd(oldTip, newTip, branchpoint);
            await Task.CompletedTask;
        }

        public async Task ReportExceptionAsync(int code, string message)
        {
            Broadcast(group).OnException(code, message);
            await Task.CompletedTask;
        }
    }
}
