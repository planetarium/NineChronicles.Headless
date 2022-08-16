#nullable disable
using System;
using System.Threading.Tasks;
using MagicOnion.Server.Hubs;
using Nekoyume.Shared.Hubs;

namespace NineChronicles.Headless
{
    public class ActionEvaluationHub : StreamingHubBase<IActionEvaluationHub, IActionEvaluationHubReceiver>, IActionEvaluationHub
    {
        private IGroup _addressGroup;
        public static event Action<string> OnClientDisconnected;


        public async Task JoinAsync(string addressHex)
        {
            _addressGroup = await Group.AddAsync(addressHex);
        }

        public async Task LeaveAsync()
        {
            if (_addressGroup is null)
            {
                throw new InvalidOperationException();
            }

            await _addressGroup.RemoveAsync(Context);
        }

        public async Task BroadcastRenderAsync(byte[] outputStates)
        {
            Broadcast(_addressGroup).OnRender(outputStates);
            await Task.CompletedTask;
        }

        public async Task BroadcastUnrenderAsync(byte[] outputStates)
        {
            Broadcast(_addressGroup).OnUnrender(outputStates);
            await Task.CompletedTask;
        }

        public async Task BroadcastRenderBlockAsync(byte[] oldTip, byte[] newTip)
        {
            Broadcast(_addressGroup).OnRenderBlock(oldTip, newTip);
            await Task.CompletedTask;
        }

        public async Task ReportReorgAsync(byte[] oldTip, byte[] newTip, byte[] branchpoint)
        {
            Broadcast(_addressGroup).OnReorged(oldTip, newTip, branchpoint);
            await Task.CompletedTask;
        }

        public async Task ReportReorgEndAsync(byte[] oldTip, byte[] newTip, byte[] branchpoint)
        {
            Broadcast(_addressGroup).OnReorgEnd(oldTip, newTip, branchpoint);
            await Task.CompletedTask;
        }

        public async Task ReportExceptionAsync(int code, string message)
        {
            Broadcast(_addressGroup).OnException(code, message);
            await Task.CompletedTask;
        }

        public async Task PreloadStartAsync()
        {
            Broadcast(_addressGroup).OnPreloadStart();
            await Task.CompletedTask;
        }

        public async Task PreloadEndAsync()
        {
            Broadcast(_addressGroup).OnPreloadEnd();
            await Task.CompletedTask;
        }

        protected override ValueTask OnDisconnected()
        {
            OnClientDisconnected?.Invoke(_addressGroup.GroupName);
            return base.OnDisconnected();
        }
    }
}
