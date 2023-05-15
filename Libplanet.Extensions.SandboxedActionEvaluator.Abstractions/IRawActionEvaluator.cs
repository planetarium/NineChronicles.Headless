namespace Libplanet.Extensions.SandboxedActionEvaluator.Abstractions;

public interface IRawActionEvaluator
{
    byte[] Evaluate(byte[] block);
}
