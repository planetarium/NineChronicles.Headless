#nullable disable
using System;
using System.Threading.Tasks;
using MagicOnion.Server.Hubs;
using Nekoyume.Shared.Hubs;

namespace NineChronicles.Headless
{
    public class ActionEvaluationHub : StreamingHubBase<IActionEvaluationHub, IActionEvaluationHubReceiver>, IActionEvaluationHub
    {
        public IGroup AddressGroup;

        public async Task JoinAsync(string addressHex)
        {
            AddressGroup = await Group.AddAsync(addressHex);
        }

        public async Task LeaveAsync()
        {
            if (AddressGroup is null)
            {
                throw new InvalidOperationException();
            }

            await AddressGroup.RemoveAsync(Context);
        }

        public async Task BroadcastRenderAsync(byte[] outputStates)
        {
            Broadcast(AddressGroup).OnRender(outputStates);
            await Task.CompletedTask;
        }

        public async Task BroadcastUnrenderAsync(byte[] outputStates)
        {
            Broadcast(AddressGroup).OnUnrender(outputStates);
            await Task.CompletedTask;
        }

        public async Task BroadcastRenderBlockAsync(byte[] oldTip, byte[] newTip)
        {
            Broadcast(AddressGroup).OnRenderBlock(oldTip, newTip);
            await Task.CompletedTask;
        }
        
        public async Task ReportReorgAsync(byte[] oldTip, byte[] newTip, byte[] branchpoint)
        {
            Broadcast(AddressGroup).OnReorged(oldTip, newTip, branchpoint);
            await Task.CompletedTask;
        }
        
        public async Task ReportReorgEndAsync(byte[] oldTip, byte[] newTip, byte[] branchpoint)
        {
            Broadcast(AddressGroup).OnReorgEnd(oldTip, newTip, branchpoint);
            await Task.CompletedTask;
        }

        public async Task ReportExceptionAsync(int code, string message)
        {
            Broadcast(AddressGroup).OnException(code, message);
            await Task.CompletedTask;
        }

        public async Task PreloadStartAsync()
        {
            Broadcast(AddressGroup).OnPreloadStart();
            await Task.CompletedTask;
        }

        public async Task PreloadEndAsync()
        {
            Broadcast(AddressGroup).OnPreloadEnd();
            await Task.CompletedTask;
        }
    }
}
