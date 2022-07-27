using Bencodex;
using Bencodex.Types;
using Libplanet;
using Libplanet.Action;
using Nekoyume.Model.State;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Nekoyume.Action;

namespace NineChronicles.Headless.Tests
{
    [Serializable]
    [ActionType("dumb_transfer_action")]
    public class DumbTransferAction : ActionBase, ISerializable
    {
        public DumbTransferAction()
        {
        }

        public DumbTransferAction(Address sender, Address recipient)
        {
            Sender = sender;
            Recipient = recipient;
        }

        protected DumbTransferAction(SerializationInfo info, StreamingContext context)
        {
            var rawBytes = (byte[])info.GetValue("serialized", typeof(byte[]))!;
            Dictionary pv = (Dictionary)new Codec().Decode(rawBytes);

            LoadPlainValue(pv);
        }

        public Address Sender { get; private set; }
        public Address Recipient { get; private set; }

        public override IValue PlainValue
        {
            get
            {
                IEnumerable<KeyValuePair<IKey, IValue>> pairs = new[]
                {
                    new KeyValuePair<IKey, IValue>((Text) "sender", Sender.Serialize()),
                    new KeyValuePair<IKey, IValue>((Text) "recipient", Recipient.Serialize()),
                };

                return new Dictionary(pairs);
            }
        }

        public override IAccountStateDelta Execute(IActionContext context)
        {
            var state = context.PreviousStates;
            if (context.Rehearsal)
            {
                return state;
            }

            return state;
        }

        public override void LoadPlainValue(IValue plainValue)
        {
            var asDict = (Dictionary)plainValue;

            Sender = asDict["sender"].ToAddress();
            Recipient = asDict["recipient"].ToAddress();
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("serialized", new Codec().Encode(PlainValue));
        }
    }
}

