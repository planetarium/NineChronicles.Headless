using Bencodex;
using Bencodex.Types;
using Libplanet;
using Libplanet.Action;
using Nekoyume.Model.State;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.Serialization;
using Nekoyume.Action;
using MemberTypes = System.Reflection.MemberTypes;

namespace NineChronicles.Headless.Tests
{
    [Serializable]
    [ActionType("dumb_transfer_action")]
    public class DumbTransferAction : ActionBase, ISerializable
    {
        static DumbTransferAction()
        {
            // Monkey patch PolymorphicAction<T>._actionTypeLoader so that it can load
            // this action type, which is not declared in the same assembly as ActionBase.
            // See also: https://github.com/planetarium/libplanet/pull/2539
            var polyActionType = typeof(PolymorphicAction<ActionBase>);
            var loaderType = typeof(StaticActionTypeLoader);
            var loaderField = (FieldInfo)polyActionType.GetMember(
                "_actionTypeLoader",
                MemberTypes.Field,
                BindingFlags.Static | BindingFlags.NonPublic
            )[0];
            var loader = (StaticActionTypeLoader)loaderField.GetValue(null)!;
            var asmSetField = (FieldInfo)loaderType.GetMember(
                "_assembliesSet",
                MemberTypes.Field,
                BindingFlags.Instance | BindingFlags.NonPublic
            )[0];
            asmSetField.SetValue(
                loader,
                ImmutableHashSet.Create(
                    typeof(ActionBase).Assembly,
                    typeof(DumbTransferAction).Assembly
                )
            );
            var cacheField = (FieldInfo)loaderType.GetMember(
                "_types",
                MemberTypes.Field,
                BindingFlags.Instance | BindingFlags.NonPublic
            )[0];
            cacheField.SetValue(loader, null);
            var typesField = (FieldInfo)polyActionType.GetMember(
                "_types",
                MemberTypes.Field,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static
            )[0];
            typesField.SetValue(null, null);
        }

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
