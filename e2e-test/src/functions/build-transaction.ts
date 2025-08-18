import {
  BencodexDictionary,
  Dictionary,
  Key,
  Value,
  decode,
  encode,
} from '@planetarium/bencodex';
import { Account, signTransaction } from '@planetarium/sign';
import * as fs from 'fs';

import { executeGqlQuery } from './gql.js';
import { adminKey } from './keys.js';
import { log, network } from './config.js';

export type AuthKey = {
  privateKey: string;
  publicKey: string;
  address: string;
  account: Account;
};

const unsignedActionTxQuery = async (
  key: AuthKey,
  txKey: string,
  innerQuery: string,
) => {
  const query = `
  {
    actionTxQuery(publicKey: "${key.publicKey}") {
      ${innerQuery}
    }
  }`;
  const response = await executeGqlQuery(network, query);
  const body = await response?.data;
  if (!body || body.errors) {
    console.error(body.errors);
    throw new Error(
      body.errors.map((error: Error) => error.message).join('\n'),
    );
  }
  const unsignedTx = body.data.actionTxQuery[txKey];

  if (log)
    console.log(
      `[UnsignedActionTxQuery] (${key.address}) Signed Tx: ${unsignedTx}`,
    );

  return unsignedTx;
};

const actionTxQuery = async (
  key: AuthKey,
  txKey: string,
  innerQuery: string,
) => {
  const unsignedTx = await unsignedActionTxQuery(key, txKey, innerQuery);
  const signedTx = await signTransaction(unsignedTx, key.account);

  if (log)
    console.log(`[ActionTxQuery] (${key.address}) Signed Tx: ${signedTx}`);

  return signedTx;
};

export const transferAsset = async (
  key: AuthKey,
  amount: number,
  currency: 'MEAD' | 'NCG',
) => {
  const ncgQuery = `transferAsset(
    sender: "${adminKey.address}",
    recipient: "${key.address}",
    amount: "${amount}",
    currency: ${currency})`;
  const ncgTx = await actionTxQuery(adminKey, 'transferAsset', ncgQuery);

  return ncgTx;
};

export const stake = async (key: AuthKey, amount: number) => {
  const stakeQuery = `stake(amount: ${amount})`;
  const stakeTx = await actionTxQuery(key, 'stake', stakeQuery);

  return stakeTx;
};

export const stake2 = async (key: AuthKey, amount: number) => {
  const stakeQuery = `stake(amount: ${amount})`;
  const stakeTx = await unsignedActionTxQuery(key, 'stake', stakeQuery);

  const buffer = Buffer.from(stakeTx, 'hex');
  const decoded = decode(buffer) as Dictionary;

  const actionsKey: Key = new Uint8Array([0x61]);
  const entries = [...decoded.entries()].map((entry): readonly [Key, Value] => {
    if (entry[0][0] !== actionsKey[0]) return entry;

    const actionEntries = [
      ...((entry[1] as Dictionary[])[0] as Dictionary),
    ].map((entry): readonly [Key, Value] => {
      return [entry[0], entry[0] === 'type_id' ? 'stake2' : entry[1]];
    });
    const action = new BencodexDictionary(actionEntries);

    return [actionsKey, [action]];
  });

  const stake2Dictionary = new BencodexDictionary(entries);
  const unsignedTx = [...encode(stake2Dictionary)]
    .map((byte) => byte.toString(16).padStart(2, '0'))
    .join('');

  const signedTx = await signTransaction(unsignedTx, key.account);

  return signedTx;
};

export const patchTableSheet = async (csvPath: string) => {
  const csvName = csvPath.split('/').pop() as string;
  const csvData = fs.readFileSync(csvPath, 'utf8');

  const trimedCsvData = csvData
    .trim()
    .replaceAll('\r', '')
    .replaceAll('\n', '\\n');
  const query = `patchTableSheet(
    tablename: "${csvName}",
    tableCsv: "${trimedCsvData}")`;
  const tx = await actionTxQuery(adminKey, 'patchTableSheet', query);

  return tx;
};

export const createAvatar = async (
  key: AuthKey,
  index: 0 | 1 | 2,
  name: string,
) => {
  const query = `createAvatar(index: ${index}, name: "${name}")`;
  const tx = await actionTxQuery(key, 'createAvatar', query);

  return tx;
};
