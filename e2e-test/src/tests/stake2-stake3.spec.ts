import '../functions/config.js';

import { expect, test } from 'vitest';

import { generateKeys } from '../functions/keys.js';
import { stake, stake2, transferAsset } from '../functions/build-transaction.js';
import { stageTransaction, waitForMining } from '../functions/stage-transaction.js';
import { getGoldBalance, getStakeState } from '../functions/inquire.js';

const execute = async (tx: string) => {
  const txId = await stageTransaction(tx);

  return waitForMining(txId);
};

test('Test Stake(2) -> Stake(3)', async () => {
  const key = await generateKeys();
  console.log('test with user', key.address);

  await execute(await transferAsset(key, 50, 'NCG'));
  await execute(await transferAsset(key, 10, 'MEAD'));

  const stake2Tx = await stake2(key, 50);
  const stake2Result = await execute(stake2Tx);

  expect(stake2Result.txStatus).toBe('SUCCESS');
  if (stake2Result.txStatus !== 'SUCCESS') return;

  const stake2Balance = await getGoldBalance(key.address);
  expect(stake2Balance).toBe(0);

  const stake2State = await getStakeState(key.address);
  expect(stake2State?.address).not.toBeUndefined();
  expect(stake2State?.deposit).toBe(50);
  expect(stake2State?.startedBlockIndex).toBe(stake2Result.blockIndex);
  expect(stake2State?.cancellableBlockIndex).toBe(stake2Result.blockIndex + 201600);
  expect(stake2State?.receivedBlockIndex).toBe(0);

  const stake3Tx = await stake(key, 50);
  const stake3Result = await execute(stake3Tx);

  expect(stake3Result.txStatus).toBe('SUCCESS');
  if (stake3Result.txStatus !== 'SUCCESS') return;

  const stake3Balance = await getGoldBalance(key.address);
  expect(stake3Balance).toBe(0);

  const stake3State = await getStakeState(key.address);
  expect(stake3State?.address).not.toBeUndefined();
  // expect(stake3State?.address).not.toBe(stake2State?.address);
  expect(stake3State?.deposit).toBe(50);

  console.log(stake3State);
});
