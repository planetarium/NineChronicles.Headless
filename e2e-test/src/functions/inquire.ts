import { executeGqlQuery } from './gql.js';
import { network } from './config.js';

export const getGoldBalance = async (address: string) => {
  const query = `{ goldBalance(address: "${address}") }`;
  const response = await executeGqlQuery(network, query);
  const result = response?.data?.data?.goldBalance;
  if (!result) return;

  return parseInt(result, 10);
};

export const getMeadBalance = async (address: string) => {
  const query = `
  {
    stateQuery {
      balance(
        address: "${address}",
        currency: {
          ticker: "Mead",
          decimalPlaces: 18,
          minters: []
        }
      ) {
        quantity
      }
    }
  }`;
  const response = await executeGqlQuery(network, query);
  const result = response?.data?.data?.stateQuery?.balance?.quantity;
  if (!result) return;

  return parseInt(result, 10);
};

export const getAvatarAddress = async (address: string) => {
  const query = `{ stateQuery {
    agent(address: "${address}") {
      avatarStates {
        address
      }
    }
  }}`;
  const response = await executeGqlQuery(network, query);
  const result =
    response?.data?.data?.stateQuery?.agent?.avatarStates?.[0]?.address;

  return result as string | undefined;
};

export const getStakeState = async (address: string) => {
  const query = `{ stateQuery {
    stakeState(address: "${address}") {
      address
      deposit
      startedBlockIndex
      cancellableBlockIndex
      receivedBlockIndex
    }
  }}`;
  const response = await executeGqlQuery(network, query);
  const result = response?.data?.data?.stateQuery?.stakeState;
  if (!result) return;

  return {
    address: result.address,
    deposit: parseInt(result.deposit, 10),
    startedBlockIndex: parseInt(result.startedBlockIndex, 10),
    cancellableBlockIndex: parseInt(result.cancellableBlockIndex, 10),
    receivedBlockIndex: parseInt(result.receivedBlockIndex, 10),
  };
};
