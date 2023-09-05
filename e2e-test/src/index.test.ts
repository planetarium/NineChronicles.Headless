import './config';

import { beforeAll, describe, expect, test } from 'vitest';
import { generateKeys } from './auth';
import { getGoldBalance, getMeadBalance } from './inquire';
import { stake, transferAsset } from './build-transaction';
import { stageTransaction, waitForMining } from './stage-transaction';

test('Key should be generated with no gold balance', async () => {
  const key = await generateKeys();
  const goldBalance = await getGoldBalance(key.address);

  expect(goldBalance).toBe(0);
});

describe('Test claim', async () => {
  const key = await generateKeys();

  beforeAll(async () => {
    const transferAssetTxNcg = await transferAsset(key, 100, 'NCG');
    const ncgTxId = await stageTransaction(transferAssetTxNcg);
    await waitForMining(ncgTxId);

    const transferAssetTxMead = await transferAsset(key, 10, 'MEAD');
    const meadTxId = await stageTransaction(transferAssetTxMead);
    await waitForMining(meadTxId);

    console.log('test with user', key.address);
  });

  test('Gold balance should be 100', async () => {
    const goldBalance = await getGoldBalance(key.address);

    expect(goldBalance).toBe(100);
  });

  test('Mead balance should be 10', async () => {
    const meadBalance = await getMeadBalance(key.address);

    expect(meadBalance).toBe(10);
  });

  test('Stake should be success', async () => {
    const claimTx = await stake(key, 50);
    const txId = await stageTransaction(claimTx);
    const result = await waitForMining(txId);
    const goldBalance = await getGoldBalance(key.address);

    expect(result.txStatus).toBe('SUCCESS');
    expect(goldBalance).toBe(50);
  });
});
