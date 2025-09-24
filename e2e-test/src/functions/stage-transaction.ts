import { executeGqlQuery } from './gql.js';
import { log, network } from './config.js';

export const stageTransaction = async (tx: string) => {
  const response = await executeGqlQuery(
    network,
    `mutation {
      stageTransaction(payload: "${tx}")
    }`,
  );

  const result = response?.data?.data?.stageTransaction;

  if (!result) {
    throw new Error(`Failed to stage transaction ${response?.data.toString()}`);
  }

  if (log) console.log('[StageTransaction] TxId:', result);
  return result;
};

export type TxResult = {
  txStatus: 'SUCCESS';
  blockIndex: number;
  blockHash: string;
} | {
  txStatus: 'FAILED';
  exceptionName: string;
} | {
  txStatus: 'STAGING';
};

const checkIsStaged = async (txId: string) => {
  type Response = { data: { transaction: { transactionResult: TxResult; }; }; };
  const response = await executeGqlQuery<Response>(
    network,
    `{
      transaction {
        transactionResult(txId: "${txId}") {
          txStatus
          blockIndex
          blockHash
          exceptionName}}}`,
  );
  if (response?.status !== 200) return false;

  const result = response.data?.data?.transaction?.transactionResult;
  if (result?.txStatus === 'STAGING') return false;

  return result;
};

export const waitForMining = async (txId: string) => {
  while (true) {
    const stageResult = await checkIsStaged(txId);

    if (stageResult) {
      if (log) console.log('[WaitForMining] Stage Result:', stageResult);
      return stageResult;
    }

    await new Promise((resolve) => setTimeout(resolve, 500));
  }
};
